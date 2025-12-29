global using Generated;
global using Selecta;
// Type aliases for Result types to reduce verbosity in DataProvider.Example
global using BasicOrderListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<DataProvider.Example.Model.BasicOrder>,
    Selecta.SqlError
>.Error<
    System.Collections.Generic.IReadOnlyList<DataProvider.Example.Model.BasicOrder>,
    Selecta.SqlError
>;
global using BasicOrderListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<DataProvider.Example.Model.BasicOrder>,
    Selecta.SqlError
>.Ok<
    System.Collections.Generic.IReadOnlyList<DataProvider.Example.Model.BasicOrder>,
    Selecta.SqlError
>;
global using CustomerListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.Customer>, Selecta.SqlError>;
global using CustomerListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Customer>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Customer>, Selecta.SqlError>;
global using CustomerReadOnlyListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Customer>,
    Selecta.SqlError
>.Error<System.Collections.Generic.IReadOnlyList<Generated.Customer>, Selecta.SqlError>;
global using CustomerReadOnlyListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Customer>,
    Selecta.SqlError
>.Ok<System.Collections.Generic.IReadOnlyList<Generated.Customer>, Selecta.SqlError>;
global using InvoiceListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.Invoice>, Selecta.SqlError>;
global using InvoiceListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Invoice>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Invoice>, Selecta.SqlError>;
global using IntSqlError = Outcome.Result<int, Selecta.SqlError>.Error<int, Selecta.SqlError>;
global using IntSqlOk = Outcome.Result<int, Selecta.SqlError>.Ok<int, Selecta.SqlError>;
global using LongSqlError = Outcome.Result<long, Selecta.SqlError>.Error<long, Selecta.SqlError>;
global using LongSqlOk = Outcome.Result<long, Selecta.SqlError>.Ok<long, Selecta.SqlError>;
global using OrderListError = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Order>,
    Selecta.SqlError
>.Error<System.Collections.Immutable.ImmutableList<Generated.Order>, Selecta.SqlError>;
global using OrderListOk = Outcome.Result<
    System.Collections.Immutable.ImmutableList<Generated.Order>,
    Selecta.SqlError
>.Ok<System.Collections.Immutable.ImmutableList<Generated.Order>, Selecta.SqlError>;
global using OrderReadOnlyListError = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Order>,
    Selecta.SqlError
>.Error<System.Collections.Generic.IReadOnlyList<Generated.Order>, Selecta.SqlError>;
global using OrderReadOnlyListOk = Outcome.Result<
    System.Collections.Generic.IReadOnlyList<Generated.Order>,
    Selecta.SqlError
>.Ok<System.Collections.Generic.IReadOnlyList<Generated.Order>, Selecta.SqlError>;
global using StringSqlError = Outcome.Result<string, Selecta.SqlError>.Error<
    string,
    Selecta.SqlError
>;
global using StringSqlOk = Outcome.Result<string, Selecta.SqlError>.Ok<string, Selecta.SqlError>;
global using StringSqlResult = Outcome.Result<string, Selecta.SqlError>;
