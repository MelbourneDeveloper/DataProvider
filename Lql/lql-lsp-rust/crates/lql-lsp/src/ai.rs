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
