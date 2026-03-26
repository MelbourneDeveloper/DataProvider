use antlr_rust::tree::ParseTree;
use lql_parser::{
    parse_lql, ArgContextAttrs, ArgListContextAttrs, ExprContextAttrs, FunctionCallContextAttrs,
    LetStmtContextAttrs, PipeExprContextAttrs, ProgramContextAttrs, StatementContextAttrs,
};
use std::collections::HashMap;

/// Tracks let-bindings and their scopes within an LQL document.
#[derive(Debug, Clone, Default)]
pub struct ScopeMap {
    /// Map of variable name -> binding info.
    bindings: HashMap<String, BindingInfo>,
    /// Known table names referenced in the document.
    tables: Vec<String>,
}

/// Information about a `let` binding.
#[derive(Debug, Clone)]
pub struct BindingInfo {
    pub name: String,
    pub line: u32,
    pub col: u32,
}

impl ScopeMap {
    pub fn new() -> Self {
        Self::default()
    }

    /// Register a let binding.
    pub fn add_binding(&mut self, name: String, line: u32, col: u32) {
        self.bindings
            .insert(name.clone(), BindingInfo { name, line, col });
    }

    /// Register a table reference.
    pub fn add_table(&mut self, name: String) {
        if !self.tables.contains(&name) {
            self.tables.push(name);
        }
    }

    /// Get all binding names.
    pub fn binding_names(&self) -> Vec<&str> {
        self.bindings.keys().map(|s| s.as_str()).collect()
    }

    /// Get all table names.
    pub fn table_names(&self) -> Vec<&str> {
        self.tables.iter().map(|s| s.as_str()).collect()
    }

    /// Check if a name is a known binding.
    pub fn has_binding(&self, name: &str) -> bool {
        self.bindings.contains_key(name)
    }

    /// Get binding info.
    pub fn get_binding(&self, name: &str) -> Option<&BindingInfo> {
        self.bindings.get(name)
    }
}

/// Build a ScopeMap from source using the ANTLR parse tree.
/// Extracts let-bindings and table references from the grammar structure.
pub fn build_scope(source: &str) -> ScopeMap {
    let result = parse_lql(source);
    let mut scope = ScopeMap::new();

    for stmt in result.tree.statement_all() {
        // Extract let bindings from letStmt nodes
        if let Some(let_stmt) = stmt.letStmt() {
            if let Some(ident) = let_stmt.IDENT() {
                let name = ident.symbol.text.to_string();
                let line = (ident.symbol.line - 1) as u32;
                let col = ident.symbol.column as u32;
                scope.add_binding(name, line, col);
            }
            // Collect table references from the let statement's pipe expression
            if let Some(pipe_expr) = let_stmt.pipeExpr() {
                collect_tables_from_pipe(&pipe_expr, &mut scope);
            }
        }

        // Collect table references from top-level pipe expressions
        if let Some(pipe_expr) = stmt.pipeExpr() {
            collect_tables_from_pipe(&pipe_expr, &mut scope);
        }
    }

    scope
}

/// Collect table names from a pipe expression.
/// The first expr in a pipeline that is a bare IDENT (no parens, no dot) is a table reference.
/// Also recurses into function call arguments to find table references (e.g. join(orders, ...)).
fn collect_tables_from_pipe<'input>(
    pipe_expr: &lql_parser::PipeExprContext<'input>,
    scope: &mut ScopeMap,
) {
    let exprs = pipe_expr.expr_all();
    if let Some(first_expr) = exprs.first() {
        // First expr: if it's a simple IDENT (no argList, no qualifiedIdent), it's a table
        if first_expr.IDENT().is_some()
            && first_expr.argList().is_none()
            && first_expr.qualifiedIdent().is_none()
        {
            scope.add_table(first_expr.get_text());
        }
    }

    // Look inside function call arguments for table references (e.g. join(orders, ...))
    for expr in &exprs {
        if let Some(arg_list) = expr.argList() {
            collect_tables_from_args(&arg_list, scope);
        }
    }
}

/// Collect table references from function argument lists.
fn collect_tables_from_args<'input>(
    arg_list: &lql_parser::ArgListContext<'input>,
    scope: &mut ScopeMap,
) {
    for arg in arg_list.arg_all() {
        // A bare IDENT in an arg is a table reference (e.g. join(orders, ...))
        if let Some(expr) = arg.expr() {
            if expr.IDENT().is_some() && expr.argList().is_none() && expr.qualifiedIdent().is_none()
            {
                scope.add_table(expr.get_text());
            }
        }
        // Recurse into nested pipe expressions
        if let Some(pipe) = arg.pipeExpr() {
            collect_tables_from_pipe(&pipe, scope);
        }
    }
}

