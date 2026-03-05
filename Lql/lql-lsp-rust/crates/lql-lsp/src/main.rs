pub mod ai;
mod db;

use ai::{AiCompletionContext, AiCompletionProvider, AiConfig};
use lql_analyzer::{
    analyze, extract_symbols, get_completions, get_hover_with_schema, CompletionContext,
    CompletionKind, DiagnosticSeverity as LqlSeverity, SchemaCache, ScopeMap,
    SymbolKind as LqlSymbolKind,
};
use lql_parser::parse_lql;
use std::collections::HashMap;
use std::sync::{Arc, Mutex};
use std::time::Duration;
use tokio::sync::RwLock;
use tower_lsp::jsonrpc::Result;
use tower_lsp::lsp_types::*;
use tower_lsp::{Client, LanguageServer, LspService, Server};

struct LqlBackend {
    client: Client,
    documents: Mutex<HashMap<Url, String>>,
    schema: RwLock<Option<SchemaCache>>,
    init_connection_string: Mutex<Option<String>>,
    ai_provider: RwLock<Option<Arc<dyn AiCompletionProvider>>>,
    ai_config: Mutex<Option<AiConfig>>,
}

impl LqlBackend {
    fn new(client: Client) -> Self {
        Self {
            client,
            documents: Mutex::new(HashMap::new()),
            schema: RwLock::new(None),
            init_connection_string: Mutex::new(None),
            ai_provider: RwLock::new(None),
            ai_config: Mutex::new(None),
        }
    }

    /// Set an AI completion provider. Called externally to plug in a model.
    pub async fn set_ai_provider(&self, provider: Arc<dyn AiCompletionProvider>) {
        *self.ai_provider.write().await = Some(provider);
    }

