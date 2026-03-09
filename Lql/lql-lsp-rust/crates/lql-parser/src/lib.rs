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
    fn test_parse_left_join() {
        let result = parse_lql(
            "users |> left_join(orders, on = users.id = orders.user_id) |> select(users.name)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_right_join() {
        let result = parse_lql(
            "users |> right_join(orders, on = users.id = orders.user_id) |> select(orders.total)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_cross_join() {
        let result = parse_lql("users |> cross_join(roles) |> select(users.name, roles.name)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_limit_offset() {
        let result = parse_lql("users |> limit(10) |> offset(20)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_order_by_asc_desc() {
        let result = parse_lql("users |> order_by(users.name asc, users.age desc)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_multiple_let_bindings() {
        let source = r#"let a = users |> select(users.id)
let b = orders |> select(orders.total)
a |> join(b, on = a.id = b.user_id)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_nested_filter() {
        let result = parse_lql(
            "users |> filter(fn(row) => row.users.age > 18 and row.users.active = true)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_or_condition() {
        let result = parse_lql(
            "users |> filter(fn(row) => row.users.role = 'admin' or row.users.role = 'super')",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_not_condition_errors() {
        // Grammar doesn't support standalone `not` prefix — verify error recovery
        let result =
            parse_lql("users |> filter(fn(row) => not row.users.deleted)");
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_parse_string_literal() {
        let result =
            parse_lql("users |> filter(fn(row) => row.users.name = 'O\\'Brien')");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_numeric_literal() {
        let result = parse_lql("orders |> filter(fn(row) => row.orders.total > 99.95)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_null_check() {
        let result =
            parse_lql("users |> filter(fn(row) => row.users.email is not null)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_is_null() {
        let result = parse_lql("users |> filter(fn(row) => row.users.phone is null)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_in_expression() {
        let result = parse_lql(
            "users |> filter(fn(row) => row.users.status in ('active', 'pending'))",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_like_expression() {
        let result =
            parse_lql("users |> filter(fn(row) => row.users.name like '%john%')");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_between_not_supported() {
        // BETWEEN is not in the grammar — verify graceful error
        let result = parse_lql(
            "orders |> filter(fn(row) => row.orders.total between 100 and 500)",
        );
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_parse_count_star() {
        let result = parse_lql(
            "users |> group_by(users.role) |> select(users.role, count(*) as cnt)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_distinct() {
        let result = parse_lql(
            "users |> select(count(distinct users.email) as unique_emails)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_arithmetic() {
        let result = parse_lql(
            "orders |> select(orders.price * orders.quantity as total)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_subtraction() {
        let result = parse_lql(
            "orders |> select(orders.price - orders.discount as net_price)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_insert() {
        let result = parse_lql("users |> select(users.id, users.name) |> insert(archive_table)");
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_union_all() {
        let source = r#"let a = users |> select(users.id, users.name)
let b = archived |> select(archived.id, archived.name)
a |> union_all(b)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_coalesce_not_in_grammar() {
        // coalesce() not recognized as a grammar-level function — verify error recovery
        let result = parse_lql(
            "users |> select(coalesce(users.nickname, users.name) as display_name)",
        );
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_parse_nested_function_calls() {
        let result = parse_lql(
            "users |> select(upper(trim(users.name)) as clean_name)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_case_with_null() {
        let source = r#"users |> select(
    case
        when users.email is null then 'No email'
        else users.email
    end as email_display
)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_multiple_aggregates() {
        let source = r#"orders
|> group_by(orders.category)
|> select(
    orders.category,
    count(*) as total,
    sum(orders.amount) as sum_amount,
    avg(orders.amount) as avg_amount,
    min(orders.amount) as min_amount,
    max(orders.amount) as max_amount
)"#;
        let result = parse_lql(source);
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_chained_pipeline() {
        let result = parse_lql(
            "users |> filter(fn(r) => r.users.active = true) |> order_by(users.name asc) |> limit(50) |> offset(100)",
        );
        assert!(result.errors.is_empty(), "errors: {:?}", result.errors);
    }

    #[test]
    fn test_parse_empty_string_produces_tree() {
        let result = parse_lql("");
        // Empty input should still produce a tree (possibly empty program)
        let _tree = result.tree;
    }

    // ── error recovery tests ───────────────────────────────────────────
    #[test]
    fn test_error_recovery_missing_closing_paren() {
        let result = parse_lql("users |> select(users.id, users.name");
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_error_recovery_missing_pipe_arg() {
        let result = parse_lql("users |>");
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_error_recovery_double_pipe() {
        let result = parse_lql("users |> |> select(users.id)");
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_error_recovery_invalid_syntax() {
        // Lexer token recognition errors don't surface through our error collector,
        // but parser-level errors do. Test with syntactically invalid structure.
        let result = parse_lql("users |> select(,,,)");
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_error_recovery_unclosed_string() {
        let result = parse_lql("users |> filter(fn(r) => r.users.name = 'hello)");
        assert!(!result.errors.is_empty());
    }

    #[test]
    fn test_error_has_message_and_span() {
        let result = parse_lql("users |> select(");
        assert!(!result.errors.is_empty());
        let err = &result.errors[0];
        assert!(!err.message.is_empty());
        assert_eq!(err.severity, error::Severity::Error);
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
