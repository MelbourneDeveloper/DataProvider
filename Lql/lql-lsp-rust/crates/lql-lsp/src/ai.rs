use lql_analyzer::{CompletionItem, CompletionKind};

/// Context provided to AI completion providers.
/// Contains everything a language model needs to generate relevant completions.
#[derive(Debug, Clone)]
pub struct AiCompletionContext {
    /// Full document text.
    pub document_text: String,
    /// Cursor line (0-indexed).
    pub line: u32,
    /// Cursor column (0-indexed).
    pub column: u32,
    /// Text before cursor on the current line.
    pub line_prefix: String,
    /// Word prefix currently being typed (may be empty).
    pub word_prefix: String,
    /// URI of the file being edited.
    pub file_uri: String,
    /// Database table names available (from schema, if loaded).
    pub available_tables: Vec<String>,
    /// Full database schema as a compact text description for AI context.
    /// Format: "table_name(col1 type [PK] [NOT NULL], col2 type, ...)"
    pub schema_description: String,
}

/// Configuration for an AI completion provider, parsed from LSP initializationOptions.
///
/// Example initializationOptions JSON:
/// ```json
/// {
///   "connectionString": "host=localhost ...",
///   "aiProvider": {
///     "provider": "openai",
///     "endpoint": "https://api.openai.com/v1/completions",
///     "model": "gpt-4",
///     "apiKey": "sk-...",
///     "timeoutMs": 2000,
///     "enabled": true
///   }
/// }
/// ```
#[derive(Debug, Clone)]
pub struct AiConfig {
    /// Provider type identifier (e.g., "openai", "anthropic", "ollama", "custom").
    pub provider: String,
    /// API endpoint URL.
    pub endpoint: String,
    /// Model identifier.
    pub model: String,
    /// API key (optional — some providers use other auth mechanisms).
    pub api_key: Option<String>,
    /// Maximum time to wait for AI completions in milliseconds.
    /// AI completions that exceed this are silently dropped.
    pub timeout_ms: u64,
    /// Whether AI completions are enabled.
    pub enabled: bool,
}

/// Trait for AI-powered completion providers.
///
/// Implement this to integrate custom language models for LQL autocomplete.
/// The LSP server calls this alongside schema and keyword completions, merging
/// all results. A timeout is enforced so slow AI responses never block the editor.
///
/// # Implementing a custom provider
///
/// ```rust,ignore
/// use lql_lsp::ai::{AiCompletionProvider, AiCompletionContext, AiConfig};
/// use lql_analyzer::CompletionItem;
///
/// struct MyModelProvider {
///     client: reqwest::Client,
///     config: AiConfig,
/// }
///
/// #[tower_lsp::async_trait]
/// impl AiCompletionProvider for MyModelProvider {
///     async fn complete(&self, ctx: &AiCompletionContext) -> Vec<CompletionItem> {
///         // POST to your model endpoint with ctx.document_text, ctx.line_prefix, etc.
///         // Parse response and return CompletionItems
///         vec![]
///     }
/// }
/// ```
#[tower_lsp::async_trait]
pub trait AiCompletionProvider: Send + Sync {
    /// Generate AI-powered completion suggestions.
    ///
    /// Called on every completion request alongside schema and keyword completions.
    /// Results are merged and sorted by priority. AI items should use
    /// `CompletionKind::Snippet` and `sort_priority: 6` to appear after
    /// schema-based completions.
    ///
    /// Implementations should return quickly — the LSP enforces a timeout
    /// (default 2000ms) and silently drops results that arrive too late.
    async fn complete(&self, context: &AiCompletionContext) -> Vec<CompletionItem>;
}

