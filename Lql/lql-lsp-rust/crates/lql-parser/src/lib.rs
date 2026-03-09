#![allow(dead_code)]
#![allow(nonstandard_style)]

pub mod error;
pub mod generated;
pub mod span;

pub use generated::lqllexer::LqlLexer;
pub use generated::lqllistener::LqlListener;
pub use generated::lqlparser::*;
pub use generated::lqlvisitor::LqlVisitor;

use antlr_rust::common_token_stream::CommonTokenStream;
use antlr_rust::error_listener::ErrorListener;
use antlr_rust::parser::Parser as AntlrParser;
use antlr_rust::recognizer::Recognizer;
use antlr_rust::InputStream;
use error::ParseError;
use span::Span;
use std::rc::Rc;

/// Collected errors from parsing.
pub struct LqlParseResult<'input> {
    pub tree: Rc<ProgramContextAll<'input>>,
    pub errors: Vec<ParseError>,
}

/// Parse LQL source text, returning the parse tree and any errors.
/// This function always returns a (possibly partial) tree — essential for LSP use.
pub fn parse_lql(source: &str) -> LqlParseResult<'_> {
    let input = InputStream::new(source);
    let lexer = LqlLexer::new(input);
    let error_collector = ErrorCollector::new(source);
    let error_collector_ref = Box::new(error_collector.clone());

    let token_source = CommonTokenStream::new(lexer);
    let mut parser = LqlParser::new(token_source);
    parser.remove_error_listeners();
    parser.add_error_listener(error_collector_ref);

    let tree = parser
        .program()
        .expect("ANTLR parser should always produce a tree");

    LqlParseResult {
        tree,
        errors: error_collector.take_errors(),
    }
}

/// Error listener that collects parse errors with span information.
#[derive(Clone)]
struct ErrorCollector {
    errors: Rc<std::cell::RefCell<Vec<ParseError>>>,
    source: String,
}

impl ErrorCollector {
    fn new(source: &str) -> Self {
        Self {
            errors: Rc::new(std::cell::RefCell::new(Vec::new())),
            source: source.to_string(),
        }
    }

    fn take_errors(&self) -> Vec<ParseError> {
        self.errors.borrow_mut().drain(..).collect()
    }
}

impl<'input, T: Recognizer<'input>> ErrorListener<'input, T> for ErrorCollector {
    fn syntax_error(
        &self,
        _recognizer: &T,
        _offending_symbol: Option<
            &<T::TF as antlr_rust::token_factory::TokenFactory<'input>>::Inner,
        >,
        line: isize,
        column: isize,
        msg: &str,
        _error: Option<&antlr_rust::errors::ANTLRError>,
    ) {
        let byte_offset = line_col_to_byte_offset(&self.source, line as u32, column as u32);
        let span = Span::new(byte_offset, byte_offset + 1);
        self.errors
            .borrow_mut()
            .push(ParseError::error(msg.to_string(), span));
    }
}

fn line_col_to_byte_offset(source: &str, line: u32, col: u32) -> u32 {
    let mut current_line = 1u32;
    let mut current_col = 0u32;
    for (i, ch) in source.char_indices() {
        if current_line == line && current_col == col {
            return i as u32;
        }
        if ch == '\n' {
            current_line += 1;
            current_col = 0;
        } else {
            current_col += 1;
        }
    }
    source.len() as u32
}

#[cfg(test)]
mod tests {
    use super::*;
    use antlr_rust::tree::ParseTree;

    #[test]
    fn test_parse_simple_select() {
        let result = parse_lql("users |> select(users.id, users.name)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
        let text = result.tree.get_text();
        assert!(text.contains("users"));
    }

    #[test]
    fn test_parse_filter() {
        let result =
            parse_lql("users |> filter(fn(row) => row.users.age > 18) |> select(users.name)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_let_binding() {
        let result = parse_lql("let x = users |> select(users.id)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_aggregation() {
        let source = r#"orders
|> group_by(orders.user_id, orders.status)
|> select(
    orders.user_id,
    orders.status,
    count(*) as order_count,
    sum(orders.total) as total_amount,
    avg(orders.total) as avg_amount
)
|> having(fn(group) => count(*) > 2)
|> order_by(total_amount desc)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_join() {
        let result =
            parse_lql("users |> join(orders, on = users.id = orders.user_id) |> select(users.name, orders.total)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_case_expression() {
        let source = r#"orders |> select(
    orders.id,
    case
        when orders.total_amount > 1000 then orders.total_amount * 0.95
        when orders.total_amount > 500 then orders.total_amount * 0.97
        else orders.total_amount
    end as discounted_amount
)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_window_function() {
        let result = parse_lql(
            "orders |> select(row_number() over (partition by orders.user_id order by orders.total desc))",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_exists_subquery() {
        let source = r#"users
|> filter(fn(row) => exists(
    orders
    |> filter(fn(o) => o.orders.user_id = row.users.id and o.orders.status = 'completed')
))
|> select(users.id, users.name)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_complex_union() {
        let source = r#"let joined =
    users
    |> join(orders, on = users.id = orders.user_id)
    |> filter(fn(row) => row.orders.status = 'completed')

let all_users =
    joined
    |> select(users.id, users.name)
    |> union(
        archived_users
        |> select(archived_users.id, archived_users.name)
    )

all_users |> insert(report_table)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_error_recovery() {
        let result = parse_lql("users |> select(");
        // Should have errors but not panic
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_parse_window_multiarg() {
        let source = r#"orders
|> select(
    orders.id,
    orders.user_id,
    orders.total,
    row_number() over (partition by orders.user_id order by orders.total desc),
    sum(orders.total) over (partition by orders.user_id) as user_total
)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }
}
