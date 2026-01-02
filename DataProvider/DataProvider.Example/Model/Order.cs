using System.Collections.Immutable;

namespace DataProvider.Example.Model;

/// <summary>
/// Represents an order with its items
/// </summary>
/// <param name="Id">Order identifier</param>
/// <param name="OrderNumber">Order number</param>
/// <param name="OrderDate">Order date</param>
/// <param name="CustomerId">Customer identifier</param>
/// <param name="TotalAmount">Total order amount</param>
/// <param name="Status">Order status</param>
/// <param name="Items">Order items</param>
public sealed record Order(
    long Id,
    string OrderNumber,
    string OrderDate,
    long CustomerId,
    double TotalAmount,
    string Status,
    ImmutableArray<OrderItem> Items
);

/// <summary>
/// Represents an item within an order
/// </summary>
/// <param name="Id">Item identifier</param>
/// <param name="OrderId">Order identifier</param>
/// <param name="ProductName">Product name</param>
/// <param name="Quantity">Quantity ordered</param>
/// <param name="Price">Unit price</param>
/// <param name="Subtotal">Subtotal amount</param>
public sealed record OrderItem(
    long Id,
    long OrderId,
    string ProductName,
    double Quantity,
    double Price,
    double Subtotal
);
