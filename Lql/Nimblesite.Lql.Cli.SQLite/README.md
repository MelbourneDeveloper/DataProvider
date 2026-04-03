# LQL to SQLite CLI Transpiler

A command-line tool that transpiles LQL (Language Query Language) files to SQLite SQL.

## Installation

Build the project:

```bash
dotnet build LqlCli.SQLite/LqlCli.SQLite.csproj
```

## Usage

### Basic Usage

Transpile an LQL file to SQLite SQL and print to console:

```bash
dotnet run --project LqlCli.SQLite/LqlCli.SQLite.csproj -- --input input.lql
```

### Output to File

Transpile and save to a file:

```bash
dotnet run --project LqlCli.SQLite/LqlCli.SQLite.csproj -- --input input.lql --output output.sql
```

### Validate Syntax Only

Check if the LQL syntax is valid without generating output:

```bash
dotnet run --project LqlCli.SQLite/LqlCli.SQLite.csproj -- --input input.lql --validate
```

## Options

- `-i, --input <file>` (REQUIRED): Input LQL file to transpile
- `-o, --output <file>`: Output SQLite SQL file (optional - prints to console if not specified)
- `-v, --validate`: Validate the LQL syntax without generating output
- `--help`: Show help and usage information

## Examples

### Simple Select

Input LQL:
```
users |> select(users.id, users.name, users.email)
```

Output SQLite SQL:
```sql
SELECT users.id, users.name, users.email FROM users
```

### With Filtering

Input LQL:
```
employees
|> select(employees.id, employees.name, employees.salary)
|> filter(fn(row) => row.employees.salary > 50000 and row.employees.salary < 100000)
```

Output SQLite SQL:
```sql
SELECT employees.id, employees.name, employees.salary FROM employees WHERE employees.salary > 50000 AND employees.salary < 100000
```

## Error Handling

The tool will display detailed error messages for:
- File not found errors
- LQL parsing errors
- SQL generation errors

Exit codes:
- `0`: Success
- `1`: Error occurred

## Building Native Binary

To create a native AOT binary:

```bash
dotnet publish LqlCli.SQLite/LqlCli.SQLite.csproj -c Release -r win-x64 --self-contained
```

Replace `win-x64` with your target runtime identifier (`linux-x64`, `osx-x64`, etc.).