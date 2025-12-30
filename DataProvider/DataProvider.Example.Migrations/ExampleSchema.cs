using Migration;
using static Migration.PortableTypes;

namespace DataProvider.Example.Migrations;

/// <summary>
/// Database schema definition for DataProvider.Example.
/// Uses Migration for portable, database-independent schema definition.
/// </summary>
public static class ExampleSchema
{
    /// <summary>
    /// Gets the complete Example database schema definition.
    /// </summary>
    public static SchemaDefinition Definition { get; } = BuildSchema();

    private static SchemaDefinition BuildSchema() =>
        Schema
            .Define("example")
            .Table(
                "Invoice",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("InvoiceNumber", Text, c => c.NotNull())
                        .Column("InvoiceDate", Text, c => c.NotNull())
                        .Column("CustomerName", Text, c => c.NotNull())
                        .Column("CustomerEmail", Text)
                        .Column("TotalAmount", Float64, c => c.NotNull())
                        .Column("DiscountAmount", Float64)
                        .Column("Notes", Text)
            )
            .Table(
                "InvoiceLine",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("InvoiceId", Text, c => c.NotNull())
                        .Column("Description", Text, c => c.NotNull())
                        .Column("Quantity", Float64, c => c.NotNull())
                        .Column("UnitPrice", Float64, c => c.NotNull())
                        .Column("Amount", Float64, c => c.NotNull())
                        .Column("DiscountPercentage", Float64)
                        .Column("Notes", Text)
                        .ForeignKey("InvoiceId", "Invoice", "Id")
            )
            .Table(
                "Customer",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("CustomerName", Text, c => c.NotNull())
                        .Column("Email", Text)
                        .Column("Phone", Text)
                        .Column("CreatedDate", Text, c => c.NotNull())
            )
            .Table(
                "Address",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("CustomerId", Text, c => c.NotNull())
                        .Column("Street", Text, c => c.NotNull())
                        .Column("City", Text, c => c.NotNull())
                        .Column("State", Text, c => c.NotNull())
                        .Column("ZipCode", Text, c => c.NotNull())
                        .Column("Country", Text, c => c.NotNull())
                        .ForeignKey("CustomerId", "Customer", "Id")
            )
            .Table(
                "Orders",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("OrderNumber", Text, c => c.NotNull())
                        .Column("OrderDate", Text, c => c.NotNull())
                        .Column("CustomerId", Text, c => c.NotNull())
                        .Column("TotalAmount", Float64, c => c.NotNull())
                        .Column("Status", Text, c => c.NotNull())
                        .ForeignKey("CustomerId", "Customer", "Id")
            )
            .Table(
                "OrderItem",
                t =>
                    t.Column("Id", Text, c => c.PrimaryKey())
                        .Column("OrderId", Text, c => c.NotNull())
                        .Column("ProductName", Text, c => c.NotNull())
                        .Column("Quantity", Float64, c => c.NotNull())
                        .Column("Price", Float64, c => c.NotNull())
                        .Column("Subtotal", Float64, c => c.NotNull())
                        .ForeignKey("OrderId", "Orders", "Id")
            )
            .Build();
}