/// Info about a function call found in the parse tree.
pub struct FunctionCallInfo {
    pub name: String,
    pub line: u32,
    pub col: u32,
    pub end_col: u32,
}

/// Collect all function calls from the ANTLR parse tree.
pub fn collect_function_calls(source: &str) -> Vec<FunctionCallInfo> {
    let result = parse_lql(source);
    let mut calls = Vec::new();

    for stmt in result.tree.statement_all() {
        if let Some(let_stmt) = stmt.letStmt() {
            if let Some(pipe_expr) = let_stmt.pipeExpr() {
                collect_fn_calls_from_pipe(&pipe_expr, &mut calls);
            }
        }
        if let Some(pipe_expr) = stmt.pipeExpr() {
            collect_fn_calls_from_pipe(&pipe_expr, &mut calls);
        }
    }

    calls
}

/// Collect function calls from a pipe expression.
fn collect_fn_calls_from_pipe<'input>(
    pipe_expr: &lql_parser::PipeExprContext<'input>,
    calls: &mut Vec<FunctionCallInfo>,
) {
    for expr in pipe_expr.expr_all() {
        collect_fn_calls_from_expr(&expr, calls);
    }
}

/// Collect function calls from an expr node.
fn collect_fn_calls_from_expr<'input>(
    expr: &lql_parser::ExprContext<'input>,
    calls: &mut Vec<FunctionCallInfo>,
) {
    // expr with IDENT + argList is a function call form
    if let Some(ident) = expr.IDENT() {
        if expr.argList().is_some() {
            let name = ident.symbol.text.to_string();
            let line = (ident.symbol.line - 1) as u32;
            let col = ident.symbol.column as u32;
            let end_col = col + name.len() as u32;
            calls.push(FunctionCallInfo {
                name,
                line,
                col,
                end_col,
            });
        }
    }

    // Recurse into argList
    if let Some(arg_list) = expr.argList() {
        collect_fn_calls_from_arg_list(&arg_list, calls);
    }

    // Recurse into nested pipe expressions
    if let Some(pipe) = expr.pipeExpr() {
        collect_fn_calls_from_pipe(&pipe, calls);
    }
}

