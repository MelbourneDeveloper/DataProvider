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
}