impl AiConfig {
    /// Parse AI provider configuration from the `aiProvider` key in initializationOptions.
    pub fn from_json(value: &serde_json::Value) -> Option<Self> {
        let obj = value.as_object()?;
        Some(AiConfig {
            provider: obj.get("provider")?.as_str()?.to_string(),
            endpoint: obj.get("endpoint")?.as_str()?.to_string(),
            model: obj
                .get("model")
                .and_then(|v| v.as_str())
                .unwrap_or("default")
                .to_string(),
            api_key: obj.get("apiKey").and_then(|v| v.as_str()).map(String::from),
            timeout_ms: obj
                .get("timeoutMs")
                .and_then(|v| v.as_u64())
                .unwrap_or(2000),
            enabled: obj
                .get("enabled")
                .and_then(|v| v.as_bool())
                .unwrap_or(true),
        })
    }
}

/// Create an AI completion item with the standard priority for AI suggestions.
pub fn ai_completion(
    label: String,
    detail: String,
    documentation: String,
    insert_text: Option<String>,
) -> CompletionItem {
    CompletionItem {
        label,
        kind: CompletionKind::Snippet,
        detail,
        documentation,
        insert_text,
        sort_priority: 6, // After all schema/keyword completions
    }
}

/// Real Ollama-backed AI completion provider.
///
/// Calls the Ollama `/api/generate` endpoint with the LQL reference doc as system
/// context and the current editing context as the prompt. Parses the model's response
/// into completion items.
///
/// Activated when `aiProvider.provider` is `"ollama"` in initializationOptions.
pub struct OllamaProvider {
    /// HTTP client (reused across requests for connection pooling).
    client: reqwest::Client,
    /// Ollama API endpoint (e.g., `http://localhost:11434/api/generate`).
    endpoint: String,
    /// Model identifier (e.g., `qwen2.5-coder:1.5b`).
    model: String,
    /// LQL language reference injected as system context.
    lql_reference: String,
}

impl OllamaProvider {
    /// Create a new OllamaProvider from AI config and the LQL reference document.
    pub fn new(config: &AiConfig, lql_reference: String) -> Self {
        Self {
            client: reqwest::Client::new(),
            endpoint: config.endpoint.clone(),
            model: config.model.clone(),
            lql_reference,
        }
    }

    /// Build the prompt sent to the model.
    fn build_prompt(context: &AiCompletionContext) -> String {
        let schema_section = if context.schema_description.is_empty() {
            String::new()
        } else {
            format!(
                "\nDatabase schema:\n{}\n",
                context.schema_description
            )
        };

        format!(
            "You are an LQL (Lambda Query Language) autocomplete engine.\n\
             Complete the code at line {line}, column {col}.\n\
             The user is typing: \"{prefix}\"\n\
             {schema}\n\
             Current file:\n```lql\n{doc}\n```\n\n\
             Return ONLY a JSON array of completion objects. Each object must have:\n\
             - \"label\": short display text\n\
             - \"insertText\": the text to insert\n\
             - \"detail\": one-line description\n\n\
             Return between 1 and 5 completions. Return ONLY the JSON array, no markdown fences, no explanation.",
            line = context.line + 1,
            col = context.column + 1,
            prefix = context.line_prefix,
            schema = schema_section,
            doc = context.document_text,
        )
    }

    /// Parse the model response into completion items.
    fn parse_response(response_text: &str) -> Vec<CompletionItem> {
        // Strip markdown code fences if the model wraps them
        let cleaned = response_text
            .trim()
            .trim_start_matches("```json")
            .trim_start_matches("```")
            .trim_end_matches("```")
            .trim();

        let items: Vec<serde_json::Value> = match serde_json::from_str(cleaned) {
            Ok(v) => v,
            Err(_) => return Vec::new(),
        };

        items
            .iter()
            .filter_map(|item| {
                let label = item.get("label")?.as_str()?.to_string();
                let insert_text = item.get("insertText").and_then(|v| v.as_str()).map(String::from);
                let detail = item
                    .get("detail")
                    .and_then(|v| v.as_str())
                    .unwrap_or("AI suggestion")
                    .to_string();
                Some(ai_completion(
                    label,
                    detail,
                    "Generated by Ollama".to_string(),
                    insert_text,
                ))
            })
            .collect()
    }
}

