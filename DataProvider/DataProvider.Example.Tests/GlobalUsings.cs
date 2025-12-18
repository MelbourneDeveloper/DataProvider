#pragma warning disable IDE0005 // Using directive is unnecessary - these are used for pattern matching

// Type aliases for Result types to reduce verbosity in DataProvider.Example.Tests
global using CustomerListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.Customer>, Selecta.SqlError>;
global using CustomerListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Customer>, Selecta.SqlError>;
global using CustomerListResult = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Selecta.SqlError
>;
global using CustomerReadOnlyListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Customer>,
    Selecta.SqlError
>.Error<System.Collections.Generic.IReadOnlyList<Generated.Customer>, Selecta.SqlError>;
global using CustomerReadOnlyListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Customer>,
    Selecta.SqlError
>.Ok<System.Collections.Generic.IReadOnlyList<Generated.Customer>, Selecta.SqlError>;
global using CustomerReadOnlyListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Customer>,
    Selecta.SqlError
>;
global using InvoiceListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.Invoice>, Selecta.SqlError>;
global using InvoiceListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Invoice>, Selecta.SqlError>;
global using InvoiceListResult = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Selecta.SqlError
>;
global using OrderListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Order>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.Order>, Selecta.SqlError>;
global using OrderListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Order>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Order>, Selecta.SqlError>;
global using OrderListResult = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Order>,
    Selecta.SqlError
>;
global using OrderReadOnlyListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Order>,
    Selecta.SqlError
>.Error<System.Collections.Generic.IReadOnlyList<Generated.Order>, Selecta.SqlError>;
global using OrderReadOnlyListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Order>,
    Selecta.SqlError
>.Ok<System.Collections.Generic.IReadOnlyList<Generated.Order>, Selecta.SqlError>;
global using OrderReadOnlyListResult = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Order>,
    Selecta.SqlError
>;
global using StringSqlError = Outcome.Result<string, Selecta.SqlError>.Error<
    string,
    Selecta.SqlError
>;
global using StringSqlOk = Outcome.Result<string, Selecta.SqlError>.Ok<string, Selecta.SqlError>;
global using StringSqlResult = Outcome.Result<string, Selecta.SqlError>;
