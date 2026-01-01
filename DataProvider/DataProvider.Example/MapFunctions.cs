using System.Data;
using DataProvider.Example.Model;

namespace DataProvider.Example;

/// <summary>
/// Static mapping functions for converting IDataReader to domain objects
/// TODO: cache the column ordinals
/// </summary>
public static class MapFunctions
{
    /// <summary>
    /// Maps an IDataReader to Order record
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <returns>Order instance</returns>
    public static Generated.Order MapOrder(IDataReader reader) =>
        new(
            Id: reader.GetString(reader.GetOrdinal("Id")),
            OrderNumber: reader.GetString(reader.GetOrdinal("OrderNumber")),
            OrderDate: reader.GetString(reader.GetOrdinal("OrderDate")),
            CustomerId: reader.GetString(reader.GetOrdinal("CustomerId")),
            TotalAmount: reader.GetDouble(reader.GetOrdinal("TotalAmount")),
            Status: reader.GetString(reader.GetOrdinal("Status")),
            OrderItems: []
        );

    /// <summary>
    /// Maps an IDataReader to Customer record
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <returns>Customer instance</returns>
    public static Generated.Customer MapCustomer(IDataReader reader) =>
        new(
            Id: reader.GetString(reader.GetOrdinal("Id")),
            CustomerName: reader.GetString(reader.GetOrdinal("CustomerName")),
            Email: reader.IsDBNull(reader.GetOrdinal("Email"))
                ? null
                : reader.GetString(reader.GetOrdinal("Email")),
            Phone: reader.IsDBNull(reader.GetOrdinal("Phone"))
                ? null
                : reader.GetString(reader.GetOrdinal("Phone")),
            CreatedDate: reader.GetString(reader.GetOrdinal("CreatedDate")),
            Addresss: []
        );

    /// <summary>
    /// Maps an IDataReader to BasicOrder record
    /// </summary>
    /// <param name="reader">The data reader</param>
    /// <returns>BasicOrder instance</returns>
    public static BasicOrder MapBasicOrder(IDataReader reader) =>
        new(
            reader.GetString(reader.GetOrdinal("OrderNumber")),
            reader.GetDouble(reader.GetOrdinal("TotalAmount")),
            reader.GetString(reader.GetOrdinal("Status")),
            reader.GetString(reader.GetOrdinal("CustomerName")),
            reader.GetString(reader.GetOrdinal("Email"))
        );
}
