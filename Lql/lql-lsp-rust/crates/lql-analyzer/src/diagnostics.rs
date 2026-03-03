use crate::scope::ScopeMap;

/// Diagnostic severity level.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DiagnosticSeverity {
    Error,
    Warning,
    Info,
    Hint,
}

/// A diagnostic message with location.
#[derive(Debug, Clone)]
pub struct Diagnostic {
    pub line: u32,
    pub col: u32,
    pub end_col: u32,
    pub message: String,
    pub severity: DiagnosticSeverity,
}

/// Known pipeline functions.
const KNOWN_FUNCTIONS: &[&str] = &[
    "select", "filter", "join", "left_join", "right_join", "cross_join",
    "group_by", "order_by", "having", "limit", "offset",
    "union", "union_all", "insert", "update", "delete",
    "count", "sum", "avg", "min", "max", "first", "last",
    "concat", "substring", "length", "trim", "upper", "lower", "replace",
    "round", "floor", "ceil", "abs", "sqrt", "power", "mod",
    "now", "today", "year", "month", "day", "hour", "minute", "second",
    "coalesce", "nullif", "isnull", "isnotnull",
    "row_number", "rank", "dense_rank", "ntile", "lag", "lead",
    "extract", "date_trunc", "current_date",
];

/// Run semantic analysis on LQL source text and return diagnostics.
pub fn analyze(source: &str, scope: &ScopeMap) -> Vec<Diagnostic> {
    let mut diagnostics = Vec::new();

    for (line_idx, line) in source.lines().enumerate() {
        let trimmed = line.trim();
        let line_num = line_idx as u32;

        // Skip empty lines and comments
        if trimmed.is_empty() || trimmed.starts_with("--") {
            continue;
        }

        check_pipe_spacing(line, line_num, &mut diagnostics);
        check_unknown_functions(line, line_num, scope, &mut diagnostics);
    }

    // Document-level bracket check (not per-line, since multiline parens are normal in LQL)
    check_unmatched_brackets_document(source, &mut diagnostics);

    diagnostics
}

fn check_pipe_spacing(line: &str, line_num: u32, diagnostics: &mut Vec<Diagnostic>) {
    let bytes = line.as_bytes();
    let mut i = 0;
    while i + 1 < bytes.len() {
        if bytes[i] == b'|' && bytes[i + 1] == b'>' {
            if i > 0 && bytes[i - 1] != b' ' && bytes[i - 1] != b'\t' {
                diagnostics.push(Diagnostic {
                    line: line_num,
                    col: i as u32,
                    end_col: (i + 2) as u32,
                    message: "Pipeline operator `|>` should be preceded by a space".into(),
                    severity: DiagnosticSeverity::Warning,
                });
            }
            if i + 2 < bytes.len() && bytes[i + 2] != b' ' && bytes[i + 2] != b'\t' {
                diagnostics.push(Diagnostic {
                    line: line_num,
                    col: i as u32,
                    end_col: (i + 2) as u32,
                    message: "Pipeline operator `|>` should be followed by a space".into(),
                    severity: DiagnosticSeverity::Warning,
                });
            }
            i += 2;
        } else {
            i += 1;
        }
    }
}

fn check_unknown_functions(
    line: &str,
    line_num: u32,
    scope: &ScopeMap,
    diagnostics: &mut Vec<Diagnostic>,
) {
    // Find patterns like `word(` that look like function calls
    let bytes = line.as_bytes();
    let mut i = 0;

    while i < bytes.len() {
        // Skip if inside a string literal
        if bytes[i] == b'\'' {
            i += 1;
            while i < bytes.len() && bytes[i] != b'\'' {
                if bytes[i] == b'\\' {
                    i += 1;
                }
                i += 1;
            }
            i += 1;
            continue;
        }

        if bytes[i].is_ascii_alphabetic() || bytes[i] == b'_' {
            let start = i;
            while i < bytes.len() && (bytes[i].is_ascii_alphanumeric() || bytes[i] == b'_') {
                i += 1;
            }
            let word = &line[start..i];

            // Check if followed by '(' — indicating a function call
            let mut j = i;
            while j < bytes.len() && bytes[j].is_ascii_whitespace() {
                j += 1;
            }

            if j < bytes.len() && bytes[j] == b'(' {
                let lower = word.to_ascii_lowercase();
                // Skip known functions/keywords and scope bindings
                if !KNOWN_FUNCTIONS.contains(&lower.as_str())
                    && !is_keyword(&lower)
                    && !scope.has_binding(word)
                    && lower != "fn"
                {
                    diagnostics.push(Diagnostic {
                        line: line_num,
                        col: start as u32,
                        end_col: i as u32,
                        message: format!("Unknown function: `{word}`"),
                        severity: DiagnosticSeverity::Info,
                    });
                }
            }
        } else {
            i += 1;
        }
    }
}

/// Document-level bracket balance check. Only reports if the entire document
/// has unmatched brackets — individual lines may legitimately have unbalanced
/// parens in multiline LQL expressions.
fn check_unmatched_brackets_document(source: &str, diagnostics: &mut Vec<Diagnostic>) {
    let mut depth: i32 = 0;
    let mut in_string = false;
    let mut in_comment = false;

    for (line_num, line) in source.lines().enumerate() {
        in_comment = false;
        for (col, ch) in line.chars().enumerate() {
            if in_comment {
                break;
            }
            if ch == '-' && col + 1 < line.len() && line.as_bytes().get(col + 1) == Some(&b'-') {
                in_comment = true;
                break;
            }
            if ch == '\'' && !in_string {
                in_string = true;
                continue;
            }
            if ch == '\'' && in_string {
                in_string = false;
                continue;
            }
            if in_string {
                continue;
            }

            match ch {
                '(' => depth += 1,
                ')' => {
                    depth -= 1;
                    if depth < 0 {
                        diagnostics.push(Diagnostic {
                            line: line_num as u32,
                            col: col as u32,
                            end_col: (col + 1) as u32,
                            message: "Unmatched closing parenthesis".into(),
                            severity: DiagnosticSeverity::Error,
                        });
                        depth = 0;
                    }
                }
                _ => {}
            }
        }
    }

    if depth > 0 {
        let last_line = source.lines().count().saturating_sub(1) as u32;
        let last_col = source.lines().last().map(|l| l.len()).unwrap_or(0) as u32;
        diagnostics.push(Diagnostic {
            line: last_line,
            col: last_col,
            end_col: last_col,
            message: format!("{depth} unclosed parenthesis(es)"),
            severity: DiagnosticSeverity::Error,
        });
    }
}

fn is_keyword(word: &str) -> bool {
    matches!(
        word,
        "let" | "fn" | "as" | "asc" | "desc" | "and" | "or" | "not"
            | "distinct" | "exists" | "null" | "is" | "in"
            | "case" | "when" | "then" | "else" | "end"
            | "with" | "over" | "partition" | "order" | "by"
            | "on" | "like" | "from" | "interval"
    )
}
