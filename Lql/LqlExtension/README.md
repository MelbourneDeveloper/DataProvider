# Lambda Query Language (LQL) VS Code Extension

A comprehensive VS Code extension providing language support for Lambda Query Language (LQL) with syntax highlighting, IntelliSense, error checking, and more.

## Features

### 🎨 Syntax Highlighting
- Full syntax highlighting for LQL files (`.lql`)
- Custom dark theme optimized for LQL
- Color-coded keywords, operators, functions, and data types

### 🧠 IntelliSense & Auto-completion
- Smart auto-completion for LQL keywords and functions
- Context-aware suggestions
- Function signatures and documentation on hover

### 🔍 Error Detection & Validation
- Real-time syntax error detection
- Pipeline operator spacing validation
- Bracket matching and validation
- Unknown function detection

### 📝 Code Formatting
- Automatic code formatting with proper indentation
- Pipeline operator alignment
- Bracket and parentheses formatting

### 🛠️ Additional Features
- Code snippets for common LQL patterns
- Command palette integration
- SQL preview for compiled LQL code
- Language server protocol (LSP) support

## Installation

### From VS Code Marketplace
1. Open VS Code
2. Go to Extensions (Ctrl+Shift+X)
3. Search for "Lambda Query Language"
4. Click Install

### Manual Installation
1. Clone this repository
2. Run `npm install` in the extension directory
3. Run `npm run compile` to build the extension
4. Press F5 to launch a new VS Code window with the extension loaded

## Usage

### File Extensions
The extension automatically activates for files with the following extensions:
- `.lql` - Lambda Query Language files
- `.lql` - Lambda Query Language files

### Commands
Access these commands via the Command Palette (Ctrl+Shift+P):

- **LQL: Format Document** - Format the current LQL document
- **LQL: Validate Document** - Validate the current LQL document
- **LQL: Show Compiled SQL** - Show the compiled SQL for the current LQL code

### Code Snippets
Type these prefixes and press Tab to insert code snippets:

- `select` - Basic select statement
- `selectf` - Select with filter
- `join` - Join two tables
- `groupby` - Group by with aggregation
- `orderby` - Order by clause
- `let` - Let binding
- `insert` - Insert statement
- `update` - Update statement
- `union` - Union query
- `case` - Case expression
- `lambda` - Lambda function

## Configuration

Configure the extension through VS Code settings:

```json
{
    "lql.languageServer.enabled": true,
    "lql.languageServer.trace": "off",
    "lql.validation.enabled": true,
    "lql.formatting.enabled": true
}
```

### Settings

- `lql.languageServer.enabled` - Enable/disable the LQL language server
- `lql.languageServer.trace` - Set language server trace level (off, messages, verbose)
- `lql.validation.enabled` - Enable/disable LQL validation
- `lql.formatting.enabled` - Enable/disable LQL code formatting

## Language Features

### Supported LQL Syntax

#### Query Operations
- `select` - Project columns
- `filter` - Filter rows
- `join` - Join tables
- `group_by` - Group rows
- `order_by` - Order rows
- `having` - Filter groups
- `limit` - Limit results
- `offset` - Skip rows
- `union` - Union queries

#### Aggregate Functions
- `count` - Count rows
- `sum` - Sum values
- `avg` - Average values
- `max` - Maximum value
- `min` - Minimum value

#### String Functions
- `concat` - Concatenate strings
- `substring` - Extract substring
- `length` - String length
- `trim` - Trim whitespace
- `upper` - Convert to uppercase
- `lower` - Convert to lowercase

#### Math Functions
- `round` - Round number
- `floor` - Floor function
- `ceil` - Ceiling function
- `abs` - Absolute value
- `sqrt` - Square root

#### Pipeline Operator
- `|>` - Pipeline data flow

#### Lambda Functions
- `fn param => expression` - Lambda function syntax
- `let variable = value in expression` - Variable binding

## Example LQL Code

```lql
-- Simple select with filter
users
|> filter (age > 18)
|> select name, email, age

-- Join with aggregation
let adult_users = users |> filter (age >= 18) in
orders
|> join adult_users on orders.user_id = adult_users.id
|> group_by adult_users.name
|> select adult_users.name, count(*) as order_count, sum(orders.total) as total_spent
|> order_by total_spent desc

-- Complex query with arithmetic
products
|> select 
    name,
    price,
    price * 0.1 as tax,
    price + (price * 0.1) as total_price
|> filter (total_price > 100)
|> order_by total_price desc
```

## Development

### Building the Extension

```bash
# Install dependencies
npm install

# Compile TypeScript
npm run compile

# Watch for changes
npm run watch

# Package extension
npm run package
```

### Language Server

The extension includes a Language Server Protocol (LSP) implementation:

```bash
# Build the language server
cd server
npm install
npm run compile
```

### Testing

```bash
# Run tests
npm test

# Run linting
npm run lint
```

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests if applicable
5. Submit a pull request

## F# Type Provider

For F# projects, LQL also offers compile-time validation through a type provider. Invalid LQL queries cause compilation errors rather than runtime failures.

```fsharp
open Lql

// Validated at compile time
type GetUsers = LqlCommand<"Users |> select(Users.Id, Users.Name)">
type FilteredUsers = LqlCommand<"Users |> filter(fn(row) => row.Users.Age > 18) |> select(*)">

// Access generated SQL
let sql = GetUsers.Sql  // SQL string generated at compile time
```

Install the type provider:

```xml
<PackageReference Include="Lql.TypeProvider.FSharp" Version="*" />
```

See the [LQL documentation](../README.md) for full type provider details.

## License

MIT License - see LICENSE file for details.

## Support

For issues and feature requests, please visit our [GitHub repository](https://github.com/your-org/lambda-query-language).