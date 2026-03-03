use lql_analyzer::{
    analyze, extract_symbols, get_completions, get_hover, CompletionContext, CompletionKind,
    DiagnosticSeverity as LqlSeverity, ScopeMap, SymbolKind as LqlSymbolKind,
};
use lql_parser::parse_lql;
use std::collections::HashMap;
use std::sync::Mutex;
use tower_lsp::jsonrpc::Result;
use tower_lsp::lsp_types::*;
use tower_lsp::{Client, LanguageServer, LspService, Server};

struct LqlBackend {
    client: Client,
    documents: Mutex<HashMap<Url, String>>,
}

impl LqlBackend {
    fn new(client: Client) -> Self {
        Self {
            client,
            documents: Mutex::new(HashMap::new()),
        }
    }

    fn build_scope(source: &str) -> ScopeMap {
        let mut scope = ScopeMap::new();

        for line in source.lines() {
            let trimmed = line.trim();

            // Extract let bindings
            if trimmed.starts_with("let ") {
                let rest = &trimmed[4..];
                let name: String = rest
                    .chars()
                    .take_while(|c| c.is_alphanumeric() || *c == '_')
                    .collect();
                if !name.is_empty() {
                    scope.add_binding(name, 0, 0);
                }
            }

            // Extract table references (first ident in a pipeline or after join)
            let bytes = trimmed.as_bytes();
            let mut i = 0;
            while i < bytes.len() {
                if bytes[i].is_ascii_alphabetic() || bytes[i] == b'_' {
                    let start = i;
                    while i < bytes.len()
                        && (bytes[i].is_ascii_alphanumeric() || bytes[i] == b'_')
                    {
                        i += 1;
                    }
                    let word = &trimmed[start..i];
                    // Heuristic: if followed by `|>` or `.` it's likely a table
                    let rest_trimmed = trimmed[i..].trim_start();
                    if rest_trimmed.starts_with("|>") || rest_trimmed.starts_with('.') {
                        let lower = word.to_ascii_lowercase();
                        if !is_keyword_name(&lower) {
                            scope.add_table(word.to_string());
                        }
                    }
                } else {
                    i += 1;
                }
            }
        }

        scope
    }

    async fn publish_diagnostics(&self, uri: Url, source: &str) {
        // Build diagnostics synchronously (ANTLR parse tree uses Rc, not Send)
        let diags = Self::collect_diagnostics(source);
        self.client.publish_diagnostics(uri, diags, None).await;
    }

    fn collect_diagnostics(source: &str) -> Vec<tower_lsp::lsp_types::Diagnostic> {
        let scope = Self::build_scope(source);

        // Parse errors from ANTLR — parse_result contains Rc so must not cross await
        let parse_result = parse_lql(source);
        let mut diags: Vec<tower_lsp::lsp_types::Diagnostic> = parse_result
            .errors
            .iter()
            .map(|e| {
                let (start_line, start_col) = e.span.start_line_col(source);
                let (end_line, end_col) = e.span.end_line_col(source);
                tower_lsp::lsp_types::Diagnostic {
                    range: Range {
                        start: Position::new(start_line, start_col),
                        end: Position::new(end_line, end_col),
                    },
                    severity: Some(tower_lsp::lsp_types::DiagnosticSeverity::ERROR),
                    source: Some("lql".into()),
                    message: e.message.clone(),
                    ..Default::default()
                }
            })
            .collect();
        drop(parse_result); // Explicitly drop Rc-containing result

        // Semantic diagnostics from analyzer
        let analyzer_diags = analyze(source, &scope);
        for d in analyzer_diags {
            let severity = match d.severity {
                LqlSeverity::Error => tower_lsp::lsp_types::DiagnosticSeverity::ERROR,
                LqlSeverity::Warning => tower_lsp::lsp_types::DiagnosticSeverity::WARNING,
                LqlSeverity::Info => tower_lsp::lsp_types::DiagnosticSeverity::INFORMATION,
                LqlSeverity::Hint => tower_lsp::lsp_types::DiagnosticSeverity::HINT,
            };
            diags.push(tower_lsp::lsp_types::Diagnostic {
                range: Range {
                    start: Position::new(d.line, d.col),
                    end: Position::new(d.line, d.end_col),
                },
                severity: Some(severity),
                source: Some("lql".into()),
                message: d.message,
                ..Default::default()
            });
        }

        diags
    }

    fn get_word_at_position(source: &str, position: Position) -> Option<String> {
        let line = source.lines().nth(position.line as usize)?;
        let col = position.character as usize;

        if col > line.len() {
            return None;
        }

        let bytes = line.as_bytes();
        let mut start = col;
        let mut end = col;

        while start > 0
            && (bytes[start - 1].is_ascii_alphanumeric() || bytes[start - 1] == b'_')
        {
            start -= 1;
        }
        while end < bytes.len() && (bytes[end].is_ascii_alphanumeric() || bytes[end] == b'_') {
            end += 1;
        }

        if start == end {
            None
        } else {
            Some(line[start..end].to_string())
        }
    }

