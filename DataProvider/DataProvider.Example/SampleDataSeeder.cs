using System.Data;
using Generated;
using Results;

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
    public static async Task<(bool flowControl, Result<string, SqlError> value)> SeedDataAsync(
        IDbTransaction transaction
    )
    {
        // Insert Customers
        var customer1Result = await transaction
            .InsertCustomerAsync("Acme Corp", "contact@acme.com", "555-0100", "2024-01-01")
            .ConfigureAwait(false);
        if (customer1Result is not Result<long, SqlError>.Success customer1Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (customer1Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        var customer2Result = await transaction
            .InsertCustomerAsync(
                "Tech Solutions",
                "info@techsolutions.com",
                "555-0200",
                "2024-01-02"
            )
            .ConfigureAwait(false);
        if (customer2Result is not Result<long, SqlError>.Success customer2Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (customer2Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        // Insert Invoice
        var invoiceResult = await transaction
            .InsertInvoiceAsync(
                "INV-001",
                "2024-01-15",
                "Acme Corp",
                "billing@acme.com",
                1250.00,
                50.00,
                "Sample invoice"
            )
            .ConfigureAwait(false);
        if (invoiceResult is not Result<long, SqlError>.Success invoiceSuccess)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (invoiceResult as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        // Insert InvoiceLines
        var invoiceLine1Result = await transaction
            .InsertInvoiceLineAsync(
                invoiceSuccess.Value,
                "Software License",
                1.0,
                1000.00,
                1000.00,
                null,
                null
            )
            .ConfigureAwait(false);
        if (invoiceLine1Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (invoiceLine1Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        var invoiceLine2Result = await transaction
            .InsertInvoiceLineAsync(
                invoiceSuccess.Value,
                "Support Package",
                1.0,
                250.00,
                250.00,
                null,
                "First year"
            )
            .ConfigureAwait(false);
        if (invoiceLine2Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (invoiceLine2Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        // Insert Addresses
        var address1Result = await transaction
            .InsertAddressAsync(
                customer1Success.Value,
                "123 Business Ave",
                "New York",
                "NY",
                "10001",
                "USA"
            )
            .ConfigureAwait(false);
        if (address1Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (address1Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        var address2Result = await transaction
            .InsertAddressAsync(
                customer1Success.Value,
                "456 Main St",
                "Albany",
                "NY",
                "12201",
                "USA"
            )
            .ConfigureAwait(false);
        if (address2Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (address2Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        var address3Result = await transaction
            .InsertAddressAsync(
                customer2Success.Value,
                "789 Tech Blvd",
                "San Francisco",
                "CA",
                "94105",
                "USA"
            )
            .ConfigureAwait(false);
        if (address3Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (address3Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        // Insert Orders
        var order1Result = await transaction
            .InsertOrdersAsync("ORD-001", "2024-01-10", customer1Success.Value, 500.00, "Completed")
            .ConfigureAwait(false);
        if (order1Result is not Result<long, SqlError>.Success order1Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (order1Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        var order2Result = await transaction
            .InsertOrdersAsync(
                "ORD-002",
                "2024-01-11",
                customer2Success.Value,
                750.00,
                "Processing"
            )
            .ConfigureAwait(false);
        if (order2Result is not Result<long, SqlError>.Success order2Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (order2Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        // Insert OrderItems
        var orderItem1Result = await transaction
            .InsertOrderItemAsync(order1Success.Value, "Widget A", 2.0, 100.00, 200.00)
            .ConfigureAwait(false);
        if (orderItem1Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (orderItem1Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        var orderItem2Result = await transaction
            .InsertOrderItemAsync(order1Success.Value, "Widget B", 3.0, 100.00, 300.00)
            .ConfigureAwait(false);
        if (orderItem2Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (orderItem2Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        var orderItem3Result = await transaction
            .InsertOrderItemAsync(order2Success.Value, "Service Package", 1.0, 750.00, 750.00)
            .ConfigureAwait(false);
        if (orderItem3Result is not Result<long, SqlError>.Success)
            return (
                flowControl: false,
                value: new Result<string, SqlError>.Failure(
                    (orderItem3Result as Result<long, SqlError>.Failure)!.ErrorValue
                )
            );

        return (
            flowControl: true,
            value: new Result<string, SqlError>.Success("Sample data seeded successfully")
        );
    }
}
