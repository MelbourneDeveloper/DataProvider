use std::collections::HashMap;
use std::sync::Arc;
use std::time::Instant;

/// Cached database schema — tables, columns, and types.
/// Thread-safe and designed for concurrent read access.
#[derive(Debug, Clone)]
pub struct SchemaCache {
    tables: Arc<HashMap<String, TableInfo>>,
    last_refreshed: Option<Instant>,
}

/// Schema information for a single database table.
#[derive(Debug, Clone)]
pub struct TableInfo {
    /// Table name as it appears in the database.
    pub name: String,
    /// Database schema (e.g., "public" for Postgres).
    pub schema: String,
    /// Columns in this table.
    pub columns: Vec<ColumnInfo>,
}

/// Schema information for a single column.
#[derive(Debug, Clone)]
pub struct ColumnInfo {
    /// Column name.
    pub name: String,
    /// SQL type as reported by the database (e.g., "uuid", "text", "integer").
    pub sql_type: String,
    /// Whether this column accepts NULL values.
    pub is_nullable: bool,
    /// Whether this column is (part of) the primary key.
    pub is_primary_key: bool,
}

impl Default for SchemaCache {
    fn default() -> Self {
        Self {
            tables: Arc::new(HashMap::new()),
            last_refreshed: None,
        }
    }
}

impl SchemaCache {
    /// Build a new schema cache from a list of tables.
    pub fn from_tables(tables: Vec<TableInfo>) -> Self {
        let map: HashMap<String, TableInfo> = tables
            .into_iter()
            .map(|t| (t.name.to_lowercase(), t))
            .collect();
        Self {
            tables: Arc::new(map),
            last_refreshed: Some(Instant::now()),
        }
    }

    /// Check whether the cache has any schema data.
    pub fn is_empty(&self) -> bool {
        self.tables.is_empty()
    }

    /// Number of cached tables.
    pub fn table_count(&self) -> usize {
        self.tables.len()
    }

    /// Get all table names (original case).
    pub fn table_names(&self) -> Vec<&str> {
        self.tables.values().map(|t| t.name.as_str()).collect()
    }

    /// Look up a table by name (case-insensitive).
    pub fn get_table(&self, name: &str) -> Option<&TableInfo> {
        self.tables.get(&name.to_lowercase())
    }

    /// Get columns for a table (case-insensitive lookup).
    pub fn get_columns(&self, table_name: &str) -> Vec<&ColumnInfo> {
        match self.tables.get(&table_name.to_lowercase()) {
            Some(table) => table.columns.iter().collect(),
            None => Vec::new(),
        }
    }

    /// Get the age of the cache, or None if never refreshed.
    pub fn age(&self) -> Option<std::time::Duration> {
        self.last_refreshed.map(|t| t.elapsed())
    }

    /// Whether the cache is stale (older than the given duration).
    pub fn is_stale(&self, max_age: std::time::Duration) -> bool {
        match self.last_refreshed {
            Some(t) => t.elapsed() > max_age,
            None => true,
        }
    }
}

impl TableInfo {
    /// Get a column by name (case-insensitive).
    pub fn get_column(&self, name: &str) -> Option<&ColumnInfo> {
        let lower = name.to_lowercase();
        self.columns.iter().find(|c| c.name.to_lowercase() == lower)
    }

    /// Get the primary key column(s).
    pub fn primary_key_columns(&self) -> Vec<&ColumnInfo> {
        self.columns.iter().filter(|c| c.is_primary_key).collect()
    }
}

impl ColumnInfo {
    /// Format a human-readable type description for hover.
    pub fn type_description(&self) -> String {
        let mut desc = self.sql_type.clone();
        if self.is_primary_key {
            desc.push_str(" (PK)");
        }
        if !self.is_nullable {
            desc.push_str(" NOT NULL");
        }
        desc
    }
}
