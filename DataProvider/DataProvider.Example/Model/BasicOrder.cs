#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities

namespace DataProvider.Example.Model;

/// <summary>
/// Basic order information with customer details
/// </summary>
/// <param name="OrderNumber">Order identifier</param>
/// <param name="TotalAmount">Total order amount</param>
/// <param name="Status">Order status</param>
/// <param name="CustomerName">Customer name</param>
/// <param name="Email">Customer email</param>
public sealed record BasicOrder(
    string OrderNumber,
    double TotalAmount,
    string Status,
    string CustomerName,
    string Email
);
