/// A document symbol for outline/breadcrumb display.
#[derive(Debug, Clone)]
pub struct DocumentSymbol {
    pub name: String,
    pub kind: SymbolKind,
    pub line: u32,
    pub col: u32,
    pub end_line: u32,
    pub end_col: u32,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SymbolKind {
    Variable,
    Function,
    Table,
}

/// Extract document symbols (let bindings, table references) from LQL source.
pub fn extract_symbols(source: &str) -> Vec<DocumentSymbol> {
    let mut symbols = Vec::new();

    for (line_idx, line) in source.lines().enumerate() {
        let trimmed = line.trim();
        let line_num = line_idx as u32;

        // Detect let bindings
        if trimmed.starts_with("let ") {
            if let Some(name) = extract_let_name(trimmed) {
                let col = line.find("let").unwrap_or(0) as u32;
                symbols.push(DocumentSymbol {
                    name,
                    kind: SymbolKind::Variable,
                    line: line_num,
                    col,
                    end_line: line_num,
                    end_col: col + trimmed.len() as u32,
                });
            }
        }
    }

    symbols
}

fn extract_let_name(trimmed: &str) -> Option<String> {
    let rest = trimmed.strip_prefix("let ")?;
    let name: String = rest
        .chars()
        .take_while(|c| c.is_alphanumeric() || *c == '_')
        .collect();
    if name.is_empty() {
        None
    } else {
        Some(name)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_extract_single_let() {
        let syms = extract_symbols("let x = users |> select(users.id)");
        assert_eq!(syms.len(), 1);
        assert_eq!(syms[0].name, "x");
        assert_eq!(syms[0].kind, SymbolKind::Variable);
        assert_eq!(syms[0].line, 0);
    }

    #[test]
    fn test_extract_multiple_lets() {
        let source = "let alpha = users\nlet beta = orders";
        let syms = extract_symbols(source);
        assert_eq!(syms.len(), 2);
        assert_eq!(syms[0].name, "alpha");
        assert_eq!(syms[0].line, 0);
        assert_eq!(syms[1].name, "beta");
        assert_eq!(syms[1].line, 1);
    }

    #[test]
    fn test_extract_no_lets() {
        let syms = extract_symbols("users |> select(users.id)");
        assert!(syms.is_empty());
    }

    #[test]
    fn test_extract_let_with_underscore() {
        let syms = extract_symbols("let my_var = stuff");
        assert_eq!(syms.len(), 1);
        assert_eq!(syms[0].name, "my_var");
    }

    #[test]
    fn test_extract_empty_source() {
        let syms = extract_symbols("");
        assert!(syms.is_empty());
    }

    #[test]
    fn test_extract_indented_let() {
        let syms = extract_symbols("  let x = 1");
        assert_eq!(syms.len(), 1);
        assert_eq!(syms[0].name, "x");
    }

    #[test]
    fn test_extract_let_no_name() {
        let syms = extract_symbols("let = bad");
        assert!(syms.is_empty());
    }

    #[test]
    fn test_extract_let_name_with_digits() {
        let syms = extract_symbols("let var2 = stuff");
        assert_eq!(syms.len(), 1);
        assert_eq!(syms[0].name, "var2");
    }

    #[test]
    fn test_symbol_end_col() {
        let source = "let abc = stuff";
        let syms = extract_symbols(source);
        assert_eq!(syms.len(), 1);
        assert!(syms[0].end_col > syms[0].col);
    }

    #[test]
    fn test_mixed_content() {
        let source = "-- comment\nlet x = users\nusers |> select(users.id)\nlet y = orders";
        let syms = extract_symbols(source);
        assert_eq!(syms.len(), 2);
        assert_eq!(syms[0].name, "x");
        assert_eq!(syms[1].name, "y");
    }

    #[test]
    fn test_extract_let_name_helper() {
        assert_eq!(extract_let_name("let foo = bar"), Some("foo".to_string()));
        assert_eq!(extract_let_name("let x_1 = y"), Some("x_1".to_string()));
        assert_eq!(extract_let_name("let = bad"), None);
        assert_eq!(extract_let_name("not a let"), None);
    }
}