#[tower_lsp::async_trait]
impl AiCompletionProvider for OllamaProvider {
    async fn complete(&self, context: &AiCompletionContext) -> Vec<CompletionItem> {
        let prompt = Self::build_prompt(context);

        let body = serde_json::json!({
            "model": self.model,
            "prompt": prompt,
            "system": self.lql_reference,
            "stream": false,
            "options": {
                "temperature": 0.1,
                "num_predict": 256,
            }
        });

        let response = match self.client.post(&self.endpoint).json(&body).send().await {
            Ok(r) => r,
            Err(_) => return Vec::new(),
        };

        let json: serde_json::Value = match response.json().await {
            Ok(j) => j,
            Err(_) => return Vec::new(),
        };

        let text = match json.get("response").and_then(|v| v.as_str()) {
            Some(t) => t,
            None => return Vec::new(),
        };

        Self::parse_response(text)
    }
}

/// Built-in test AI provider that returns deterministic completions.
/// Activated when `aiProvider.provider` is `"test"` in initializationOptions.
/// This proves the full AI pipeline works end-to-end without an external service.
pub struct TestAiProvider;

#[tower_lsp::async_trait]
impl AiCompletionProvider for TestAiProvider {
    async fn complete(&self, context: &AiCompletionContext) -> Vec<CompletionItem> {
        let mut items = vec![
            ai_completion(
                "ai_suggest_filter".to_string(),
                "AI Suggestion".to_string(),
                format!(
                    "AI-generated filter suggestion based on context at line {}, col {}",
                    context.line, context.column
                ),
                Some("filter(x => x.${1:column} == ${2:value})".to_string()),
            ),
            ai_completion(
                "ai_suggest_join".to_string(),
                "AI Suggestion".to_string(),
                "AI-generated join suggestion".to_string(),
                Some("join(${1:table}, on: x => x.${2:fk} == y.${3:pk})".to_string()),
            ),
            ai_completion(
                "ai_suggest_aggregate".to_string(),
                "AI Suggestion".to_string(),
                "AI-generated aggregation pattern".to_string(),
                Some("group_by(x => x.${1:key}) |> select(g => { key: g.key, total: sum(g.${2:value}) })".to_string()),
            ),
        ];

        // Context-aware: if tables are available, suggest table-specific completions
        for table in &context.available_tables {
            items.push(ai_completion(
                format!("ai_query_{table}"),
                format!("AI: Query {table}"),
                format!("AI-generated query pattern for table '{table}'"),
                Some(format!("{table} |> filter(x => x.${{1:column}} == ${{2:value}}) |> select(x => x)")),
            ));
        }

        // Surface schema description so tests can prove it flows through
        if !context.schema_description.is_empty() {
            items.push(ai_completion(
                "ai_schema_context".to_string(),
                "AI: Schema Loaded".to_string(),
                context.schema_description.clone(),
                None,
            ));
        }

        // Prefix filtering: only return items matching the word prefix
        if !context.word_prefix.is_empty() {
            let prefix = context.word_prefix.to_lowercase();
            items.retain(|item| item.label.to_lowercase().starts_with(&prefix));
        }

        items
    }
}

/// Built-in slow AI provider for testing timeout enforcement.
/// Activated when `aiProvider.provider` is `"test_slow"` in initializationOptions.
/// Sleeps for the configured duration to prove timeouts work.
pub struct SlowAiProvider {
    /// How long to sleep before returning results (milliseconds).
    pub delay_ms: u64,
}

#[tower_lsp::async_trait]
impl AiCompletionProvider for SlowAiProvider {
    async fn complete(&self, _context: &AiCompletionContext) -> Vec<CompletionItem> {
        tokio::time::sleep(std::time::Duration::from_millis(self.delay_ms)).await;
        vec![ai_completion(
            "ai_slow_result".to_string(),
            "Slow AI Result".to_string(),
            "This should never appear if timeout works".to_string(),
            None,
        )]
    }
}
