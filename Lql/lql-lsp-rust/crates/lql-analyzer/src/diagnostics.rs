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
    "select",
    "filter",
    "join",
    "left_join",
    "right_join",
    "cross_join",
    "group_by",
    "order_by",
    "having",
    "limit",
    "offset",
    "union",
    "union_all",
    "insert",
    "update",
    "delete",
    "count",
    "sum",
    "avg",
    "min",
    "max",
    "first",
    "last",
    "concat",
    "substring",
    "length",
    "trim",
    "upper",
    "lower",
    "replace",
    "round",
    "floor",
    "ceil",
    "abs",
    "sqrt",
    "power",
    "mod",
    "now",
    "today",
    "year",
    "month",
    "day",
    "hour",
    "minute",
    "second",
    "coalesce",
    "nullif",
    "isnull",
    "isnotnull",
    "row_number",
    "rank",
    "dense_rank",
    "ntile",
    "lag",
    "lead",
    "extract",
    "date_trunc",
    "current_date",
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
    for (line_num, line) in source.lines().enumerate() {
        for (col, ch) in line.chars().enumerate() {
            if ch == '-' && col + 1 < line.len() && line.as_bytes().get(col + 1) == Some(&b'-') {
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
        "let"
            | "fn"
            | "as"
            | "asc"
            | "desc"
            | "and"
            | "or"
            | "not"
            | "distinct"
            | "exists"
            | "null"
            | "is"
            | "in"
            | "case"
            | "when"
            | "then"
            | "else"
            | "end"
            | "with"
            | "over"
            | "partition"
            | "order"
            | "by"
            | "on"
            | "like"
            | "from"
            | "interval"
    )
}

#[cfg(test)]
mod tests {
    use super::*;

    fn empty_scope() -> ScopeMap {
        ScopeMap::new()
    }

    // ── analyze basics ──

    #[test]
    fn test_analyze_empty_source() {
        let diags = analyze("", &empty_scope());
        assert!(diags.is_empty());
    }

    #[test]
    fn test_analyze_clean_pipeline() {
        let diags = analyze("users |> select(users.id)", &empty_scope());
        // select is known, no pipe spacing issues
        assert!(diags.is_empty());
    }

    #[test]
    fn test_analyze_skips_comments() {
        let diags = analyze("-- this is a comment\n-- another", &empty_scope());
        assert!(diags.is_empty());
    }

    #[test]
    fn test_analyze_skips_empty_lines() {
        let diags = analyze("\n\n\n", &empty_scope());
        assert!(diags.is_empty());
    }

    // ── pipe spacing ──

    #[test]
    fn test_pipe_no_space_before() {
        let diags = analyze("users|> select(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("preceded by"))
            .collect();
        assert_eq!(pipe_diags.len(), 1);
        assert_eq!(pipe_diags[0].severity, DiagnosticSeverity::Warning);
    }

    #[test]
    fn test_pipe_no_space_after() {
        let diags = analyze("users |>select(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("followed by"))
            .collect();
        assert_eq!(pipe_diags.len(), 1);
    }

    #[test]
    fn test_pipe_no_space_both_sides() {
        let diags = analyze("users|>select(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Pipeline operator"))
            .collect();
        assert_eq!(pipe_diags.len(), 2);
    }

    #[test]
    fn test_pipe_with_tab_is_ok() {
        let diags = analyze("users\t|>\tselect(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Pipeline operator"))
            .collect();
        assert!(pipe_diags.is_empty());
    }

    #[test]
    fn test_pipe_at_start_of_line() {
        let diags = analyze("|> select(users.id)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("preceded by"))
            .collect();
        assert!(pipe_diags.is_empty());
    }

    #[test]
    fn test_pipe_at_end_of_line() {
        let diags = analyze("users |>", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("followed by"))
            .collect();
        assert!(pipe_diags.is_empty());
    }

    #[test]
    fn test_multiple_pipes_on_one_line() {
        let diags = analyze("a |> filter(x) |> select(y)", &empty_scope());
        let pipe_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Pipeline operator"))
            .collect();
        assert!(pipe_diags.is_empty());
    }

    // ── unknown functions ──

    #[test]
    fn test_unknown_function_detected() {
        let diags = analyze("foobar(x)", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert_eq!(unk.len(), 1);
        assert!(unk[0].message.contains("foobar"));
        assert_eq!(unk[0].severity, DiagnosticSeverity::Info);
    }

    #[test]
    fn test_known_functions_not_flagged() {
        let known_fns = ["select", "filter", "count", "sum", "avg", "max", "min",
            "join", "left_join", "group_by", "order_by", "having",
            "limit", "offset", "union", "concat", "substring", "length",
            "trim", "upper", "lower", "round", "abs", "coalesce",
            "row_number", "rank", "dense_rank", "now", "today"];
        for func in &known_fns {
            let src = format!("{func}(x)");
            let diags = analyze(&src, &empty_scope());
            let unk: Vec<_> = diags
                .iter()
                .filter(|d| d.message.contains("Unknown function"))
                .collect();
            assert!(unk.is_empty(), "Function '{func}' should be known but was flagged");
        }
    }

    #[test]
    fn test_keywords_not_flagged_as_unknown() {
        // Keywords followed by ( should not be flagged
        let keywords = ["let", "fn", "case", "exists", "not", "is", "in"];
        for kw in &keywords {
            let src = format!("{kw}(x)");
            let diags = analyze(&src, &empty_scope());
            let unk: Vec<_> = diags
                .iter()
                .filter(|d| d.message.contains("Unknown function"))
                .collect();
            assert!(unk.is_empty(), "Keyword '{kw}' should not be flagged");
        }
    }

    #[test]
    fn test_scope_binding_not_flagged() {
        let mut scope = ScopeMap::new();
        scope.add_binding("my_func".to_string(), 0, 0);
        let diags = analyze("my_func(x)", &scope);
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert!(unk.is_empty());
    }

    #[test]
    fn test_word_not_followed_by_paren_not_flagged() {
        let diags = analyze("foobar = 42", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert!(unk.is_empty());
    }

    #[test]
    fn test_string_literal_skipped() {
        let diags = analyze("filter('foobar(x)')", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("foobar"))
            .collect();
        assert!(unk.is_empty());
    }

    #[test]
    fn test_unknown_function_position() {
        let diags = analyze("  foobar(x)", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert_eq!(unk.len(), 1);
        assert_eq!(unk[0].col, 2);
        assert_eq!(unk[0].end_col, 8);
    }

    #[test]
    fn test_fn_keyword_not_flagged() {
        let diags = analyze("fn(row)", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert!(unk.is_empty());
    }

    #[test]
    fn test_function_with_whitespace_before_paren() {
        let diags = analyze("foobar  (x)", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert_eq!(unk.len(), 1);
    }

    // ── bracket checking ──

    #[test]
    fn test_balanced_brackets() {
        let diags = analyze("select(count(x))", &empty_scope());
        let bracket_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("parenthesis"))
            .collect();
        assert!(bracket_diags.is_empty());
    }

    #[test]
    fn test_unclosed_bracket() {
        let diags = analyze("select(x", &empty_scope());
        let bracket_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("unclosed"))
            .collect();
        assert_eq!(bracket_diags.len(), 1);
        assert_eq!(bracket_diags[0].severity, DiagnosticSeverity::Error);
    }

    #[test]
    fn test_extra_closing_bracket() {
        let diags = analyze("select(x))", &empty_scope());
        let bracket_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unmatched closing"))
            .collect();
        assert_eq!(bracket_diags.len(), 1);
    }

    #[test]
    fn test_multiple_unclosed() {
        let diags = analyze("a((b(c", &empty_scope());
        let bracket_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("unclosed"))
            .collect();
        assert_eq!(bracket_diags.len(), 1);
        assert!(bracket_diags[0].message.contains("3"));
    }

    #[test]
    fn test_brackets_in_string_ignored() {
        let diags = analyze("select('(')", &empty_scope());
        let bracket_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("parenthesis") || d.message.contains("unclosed"))
            .collect();
        assert!(bracket_diags.is_empty());
    }

    #[test]
    fn test_brackets_after_comment_ignored() {
        let diags = analyze("-- select((((", &empty_scope());
        assert!(diags.is_empty());
    }

    #[test]
    fn test_multiline_balanced() {
        let source = "select(\n  x,\n  y\n)";
        let diags = analyze(source, &empty_scope());
        let bracket_diags: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("parenthesis") || d.message.contains("unclosed"))
            .collect();
        assert!(bracket_diags.is_empty());
    }

    // ── combined analysis ──

    #[test]
    fn test_multiple_diagnostics() {
        let source = "users|>foobar(x";
        let diags = analyze(source, &empty_scope());
        assert!(diags.len() >= 2); // pipe spacing + unknown function or unclosed bracket
    }

    #[test]
    fn test_diagnostic_line_numbers() {
        let source = "ok line\nusers|>select(x)";
        let diags = analyze(source, &empty_scope());
        let pipe_diag = diags.iter().find(|d| d.message.contains("preceded by")).unwrap();
        assert_eq!(pipe_diag.line, 1);
    }

    #[test]
    fn test_case_insensitive_known_functions() {
        let diags = analyze("SELECT(x)", &empty_scope());
        let unk: Vec<_> = diags
            .iter()
            .filter(|d| d.message.contains("Unknown function"))
            .collect();
        assert!(unk.is_empty());
    }
}
