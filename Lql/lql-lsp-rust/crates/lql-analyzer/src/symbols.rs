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