    fn build_scope(source: &str) -> ScopeMap {
        let mut scope = ScopeMap::new();
        for line in source.lines() {
            let trimmed = line.trim();
            if trimmed.starts_with("let ") {
                let name: String = trimmed[4..]
                    .chars()
                    .take_while(|c| c.is_alphanumeric() || *c == '_')
                    .collect();
                if !name.is_empty() {
                    scope.add_binding(name, 0, 0);
                }
            }
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
        let diags = Self::collect_diagnostics(source);
        self.client.publish_diagnostics(uri, diags, None).await;
    }

    fn collect_diagnostics(source: &str) -> Vec<tower_lsp::lsp_types::Diagnostic> {
        let scope = Self::build_scope(source);
        let parse_result = parse_lql(source);
        let mut diags: Vec<tower_lsp::lsp_types::Diagnostic> = parse_result
            .errors
            .iter()
            .map(|e| {
                let (sl, sc) = e.span.start_line_col(source);
                let (el, ec) = e.span.end_line_col(source);
                tower_lsp::lsp_types::Diagnostic {
                    range: Range {
                        start: Position::new(sl, sc),
                        end: Position::new(el, ec),
                    },
                    severity: Some(tower_lsp::lsp_types::DiagnosticSeverity::ERROR),
                    source: Some("lql".into()),
                    message: e.message.clone(),
                    ..Default::default()
                }
            })
            .collect();
        drop(parse_result);

        for d in analyze(source, &scope) {
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

    /// Detect qualified "Table.Column" at cursor for hover.
    /// Returns (word, Option<(table_name, column_name)>).
    fn get_qualified_at_position(
        source: &str,
        position: Position,
    ) -> (Option<String>, Option<(String, String)>) {
        let line = match source.lines().nth(position.line as usize) {
            Some(l) => l,
            None => return (None, None),
        };
        let col = position.character as usize;
        if col > line.len() {
            return (None, None);
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
            return (None, None);
        }
        let word = line[start..end].to_string();

        // Check for "qualifier." before the word (need at least 1 char + dot)
        if start >= 1 && bytes[start - 1] == b'.' && start >= 2 {
            let dot_pos = start - 1;
            let mut q_start = dot_pos;
            while q_start > 0
                && (bytes[q_start - 1].is_ascii_alphanumeric() || bytes[q_start - 1] == b'_')
            {
                q_start -= 1;
            }
            if q_start < dot_pos {
                let qualifier = line[q_start..dot_pos].to_string();
                return (Some(word.clone()), Some((qualifier, word)));
            }
        }

        (Some(word), None)
    }

    fn compute_completion_context(source: &str, position: Position) -> CompletionContext {
        let line = source
            .lines()
            .nth(position.line as usize)
            .unwrap_or("");
        let col = (position.character as usize).min(line.len());
        let line_prefix = &line[..col];

        let word_prefix: String = line_prefix
            .chars()
            .rev()
            .take_while(|c| c.is_alphanumeric() || *c == '_')
            .collect::<String>()
            .chars()
            .rev()
            .collect();

        let trimmed_prefix = line_prefix.trim();
        let prefix_before_word =
            line_prefix.trim_end_matches(|c: char| c.is_alphanumeric() || c == '_');
        let after_pipe = trimmed_prefix.ends_with("|>")
            || trimmed_prefix.ends_with("|> ")
            || prefix_before_word.trim_end().ends_with("|>");

        let open_parens = line_prefix.matches('(').count();
        let close_parens = line_prefix.matches(')').count();
        let in_arg_list = open_parens > close_parens;

        let in_lambda = source[..source
            .lines()
            .take(position.line as usize + 1)
            .map(|l| l.len() + 1)
            .sum::<usize>()
            .min(source.len())]
            .contains("=>");

        // Detect table qualifier: "Table." or "Table.prefix"
        let before_word = &line_prefix[..line_prefix.len() - word_prefix.len()];
        let table_qualifier = if before_word.ends_with('.') {
            let q: String = before_word[..before_word.len() - 1]
                .chars()
                .rev()
                .take_while(|c| c.is_alphanumeric() || *c == '_')
                .collect::<String>()
                .chars()
                .rev()
                .collect();
            if q.is_empty() { None } else { Some(q) }
        } else {
            None
        };

        CompletionContext {
            line_prefix: line_prefix.to_string(),
            in_arg_list,
            after_pipe,
            in_lambda,
            word_prefix,
            table_qualifier,
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
    async fn initialize(&self, params: InitializeParams) -> Result<InitializeResult> {
        // Extract connection string and AI config from initializationOptions
        if let Some(ref options) = params.initialization_options {
            if let Some(conn) = options.get("connectionString").and_then(|v| v.as_str()) {
                if !conn.is_empty() {
                    *self.init_connection_string.lock().unwrap() = Some(conn.to_string());
                }
            }
            if let Some(ai_obj) = options.get("aiProvider") {
                if let Some(config) = AiConfig::from_json(ai_obj) {
                    *self.ai_config.lock().unwrap() = Some(config);
                }
            }
        }

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

        // Set up AI provider (before DB check, which may return early)
        let ai_config = self.ai_config.lock().unwrap().clone();
        if let Some(ref config) = ai_config {
            if config.enabled {
                // Activate built-in test providers or log external config
                match config.provider.as_str() {
                    "test" => {
                        self.set_ai_provider(Arc::new(ai::TestAiProvider)).await;
                        self.client
                            .log_message(
                                MessageType::INFO,
                                "AI test provider activated — returns deterministic completions",
                            )
                            .await;
                    }
                    "test_slow" => {
                        let delay = config.timeout_ms.saturating_add(5000);
                        self.set_ai_provider(Arc::new(ai::SlowAiProvider { delay_ms: delay }))
                            .await;
                        self.client
                            .log_message(
                                MessageType::INFO,
                                format!(
                                    "AI slow test provider activated — {}ms delay (timeout: {}ms)",
                                    delay, config.timeout_ms
                                ),
                            )
                            .await;
                    }
                    "ollama" => {
                        let lql_ref = include_str!("../../lql-reference.md").to_string();
                        self.set_ai_provider(Arc::new(ai::OllamaProvider::new(config, lql_ref)))
                            .await;
                        self.client
                            .log_message(
                                MessageType::INFO,
                                format!(
                                    "Ollama AI provider activated (model: {}, endpoint: {})",
                                    config.model, config.endpoint
                                ),
                            )
                            .await;
                    }
                    _ => {
                        self.client
                            .log_message(
                                MessageType::INFO,
                                format!(
                                    "AI completion provider configured: {} (model: {}, endpoint: {})",
                                    config.provider, config.model, config.endpoint
                                ),
                            )
                            .await;
                    }
                }
            }
        }

        // Resolve connection string: initializationOptions > env var
        let conn_str = self
            .init_connection_string
            .lock()
            .unwrap()
            .clone()
            .or_else(db::discover_connection_string);

        let conn_str = match conn_str {
            Some(s) => s,
            None => {
                self.client
                    .log_message(
                        MessageType::INFO,
                        "No DB connection configured (set LQL_CONNECTION_STRING)",
                    )
                    .await;
                return;
            }
        };

        self.client
            .log_message(MessageType::INFO, "Connecting to database for schema...")
            .await;

        // Initial schema fetch — write directly since we have &self
        match db::fetch_schema(&conn_str).await {
            Ok(cache) => {
                let count = cache.table_count();
                *self.schema.write().await = Some(cache);
                self.client
                    .log_message(
                        MessageType::INFO,
                        format!("Schema loaded: {count} tables"),
                    )
                    .await;
            }
            Err(e) => {
                self.client
                    .log_message(
                        MessageType::WARNING,
                        format!("Schema fetch failed: {e}"),
                    )
                    .await;
            }
        }
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

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

        let scope = Self::build_scope(&source);
        let ctx = Self::compute_completion_context(&source, position);

        // Read schema (cheap clone — SchemaCache uses Arc internally)
        let schema = self.schema.read().await.clone();
        let mut items = get_completions(&ctx, &scope, schema.as_ref());

        // Merge AI completions if a provider is configured
        let ai_provider = self.ai_provider.read().await.clone();
        if let Some(ref provider) = ai_provider {
            let ai_config = self.ai_config.lock().unwrap().clone();
            let timeout_ms = ai_config.as_ref().map(|c| c.timeout_ms).unwrap_or(2000);
            let enabled = ai_config.as_ref().map(|c| c.enabled).unwrap_or(true);

            if enabled {
                // Build compact schema description for AI context
                let (available_tables, schema_description) = match schema.as_ref() {
                    Some(s) => {
                        let names: Vec<String> = s.table_names().iter().map(|n| n.to_string()).collect();
                        let desc = names.iter().map(|name| {
                            let cols = s.get_columns(name);
                            let col_strs: Vec<String> = cols.iter().map(|c| {
                                let mut s = format!("{} {}", c.name, c.sql_type);
                                if c.is_primary_key { s.push_str(" PK"); }
                                if !c.is_nullable { s.push_str(" NOT NULL"); }
                                s
                            }).collect();
                            format!("{}({})", name, col_strs.join(", "))
                        }).collect::<Vec<_>>().join("\n");
                        (names, desc)
                    }
                    None => (Vec::new(), String::new()),
                };

                let ai_ctx = AiCompletionContext {
                    document_text: source.clone(),
                    line: position.line,
                    column: position.character,
                    line_prefix: ctx.line_prefix.clone(),
                    word_prefix: ctx.word_prefix.clone(),
                    file_uri: uri.to_string(),
                    available_tables,
                    schema_description,
                };

                match tokio::time::timeout(
                    Duration::from_millis(timeout_ms),
                    provider.complete(&ai_ctx),
                )
                .await
                {
                    Ok(ai_items) => items.extend(ai_items),
                    Err(_) => {
                        // AI provider timed out — silently proceed with schema/keyword completions
                    }
                }
            }
        }

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

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

        let (word, qualified) = Self::get_qualified_at_position(&source, position);
        let word = match word {
            Some(w) => w,
            None => return Ok(None),
        };

        let schema = self.schema.read().await.clone();
        let qualified_refs = qualified.as_ref().map(|(t, c)| (t.as_str(), c.as_str()));
        let info = match get_hover_with_schema(&word, qualified_refs, schema.as_ref()) {
            Some(info) => info,
            None => return Ok(None),
        };

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

    async fn document_symbol(
        &self,
        params: DocumentSymbolParams,
    ) -> Result<Option<DocumentSymbolResponse>> {
        let uri = &params.text_document.uri;

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

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

        let source = {
            let docs = self.documents.lock().unwrap();
            match docs.get(uri) {
                Some(s) => s.clone(),
                None => return Ok(None),
            }
        };

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
        if trimmed.starts_with(')') {
            indent = indent.saturating_sub(1);
        }
        if trimmed.starts_with("|>") && indent == 0 {
            indent = 1;
        }
        result.push_str(&indent_str.repeat(indent));
        result.push_str(trimmed);
        result.push('\n');
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
