use lql_analyzer::{ColumnInfo, SchemaCache, TableInfo};
use std::collections::HashMap;

/// Discover a connection string from environment variables.
/// Priority: LQL_CONNECTION_STRING > DATABASE_URL
pub fn discover_connection_string() -> Option<String> {
    std::env::var("LQL_CONNECTION_STRING")
        .ok()
        .filter(|s| !s.is_empty())
        .or_else(|| {
            std::env::var("DATABASE_URL")
                .ok()
                .filter(|s| !s.is_empty())
        })
}

/// Convert Npgsql-style connection string to libpq key=value format.
/// "Host=localhost;Database=mydb;Username=user;Password=pass"
/// becomes "host=localhost dbname=mydb user=user password=pass"
pub fn normalize_connection_string(input: &str) -> String {
    if input.starts_with("postgres://") || input.starts_with("postgresql://") {
        return input.to_string();
    }
    if !input.contains(';') {
        return input.to_string();
    }

    let params: HashMap<String, String> = input
        .split(';')
        .filter_map(|part| {
            let part = part.trim();
            if part.is_empty() {
                return None;
            }
            part.split_once('=')
                .map(|(k, v)| (k.trim().to_lowercase(), v.trim().to_string()))
        })
        .collect();

    let mut parts = Vec::new();
    if let Some(h) = params.get("host").or(params.get("server")) {
        parts.push(format!("host={h}"));
    }
    if let Some(p) = params.get("port") {
        parts.push(format!("port={p}"));
    }
    if let Some(d) = params.get("database").or(params.get("initial catalog")) {
        parts.push(format!("dbname={d}"));
    }
    if let Some(u) = params
        .get("username")
        .or(params.get("user id"))
        .or(params.get("uid"))
    {
        parts.push(format!("user={u}"));
    }
    if let Some(p) = params.get("password").or(params.get("pwd")) {
        parts.push(format!("password={p}"));
    }

    parts.join(" ")
}

/// Fetch full database schema from PostgreSQL via information_schema.
pub async fn fetch_schema(connection_string: &str) -> std::result::Result<SchemaCache, String> {
    let conn_str = normalize_connection_string(connection_string);
    let (client, connection) = tokio_postgres::connect(&conn_str, tokio_postgres::NoTls)
        .await
        .map_err(|e| format!("DB connect failed: {e}"))?;

    tokio::spawn(async move {
        if let Err(e) = connection.await {
            eprintln!("DB connection error: {e}");
        }
    });

    let col_rows = client
        .query(
            "SELECT c.table_schema, c.table_name, c.column_name, c.data_type, \
                    c.is_nullable, \
                    CASE WHEN pk.column_name IS NOT NULL THEN 'YES' ELSE 'NO' END as is_pk \
             FROM information_schema.columns c \
             LEFT JOIN ( \
                 SELECT kcu.table_schema, kcu.table_name, kcu.column_name \
                 FROM information_schema.key_column_usage kcu \
                 JOIN information_schema.table_constraints tc \
                   ON kcu.constraint_name = tc.constraint_name \
                   AND kcu.table_schema = tc.table_schema \
                 WHERE tc.constraint_type = 'PRIMARY KEY' \
             ) pk ON c.table_schema = pk.table_schema \
                  AND c.table_name = pk.table_name \
                  AND c.column_name = pk.column_name \
             WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema') \
             ORDER BY c.table_schema, c.table_name, c.ordinal_position",
            &[],
        )
        .await
        .map_err(|e| format!("Failed to query columns: {e}"))?;

    let mut tables_map: HashMap<(String, String), Vec<ColumnInfo>> = HashMap::new();
    for row in &col_rows {
        let schema: String = row.get(0);
        let table: String = row.get(1);
        tables_map
            .entry((schema, table))
            .or_default()
            .push(ColumnInfo {
                name: row.get(2),
                sql_type: row.get(3),
                is_nullable: row.get::<_, String>(4) == "YES",
                is_primary_key: row.get::<_, String>(5) == "YES",
            });
    }

    let tables: Vec<TableInfo> = tables_map
        .into_iter()
        .map(|((schema, name), columns)| TableInfo {
            name,
            schema,
            columns,
        })
        .collect();

    Ok(SchemaCache::from_tables(tables))
}