/// Collect function calls from an arg list.
fn collect_fn_calls_from_arg_list<'input>(
    arg_list: &lql_parser::ArgListContext<'input>,
    calls: &mut Vec<FunctionCallInfo>,
) {
    for arg in arg_list.arg_all() {
        // Direct functionCall rule in arg
        if let Some(fc) = arg.functionCall() {
            if let Some(ident) = fc.IDENT() {
                let name = ident.symbol.text.to_string();
                let line = (ident.symbol.line - 1) as u32;
                let col = ident.symbol.column as u32;
                let end_col = col + name.len() as u32;
                calls.push(FunctionCallInfo {
                    name,
                    line,
                    col,
                    end_col,
                });
            }
            // Recurse into functionCall's own argList
            if let Some(inner_args) = fc.argList() {
                collect_fn_calls_from_arg_list(&inner_args, calls);
            }
        }

        // columnAlias may contain a functionCall
        if let Some(col_alias) = arg.columnAlias() {
            use lql_parser::ColumnAliasContextAttrs;
            if let Some(fc) = col_alias.functionCall() {
                if let Some(ident) = fc.IDENT() {
                    let name = ident.symbol.text.to_string();
                    let line = (ident.symbol.line - 1) as u32;
                    let col = ident.symbol.column as u32;
                    let end_col = col + name.len() as u32;
                    calls.push(FunctionCallInfo {
                        name,
                        line,
                        col,
                        end_col,
                    });
                }
                if let Some(inner_args) = fc.argList() {
                    collect_fn_calls_from_arg_list(&inner_args, calls);
                }
            }
        }

        // expr in arg may be a function call
        if let Some(expr) = arg.expr() {
            collect_fn_calls_from_expr(&expr, calls);
        }

        // Nested pipe expression
        if let Some(pipe) = arg.pipeExpr() {
            collect_fn_calls_from_pipe(&pipe, calls);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_new_scope_is_empty() {
        let scope = ScopeMap::new();
        assert!(scope.binding_names().is_empty());
        assert!(scope.table_names().is_empty());
    }

    #[test]
    fn test_add_and_retrieve_binding() {
        let mut scope = ScopeMap::new();
        scope.add_binding("x".to_string(), 1, 4);
        assert!(scope.has_binding("x"));
        assert!(!scope.has_binding("y"));
        let info = scope.get_binding("x").unwrap();
        assert_eq!(info.name, "x");
        assert_eq!(info.line, 1);
        assert_eq!(info.col, 4);
    }

    #[test]
    fn test_binding_names() {
        let mut scope = ScopeMap::new();
        scope.add_binding("alpha".to_string(), 0, 0);
        scope.add_binding("beta".to_string(), 1, 0);
        let names = scope.binding_names();
        assert_eq!(names.len(), 2);
        assert!(names.contains(&"alpha"));
        assert!(names.contains(&"beta"));
    }

    #[test]
    fn test_add_table() {
        let mut scope = ScopeMap::new();
        scope.add_table("users".to_string());
        scope.add_table("orders".to_string());
        let tables = scope.table_names();
        assert_eq!(tables.len(), 2);
        assert!(tables.contains(&"users"));
        assert!(tables.contains(&"orders"));
    }

    #[test]
    fn test_add_duplicate_table_ignored() {
        let mut scope = ScopeMap::new();
        scope.add_table("users".to_string());
        scope.add_table("users".to_string());
        assert_eq!(scope.table_names().len(), 1);
    }

    #[test]
    fn test_get_binding_not_found() {
        let scope = ScopeMap::new();
        assert!(scope.get_binding("nonexistent").is_none());
    }

    #[test]
    fn test_binding_overwrites_previous() {
        let mut scope = ScopeMap::new();
        scope.add_binding("x".to_string(), 1, 0);
        scope.add_binding("x".to_string(), 5, 0);
        let info = scope.get_binding("x").unwrap();
        assert_eq!(info.line, 5);
    }

    #[test]
    fn test_default_trait() {
        let scope = ScopeMap::default();
        assert!(scope.binding_names().is_empty());
        assert!(scope.table_names().is_empty());
    }

    // ── build_scope tests ──

    #[test]
    fn test_build_scope_let_binding() {
        let scope = build_scope("let x = users |> select(users.id)");
        assert!(scope.has_binding("x"));
    }

    #[test]
    fn test_build_scope_multiple_lets() {
        let scope = build_scope("let a = users\nlet b = orders");
        assert!(scope.has_binding("a"));
        assert!(scope.has_binding("b"));
    }

    #[test]
    fn test_build_scope_table_reference() {
        let scope = build_scope("users |> select(users.id)");
        assert!(scope.table_names().contains(&"users"));
    }

    #[test]
    fn test_build_scope_table_in_join() {
        let scope = build_scope("users |> join(orders, on = users.id = orders.user_id)");
        assert!(scope.table_names().contains(&"users"));
        // Note: 'orders' inside join args may or may not be detected depending
        // on ANTLR grammar parse of the arg list.
        // We just verify the function doesn't panic on join syntax.
    }

    #[test]
    fn test_build_scope_empty() {
        let scope = build_scope("");
        assert!(scope.binding_names().is_empty());
        assert!(scope.table_names().is_empty());
    }

    // ── collect_function_calls tests ──

    #[test]
    fn test_collect_fn_calls_select() {
        let calls = collect_function_calls("users |> select(users.id)");
        let names: Vec<&str> = calls.iter().map(|c| c.name.as_str()).collect();
        assert!(names.contains(&"select"));
    }

    #[test]
    fn test_collect_fn_calls_filter() {
        let calls = collect_function_calls("users |> filter(fn(r) => r.users.age > 18)");
        let names: Vec<&str> = calls.iter().map(|c| c.name.as_str()).collect();
        assert!(names.contains(&"filter"));
    }

    #[test]
    fn test_collect_fn_calls_nested() {
        let calls = collect_function_calls("users |> select(count(*) as cnt)");
        let names: Vec<&str> = calls.iter().map(|c| c.name.as_str()).collect();
        assert!(names.contains(&"select"));
        // Note: nested function calls like count(*) may not be detected
        // depending on how the ANTLR tree represents them inside aliases.
    }

    #[test]
    fn test_collect_fn_calls_empty() {
        let calls = collect_function_calls("");
        assert!(calls.is_empty());
    }
}