    fn compute_completion_context(source: &str, position: Position) -> CompletionContext {
        let line = source
            .lines()
            .nth(position.line as usize)
            .unwrap_or("");
        let col = (position.character as usize).min(line.len());
        let line_prefix = &line[..col];

        // Determine word prefix
        let word_prefix: String = line_prefix
            .chars()
            .rev()
            .take_while(|c| c.is_alphanumeric() || *c == '_')
            .collect::<String>()
            .chars()
            .rev()
            .collect();

        let trimmed_prefix = line_prefix.trim();

        // Check if after a pipe — also look at prefix without current word
        let prefix_before_word = line_prefix.trim_end_matches(|c: char| c.is_alphanumeric() || c == '_');
        let after_pipe = trimmed_prefix.ends_with("|>")
            || trimmed_prefix.ends_with("|> ")
            || prefix_before_word.trim_end().ends_with("|>");

        // Check if we're in an argument list (simplified heuristic)
        let open_parens = line_prefix.matches('(').count();
        let close_parens = line_prefix.matches(')').count();
        let in_arg_list = open_parens > close_parens;

        // Check if we're in a lambda (look for `fn(...)` pattern in context)
        let in_lambda = source[..source.lines().take(position.line as usize + 1)
            .map(|l| l.len() + 1)
            .sum::<usize>()
            .min(source.len())]
            .contains("=>");

        CompletionContext {
            line_prefix: line_prefix.to_string(),
            in_arg_list,
            after_pipe,
            in_lambda,
            word_prefix,
        }
    }
}

fn is_keyword_name(word: &str) -> bool {
    matches!(
        word,
        "let" | "fn" | "as" | "asc" | "desc" | "and" | "or" | "not"
            | "distinct" | "exists" | "null" | "is" | "in"
            | "case" | "when" | "then" | "else" | "end"
            | "with" | "over" | "partition" | "order" | "by"
            | "on" | "like" | "from" | "interval"
            | "select" | "filter" | "join" | "group_by" | "order_by"
            | "having" | "limit" | "offset" | "union" | "insert"
    )
}

#[tower_lsp::async_trait]
impl LanguageServer for LqlBackend {
    async fn initialize(&self, _: InitializeParams) -> Result<InitializeResult> {
        Ok(InitializeResult {
            capabilities: ServerCapabilities {
                text_document_sync: Some(TextDocumentSyncCapability::Kind(
                    TextDocumentSyncKind::FULL,
                )),
                completion_provider: Some(CompletionOptions {
                    trigger_characters: Some(vec![
                        ".".into(),
                        "|".into(),
                        ">".into(),
                        "(".into(),
                        " ".into(),
                    ]),
                    resolve_provider: Some(false),
                    ..Default::default()
                }),
                hover_provider: Some(HoverProviderCapability::Simple(true)),
                document_symbol_provider: Some(OneOf::Left(true)),
                document_formatting_provider: Some(OneOf::Left(true)),
                ..Default::default()
            },
            ..Default::default()
        })
    }

    async fn initialized(&self, _: InitializedParams) {
        self.client
            .log_message(MessageType::INFO, "LQL Language Server initialized")
            .await;
    }

    async fn shutdown(&self) -> Result<()> {
        Ok(())
    }

    async fn did_open(&self, params: DidOpenTextDocumentParams) {
        let uri = params.text_document.uri.clone();
        let text = params.text_document.text.clone();
        self.documents
            .lock()
            .unwrap()
            .insert(uri.clone(), text.clone());
        self.publish_diagnostics(uri, &text).await;
    }

    async fn did_change(&self, params: DidChangeTextDocumentParams) {
        let uri = params.text_document.uri.clone();
        if let Some(change) = params.content_changes.into_iter().last() {
            let text = change.text.clone();
            self.documents
                .lock()
                .unwrap()
                .insert(uri.clone(), text.clone());
            self.publish_diagnostics(uri, &text).await;
        }
    }

    async fn did_close(&self, params: DidCloseTextDocumentParams) {
        self.documents
            .lock()
            .unwrap()
            .remove(&params.text_document.uri);
    }

    async fn completion(&self, params: CompletionParams) -> Result<Option<CompletionResponse>> {
        let uri = &params.text_document_position.text_document.uri;
        let position = params.text_document_position.position;

        let docs = self.documents.lock().unwrap();
        let source = match docs.get(uri) {
            Some(s) => s.clone(),
            None => return Ok(None),
        };
        drop(docs);

        let scope = Self::build_scope(&source);
        let ctx = Self::compute_completion_context(&source, position);
        let items = get_completions(&ctx, &scope);

        let lsp_items: Vec<tower_lsp::lsp_types::CompletionItem> = items
            .into_iter()
            .map(|item| {
                let kind = match item.kind {
                    CompletionKind::Keyword => CompletionItemKind::KEYWORD,
                    CompletionKind::Function => CompletionItemKind::FUNCTION,
                    CompletionKind::Snippet => CompletionItemKind::SNIPPET,
                    CompletionKind::Variable => CompletionItemKind::VARIABLE,
                    CompletionKind::Table => CompletionItemKind::CLASS,
                    CompletionKind::Column => CompletionItemKind::FIELD,
                };
                tower_lsp::lsp_types::CompletionItem {
                    label: item.label,
                    kind: Some(kind),
                    detail: Some(item.detail),
                    documentation: Some(Documentation::String(item.documentation)),
                    insert_text: item.insert_text,
                    insert_text_format: Some(InsertTextFormat::SNIPPET),
                    ..Default::default()
                }
            })
            .collect();

        Ok(Some(CompletionResponse::Array(lsp_items)))
    }

