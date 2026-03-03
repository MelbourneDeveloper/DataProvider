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
        self.bindings.insert(
            name.clone(),
            BindingInfo { name, line, col },
        );
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
