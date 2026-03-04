use crate::schema::SchemaCache;
use crate::scope::ScopeMap;

/// A completion item returned by the analyzer.
#[derive(Debug, Clone)]
pub struct CompletionItem {
    pub label: String,
    pub kind: CompletionKind,
    pub detail: String,
    pub documentation: String,
    pub insert_text: Option<String>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CompletionKind {
    Keyword,
    Function,
    Snippet,
    Variable,
    Table,
    Column,
}

/// Context about where the cursor is in the document.
#[derive(Debug, Clone)]
pub struct CompletionContext {
    /// Text of the line up to the cursor.
    pub line_prefix: String,
    /// Whether we're inside a function argument list.
    pub in_arg_list: bool,
    /// Whether we're after a pipe operator.
    pub after_pipe: bool,
    /// Whether we're inside a lambda body.
    pub in_lambda: bool,
    /// Current word prefix being typed.
    pub word_prefix: String,
    /// The table qualifier if user typed "table." (the part before the dot).
    pub table_qualifier: Option<String>,
}

/// Provide context-aware completions based on cursor position and scope.
/// Falls back gracefully if schema is None (no DB connection).
pub fn get_completions(
    ctx: &CompletionContext,
    scope: &ScopeMap,
    schema: Option<&SchemaCache>,
) -> Vec<CompletionItem> {
    let mut items = Vec::new();
    let prefix = ctx.word_prefix.to_ascii_lowercase();

    // If user typed "table." — suggest columns for that table from schema
    if let Some(ref qualifier) = ctx.table_qualifier {
        if let Some(schema) = schema {
            let columns = schema.get_columns(qualifier);
            for col in columns {
                let col_lower = col.name.to_lowercase();
                if col_lower.starts_with(&prefix) {
                    items.push(CompletionItem {
                        label: col.name.clone(),
                        kind: CompletionKind::Column,
                        detail: col.type_description(),
                        documentation: format!(
                            "Column `{}.{}` — {}",
                            qualifier,
                            col.name,
                            col.type_description()
                        ),
                        insert_text: None,
                    });
                }
            }
        }
        // If we have a qualifier, column completions are primary
        if !items.is_empty() {
            return items;
        }
    }

    // After pipe operator: suggest pipeline operations
    if ctx.after_pipe {
        items.extend(pipeline_completions(&prefix));
    }

    // Inside lambda: suggest comparison operators and logical keywords
    if ctx.in_lambda {
        items.extend(lambda_completions(&prefix));
    }

    // Always available: keywords, built-in functions, scope bindings
    items.extend(keyword_completions(&prefix));
    items.extend(aggregate_completions(&prefix));
    items.extend(string_function_completions(&prefix));

    // Schema-based table completions (real tables from database)
    if let Some(schema) = schema {
        for table_name in schema.table_names() {
            if table_name.to_lowercase().starts_with(&prefix) {
                let table = schema.get_table(table_name).unwrap();
                let col_names: Vec<&str> = table.columns.iter().map(|c| c.name.as_str()).collect();
                let col_list = if col_names.len() <= 6 {
                    col_names.join(", ")
                } else {
                    format!(
                        "{}, ... ({} total)",
                        col_names[..5].join(", "),
                        col_names.len()
                    )
                };
                items.push(CompletionItem {
                    label: table_name.to_string(),
                    kind: CompletionKind::Table,
                    detail: format!("table ({} columns)", table.columns.len()),
                    documentation: format!(
                        "Table: {table_name}\nColumns: {col_list}"
                    ),
                    insert_text: None,
                });
            }
        }
    }

    // Scope-based completions
    for name in scope.binding_names() {
        if name.to_ascii_lowercase().starts_with(&prefix) {
            items.push(CompletionItem {
                label: name.to_string(),
                kind: CompletionKind::Variable,
                detail: "let binding".into(),
                documentation: format!("Variable bound with `let {name} = ...`"),
                insert_text: None,
            });
        }
    }

    for name in scope.table_names() {
        if name.to_ascii_lowercase().starts_with(&prefix) {
            // Skip if already provided by schema
            if schema.map_or(false, |s| s.get_table(name).is_some()) {
                continue;
            }
            items.push(CompletionItem {
                label: name.to_string(),
                kind: CompletionKind::Table,
                detail: "table".into(),
                documentation: format!("Table: {name}"),
                insert_text: None,
            });
        }
    }

    items
}

fn pipeline_completions(prefix: &str) -> Vec<CompletionItem> {
    let ops = vec![
        ("select", "Project columns", "Projects specified columns from the input data", "select($0)"),
        ("filter", "Filter rows", "Filters rows based on a lambda predicate", "filter(fn(row) => $0)"),
        ("join", "Join tables", "Joins with another table on a condition", "join($1, on = $0)"),
        ("left_join", "Left join tables", "Left joins with another table", "left_join($1, on = $0)"),
        ("group_by", "Group rows", "Groups rows by specified columns", "group_by($0)"),
        ("order_by", "Order results", "Orders rows by specified columns", "order_by($0)"),
        ("having", "Filter groups", "Filters groups based on aggregate conditions", "having(fn(group) => $0)"),
        ("limit", "Limit results", "Limits the number of returned rows", "limit($0)"),
        ("offset", "Skip rows", "Skips the first n rows", "offset($0)"),
        ("union", "Union queries", "Combines results of two queries", "union($0)"),
        ("insert", "Insert into table", "Inserts results into a target table", "insert($0)"),
    ];

    ops.into_iter()
        .filter(|(name, _, _, _)| name.starts_with(prefix))
        .map(|(name, detail, doc, snippet)| CompletionItem {
            label: name.into(),
            kind: CompletionKind::Function,
            detail: detail.into(),
            documentation: doc.into(),
            insert_text: Some(snippet.into()),
        })
        .collect()
}

fn aggregate_completions(prefix: &str) -> Vec<CompletionItem> {
    let fns = vec![
        ("count", "Count rows", "count(*) or count(column)"),
        ("sum", "Sum values", "sum(column)"),
        ("avg", "Average values", "avg(column)"),
        ("max", "Maximum value", "max(column)"),
        ("min", "Minimum value", "min(column)"),
        ("row_number", "Row number", "row_number() over (...)"),
        ("rank", "Rank", "rank() over (...)"),
        ("dense_rank", "Dense rank", "dense_rank() over (...)"),
    ];

    fns.into_iter()
        .filter(|(name, _, _)| name.starts_with(prefix))
        .map(|(name, detail, doc)| CompletionItem {
            label: name.into(),
            kind: CompletionKind::Function,
            detail: detail.into(),
            documentation: doc.into(),
            insert_text: Some(format!("{name}($0)")),
        })
        .collect()
}

fn string_function_completions(prefix: &str) -> Vec<CompletionItem> {
    let fns = vec![
        ("concat", "Concatenate strings", "concat(str1, str2, ...)"),
        ("substring", "Extract substring", "substring(string, start, length)"),
        ("length", "String length", "length(string)"),
        ("trim", "Trim whitespace", "trim(string)"),
        ("upper", "Uppercase", "upper(string)"),
        ("lower", "Lowercase", "lower(string)"),
        ("round", "Round number", "round(number, precision)"),
        ("abs", "Absolute value", "abs(number)"),
        ("coalesce", "First non-null", "coalesce(val1, val2, ...)"),
    ];

    fns.into_iter()
        .filter(|(name, _, _)| name.starts_with(prefix))
        .map(|(name, detail, doc)| CompletionItem {
            label: name.into(),
            kind: CompletionKind::Function,
            detail: detail.into(),
            documentation: doc.into(),
            insert_text: Some(format!("{name}($0)")),
        })
        .collect()
}

fn keyword_completions(prefix: &str) -> Vec<CompletionItem> {
    let kws = vec![
        ("let", "Variable binding", "Binds a pipeline result to a name"),
        ("fn", "Lambda function", "fn(params) => expression"),
        ("case", "Case expression", "case when ... then ... end"),
        ("as", "Column alias", "expression as alias_name"),
        ("asc", "Ascending order", "Sort ascending"),
        ("desc", "Descending order", "Sort descending"),
        ("distinct", "Distinct modifier", "Remove duplicates"),
    ];

    kws.into_iter()
        .filter(|(name, _, _)| name.starts_with(prefix))
        .map(|(name, detail, doc)| CompletionItem {
            label: name.into(),
            kind: CompletionKind::Keyword,
            detail: detail.into(),
            documentation: doc.into(),
            insert_text: None,
        })
        .collect()
}

fn lambda_completions(prefix: &str) -> Vec<CompletionItem> {
    let ops = vec![
        ("and", "Logical AND", "Combines conditions with AND"),
        ("or", "Logical OR", "Combines conditions with OR"),
        ("not", "Logical NOT", "Negates a condition"),
        ("is", "IS check", "IS NULL / IS NOT NULL"),
        ("in", "IN check", "Check membership in a set"),
        ("like", "LIKE pattern", "Pattern matching with wildcards"),
        ("exists", "EXISTS subquery", "Check subquery has results"),
    ];

    ops.into_iter()
        .filter(|(name, _, _)| name.starts_with(prefix))
        .map(|(name, detail, doc)| CompletionItem {
            label: name.into(),
            kind: CompletionKind::Keyword,
            detail: detail.into(),
            documentation: doc.into(),
            insert_text: None,
        })
        .collect()
}