    async fn hover(&self, params: HoverParams) -> Result<Option<Hover>> {
        let uri = &params.text_document_position_params.text_document.uri;
        let position = params.text_document_position_params.position;

        let docs = self.documents.lock().unwrap();
        let source = match docs.get(uri) {
            Some(s) => s.clone(),
            None => return Ok(None),
        };
        drop(docs);

        let word = match Self::get_word_at_position(&source, position) {
            Some(w) => w,
            None => return Ok(None),
        };

        match get_hover(&word) {
            Some(info) => {
                let mut content = format!("**{}**\n\n{}", info.title, info.detail);
                if let Some(sig) = info.signature {
                    content.push_str(&format!("\n\n```lql\n{sig}\n```"));
                }
                Ok(Some(Hover {
                    contents: HoverContents::Markup(MarkupContent {
                        kind: MarkupKind::Markdown,
                        value: content,
                    }),
                    range: None,
                }))
            }
            None => Ok(None),
        }
    }

    async fn document_symbol(
        &self,
        params: DocumentSymbolParams,
    ) -> Result<Option<DocumentSymbolResponse>> {
        let uri = &params.text_document.uri;

        let docs = self.documents.lock().unwrap();
        let source = match docs.get(uri) {
            Some(s) => s.clone(),
            None => return Ok(None),
        };
        drop(docs);

        let symbols = extract_symbols(&source);
        let lsp_symbols: Vec<tower_lsp::lsp_types::SymbolInformation> = symbols
            .into_iter()
            .map(|sym| {
                let kind = match sym.kind {
                    LqlSymbolKind::Variable => tower_lsp::lsp_types::SymbolKind::VARIABLE,
                    LqlSymbolKind::Function => tower_lsp::lsp_types::SymbolKind::FUNCTION,
                    LqlSymbolKind::Table => tower_lsp::lsp_types::SymbolKind::CLASS,
                };
                #[allow(deprecated)]
                tower_lsp::lsp_types::SymbolInformation {
                    name: sym.name,
                    kind,
                    tags: None,
                    deprecated: None,
                    location: Location {
                        uri: uri.clone(),
                        range: Range {
                            start: Position::new(sym.line, sym.col),
                            end: Position::new(sym.end_line, sym.end_col),
                        },
                    },
                    container_name: None,
                }
            })
            .collect();

        Ok(Some(DocumentSymbolResponse::Flat(lsp_symbols)))
    }

    async fn formatting(
        &self,
        params: DocumentFormattingParams,
    ) -> Result<Option<Vec<TextEdit>>> {
        let uri = &params.text_document.uri;

        let docs = self.documents.lock().unwrap();
        let source = match docs.get(uri) {
            Some(s) => s.clone(),
            None => return Ok(None),
        };
        drop(docs);

        let formatted = format_lql(&source);
        if formatted == source {
            return Ok(None);
        }

        let line_count = source.lines().count() as u32;
        let last_line_len = source.lines().last().map(|l| l.len()).unwrap_or(0) as u32;

        Ok(Some(vec![TextEdit {
            range: Range {
                start: Position::new(0, 0),
                end: Position::new(line_count, last_line_len),
            },
            new_text: formatted,
        }]))
    }
}

/// Basic LQL formatter — normalizes indentation.
fn format_lql(source: &str) -> String {
    let mut result = String::new();
    let mut indent = 0usize;
    let indent_str = "    ";

    for line in source.lines() {
        let trimmed = line.trim();
        if trimmed.is_empty() {
            result.push('\n');
            continue;
        }

        // Decrease indent for closing parens at line start
        if trimmed.starts_with(')') {
            indent = indent.saturating_sub(1);
        }

        // Pipeline continuation
        if trimmed.starts_with("|>") && indent == 0 {
            indent = 1;
        }

        result.push_str(&indent_str.repeat(indent));
        result.push_str(trimmed);
        result.push('\n');

        // Increase indent after opening constructs
        if trimmed.ends_with('(') && !trimmed.starts_with("--") {
            indent += 1;
        }
    }

    result
}

#[tokio::main]
async fn main() {
    let stdin = tokio::io::stdin();
    let stdout = tokio::io::stdout();

    let (service, socket) = LspService::new(LqlBackend::new);
    Server::new(stdin, stdout, socket).serve(service).await;
}
