using System.Collections.Immutable;

namespace DataProvider.Example.Model;

/// <summary>
/// Represents a customer with their addresses
/// </summary>
/// <param name="Id">Customer identifier</param>
/// <param name="CustomerName">Customer name</param>
/// <param name="Email">Customer email</param>
/// <param name="Phone">Customer phone</param>
/// <param name="CreatedDate">Creation date</param>
/// <param name="Addresses">Customer addresses</param>
public sealed record Customer(
    long Id,
    string CustomerName,
    string? Email,
    string? Phone,
    string CreatedDate,
    ImmutableArray<Address> Addresses
);

/// <summary>
/// Represents a customer address
/// </summary>
/// <param name="Id">Address identifier</param>
/// <param name="CustomerId">Customer identifier</param>
/// <param name="Street">Street address</param>
/// <param name="City">City</param>
/// <param name="State">State</param>
/// <param name="ZipCode">Zip code</param>
/// <param name="Country">Country</param>
public sealed record Address(
    long Id,
    long CustomerId,
    string Street,
    string City,
    string State,
    string ZipCode,
    string Country
);
