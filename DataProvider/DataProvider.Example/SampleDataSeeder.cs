using System.Data;

namespace DataProvider.Example;

/// <summary>
/// Seeds the database with sample data for testing and demonstration purposes
/// </summary>
internal static class SampleDataSeeder
{
    /// <summary>
    /// Inserts comprehensive sample data into all tables using generated methods
    /// </summary>
    /// <param name="transaction">The database transaction to insert data within</param>
    /// <returns>A result indicating success or failure with details</returns>
    public static async Task<(bool flowControl, StringSqlResult value)> SeedDataAsync(
        IDbTransaction transaction
    )
    {
        // Insert Customers
        var customer1Id = Guid.NewGuid().ToString();
        var customer1Result = await transaction
            .InsertCustomerAsync(
                customer1Id,
                "Acme Corp",
                "contact@acme.com",
                "555-0100",
                "2024-01-01"
            )
            .ConfigureAwait(false);
        if (customer1Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((customer1Result as IntSqlError)!.Value)
            );

        var customer2Id = Guid.NewGuid().ToString();
        var customer2Result = await transaction
            .InsertCustomerAsync(
                customer2Id,
                "Tech Solutions",
                "info@techsolutions.com",
                "555-0200",
                "2024-01-02"
            )
            .ConfigureAwait(false);
        if (customer2Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((customer2Result as IntSqlError)!.Value)
            );

        // Insert Invoice
        var invoiceId = Guid.NewGuid().ToString();
        var invoiceResult = await transaction
            .InsertInvoiceAsync(
                invoiceId,
                "INV-001",
                "2024-01-15",
                "Acme Corp",
                "billing@acme.com",
                1250.00,
                50.00,
                "Sample invoice"
            )
            .ConfigureAwait(false);
        if (invoiceResult is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((invoiceResult as IntSqlError)!.Value)
            );

        // Insert InvoiceLines
        var invoiceLine1Result = await transaction
            .InsertInvoiceLineAsync(
                Guid.NewGuid().ToString(),
                invoiceId,
                "Software License",
                1,
                1000.00,
                1000.00,
                null,
                null
            )
            .ConfigureAwait(false);
        if (invoiceLine1Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((invoiceLine1Result as IntSqlError)!.Value)
            );

        var invoiceLine2Result = await transaction
            .InsertInvoiceLineAsync(
                Guid.NewGuid().ToString(),
                invoiceId,
                "Support Package",
                1,
                250.00,
                250.00,
                null,
                "First year"
            )
            .ConfigureAwait(false);
        if (invoiceLine2Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((invoiceLine2Result as IntSqlError)!.Value)
            );

        // Insert Addresses
        var address1Result = await transaction
            .InsertAddressAsync(
                Guid.NewGuid().ToString(),
                customer1Id,
                "123 Business Ave",
                "New York",
                "NY",
                "10001",
                "USA"
            )
            .ConfigureAwait(false);
        if (address1Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((address1Result as IntSqlError)!.Value)
            );

        var address2Result = await transaction
            .InsertAddressAsync(
                Guid.NewGuid().ToString(),
                customer1Id,
                "456 Main St",
                "Albany",
                "NY",
                "12201",
                "USA"
            )
            .ConfigureAwait(false);
        if (address2Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((address2Result as IntSqlError)!.Value)
            );

        var address3Result = await transaction
            .InsertAddressAsync(
                Guid.NewGuid().ToString(),
                customer2Id,
                "789 Tech Blvd",
                "San Francisco",
                "CA",
                "94105",
                "USA"
            )
            .ConfigureAwait(false);
        if (address3Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((address3Result as IntSqlError)!.Value)
            );

        // Insert Orders
        var order1Id = Guid.NewGuid().ToString();
        var order1Result = await transaction
            .InsertOrdersAsync(order1Id, "ORD-001", "2024-01-10", customer1Id, 500.00, "Completed")
            .ConfigureAwait(false);
        if (order1Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((order1Result as IntSqlError)!.Value)
            );

        var order2Id = Guid.NewGuid().ToString();
        var order2Result = await transaction
            .InsertOrdersAsync(order2Id, "ORD-002", "2024-01-11", customer2Id, 750.00, "Processing")
            .ConfigureAwait(false);
        if (order2Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((order2Result as IntSqlError)!.Value)
            );

        // Insert OrderItems
        var orderItem1Result = await transaction
            .InsertOrderItemAsync(
                Guid.NewGuid().ToString(),
                order1Id,
                "Widget A",
                2,
                100.00,
                200.00
            )
            .ConfigureAwait(false);
        if (orderItem1Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((orderItem1Result as IntSqlError)!.Value)
            );

        var orderItem2Result = await transaction
            .InsertOrderItemAsync(
                Guid.NewGuid().ToString(),
                order1Id,
                "Widget B",
                3,
                100.00,
                300.00
            )
            .ConfigureAwait(false);
        if (orderItem2Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((orderItem2Result as IntSqlError)!.Value)
            );

        var orderItem3Result = await transaction
            .InsertOrderItemAsync(
                Guid.NewGuid().ToString(),
                order2Id,
                "Service Package",
                1,
                750.00,
                750.00
            )
            .ConfigureAwait(false);
        if (orderItem3Result is not IntSqlOk)
            return (
                flowControl: false,
                value: new StringSqlError((orderItem3Result as IntSqlError)!.Value)
            );

        return (flowControl: true, value: new StringSqlOk("Sample data seeded successfully"));
    }
}
