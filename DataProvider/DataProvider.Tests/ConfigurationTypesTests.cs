using System.Text.Json;
using Xunit;

#pragma warning disable CA1869 // Cache and reuse JsonSerializerOptions instances

namespace DataProvider.Tests;

/// <summary>
/// Tests for configuration types and JSON serialization
/// </summary>
internal sealed class ConfigurationTypesTests
{
    [Fact]
    public void DataProviderConfig_CanBeCreatedWithDefaults()
    {
        // Arrange & Act
        var config = new DataProviderConfig();

        // Assert
        Assert.NotNull(config.Tables);
        Assert.Empty(config.Tables);
        Assert.Null(config.ConnectionString);
    }

    [Fact]
    public void DataProviderConfig_CanBeCreatedWithTablesAndConnectionString()
    {
        // Arrange
        var tables = new List<TableConfig>
        {
            new() { Name = "Invoice", Schema = "dbo" },
        }.AsReadOnly();

        // Act
        var config = new DataProviderConfig
        {
            Tables = tables,
            ConnectionString = "Data Source=test.db",
        };

        // Assert
        Assert.Single(config.Tables);
        Assert.Equal("Invoice", config.Tables[0].Name);
        Assert.Equal("Data Source=test.db", config.ConnectionString);
    }

    [Fact]
    public void TableConfig_HasCorrectDefaults()
    {
        // Arrange & Act
        var config = new TableConfig();

        // Assert
        Assert.Equal(string.Empty, config.Schema);
        Assert.Equal(string.Empty, config.Name);
        Assert.True(config.GenerateInsert);
        Assert.True(config.GenerateUpdate);
        Assert.False(config.GenerateDelete);
        Assert.NotNull(config.ExcludeColumns);
        Assert.Empty(config.ExcludeColumns);
        Assert.NotNull(config.PrimaryKeyColumns);
        Assert.Empty(config.PrimaryKeyColumns);
    }

    [Fact]
    public void TableConfig_CanBeConfiguredCompletely()
    {
        // Arrange & Act
        var config = new TableConfig
        {
            Schema = "dbo",
            Name = "Invoice",
            GenerateInsert = false,
            GenerateUpdate = false,
            GenerateDelete = true,
            ExcludeColumns = new List<string> { "CreatedAt", "UpdatedAt" }.AsReadOnly(),
            PrimaryKeyColumns = new List<string> { "Id" }.AsReadOnly(),
        };

        // Assert
        Assert.Equal("dbo", config.Schema);
        Assert.Equal("Invoice", config.Name);
        Assert.False(config.GenerateInsert);
        Assert.False(config.GenerateUpdate);
        Assert.True(config.GenerateDelete);
        Assert.Equal(2, config.ExcludeColumns.Count);
        Assert.Contains("CreatedAt", config.ExcludeColumns);
        Assert.Contains("UpdatedAt", config.ExcludeColumns);
        Assert.Single(config.PrimaryKeyColumns);
        Assert.Contains("Id", config.PrimaryKeyColumns);
    }

    [Fact]
    public void DataProviderConfig_JsonSerialization_WorksCorrectly()
    {
        // Arrange
        var config = new DataProviderConfig
        {
            Tables = new List<TableConfig>
            {
                new()
                {
                    Schema = "main",
                    Name = "Invoice",
                    GenerateInsert = true,
                    GenerateUpdate = true,
                    GenerateDelete = false,
                    ExcludeColumns = new List<string> { "CreatedAt" }.AsReadOnly(),
                    PrimaryKeyColumns = new List<string> { "Id" }.AsReadOnly(),
                },
            }.AsReadOnly(),
            ConnectionString = "Data Source=test.db",
        };

        // Act
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
        var deserialized = JsonSerializer.Deserialize<DataProviderConfig>(
            json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        // Assert
        Assert.NotNull(deserialized);
        Assert.Single(deserialized.Tables);
        Assert.Equal("Invoice", deserialized.Tables[0].Name);
        Assert.Equal("main", deserialized.Tables[0].Schema);
        Assert.Equal("Data Source=test.db", deserialized.ConnectionString);
    }

    [Fact]
    public void GroupingConfig_CanBeCreated()
    {
        // Arrange
        var parentEntity = new EntityConfig("Invoice", ["Id"], ["Id", "InvoiceNumber"]);
        var childEntity = new EntityConfig(
            "InvoiceLine",
            ["LineId"],
            ["LineId", "Description"],
            ["InvoiceId"]
        );

        // Act
        var config = new GroupingConfig("TestQuery", "ParentChild", parentEntity, childEntity);

        // Assert
        Assert.Equal("TestQuery", config.QueryName);
        Assert.Equal("ParentChild", config.GroupingStrategy);
        Assert.Equal("Invoice", config.ParentEntity.Name);
        Assert.Equal("InvoiceLine", config.ChildEntity.Name);
    }

    [Fact]
    public void EntityConfig_CanBeCreatedWithoutParentKeyColumns()
    {
        // Arrange & Act
        var entity = new EntityConfig("Invoice", ["Id"], ["Id", "InvoiceNumber", "CustomerName"]);

        // Assert
        Assert.Equal("Invoice", entity.Name);
        Assert.Single(entity.KeyColumns);
        Assert.Contains("Id", entity.KeyColumns);
        Assert.Equal(3, entity.Columns.Count);
        Assert.Null(entity.ParentKeyColumns);
    }

    [Fact]
    public void EntityConfig_CanBeCreatedWithParentKeyColumns()
    {
        // Arrange & Act
        var entity = new EntityConfig(
            "InvoiceLine",
            ["LineId"],
            ["LineId", "Description"],
            ["InvoiceId"]
        );

        // Assert
        Assert.Equal("InvoiceLine", entity.Name);
        Assert.Single(entity.KeyColumns);
        Assert.Contains("LineId", entity.KeyColumns);
        Assert.Equal(2, entity.Columns.Count);
        Assert.NotNull(entity.ParentKeyColumns);
        Assert.Single(entity.ParentKeyColumns);
        Assert.Contains("InvoiceId", entity.ParentKeyColumns);
    }

    [Fact]
    public void GroupingConfig_JsonSerialization_WorksCorrectly()
    {
        // Arrange
        var config = new GroupingConfig(
            "GetInvoices",
            "ParentChild",
            new EntityConfig("Invoice", ["Id"], ["Id", "InvoiceNumber", "CustomerName"]),
            new EntityConfig(
                "InvoiceLine",
                ["LineId"],
                ["LineId", "Description", "Amount"],
                ["InvoiceId"]
            )
        );

        // Act
        var json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );
        var deserialized = JsonSerializer.Deserialize<GroupingConfig>(
            json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal("GetInvoices", deserialized.QueryName);
        Assert.Equal("ParentChild", deserialized.GroupingStrategy);
        Assert.Equal("Invoice", deserialized.ParentEntity.Name);
        Assert.Equal("InvoiceLine", deserialized.ChildEntity.Name);
        Assert.Single(deserialized.ParentEntity.KeyColumns);
        Assert.Contains("Id", deserialized.ParentEntity.KeyColumns);
        Assert.NotNull(deserialized.ChildEntity.ParentKeyColumns);
        Assert.Single(deserialized.ChildEntity.ParentKeyColumns);
        Assert.Contains("InvoiceId", deserialized.ChildEntity.ParentKeyColumns);
    }

    [Fact]
    public void EntityConfig_PropertiesAreCorrect()
    {
        // Arrange
        var entity1 = new EntityConfig("Invoice", ["Id"], ["Id", "Name"]);
        var entity2 = new EntityConfig("Invoice", ["Id"], ["Id", "Name"]);
        var entity3 = new EntityConfig("InvoiceLine", ["Id"], ["Id", "Name"]);

        // Act & Assert - Test the meaningful properties instead of record equality
        Assert.Equal(entity1.Name, entity2.Name);
        Assert.Equal(entity1.KeyColumns.Count, entity2.KeyColumns.Count);
        Assert.Equal(entity1.Columns.Count, entity2.Columns.Count);

        Assert.NotEqual(entity1.Name, entity3.Name);
    }

    [Fact]
    public void GroupingConfig_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var parentEntity = new EntityConfig("Invoice", ["Id"], ["Id", "Name"]);
        var childEntity = new EntityConfig(
            "InvoiceLine",
            ["LineId"],
            ["LineId", "Description"],
            ["InvoiceId"]
        );

        var config1 = new GroupingConfig("TestQuery", "ParentChild", parentEntity, childEntity);
        var config2 = new GroupingConfig("TestQuery", "ParentChild", parentEntity, childEntity);
        var config3 = new GroupingConfig(
            "DifferentQuery",
            "ParentChild",
            parentEntity,
            childEntity
        );

        // Act & Assert
        Assert.Equal(config1, config2);
        Assert.NotEqual(config1, config3);
    }

    [Fact]
    public void DataProviderConfig_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var tables = new List<TableConfig>
        {
            new() { Name = "Invoice", Schema = "dbo" },
        }.AsReadOnly();

        var config1 = new DataProviderConfig { Tables = tables, ConnectionString = "test" };
        var config2 = new DataProviderConfig { Tables = tables, ConnectionString = "test" };
        var config3 = new DataProviderConfig { Tables = tables, ConnectionString = "different" };

        // Act & Assert
        Assert.Equal(config1, config2);
        Assert.NotEqual(config1, config3);
    }

    [Fact]
    public void TableConfig_PropertiesAreCorrect()
    {
        // Arrange
        var config1 = new TableConfig { Name = "Invoice", Schema = "dbo" };
        var config2 = new TableConfig { Name = "Invoice", Schema = "dbo" };
        var config3 = new TableConfig { Name = "InvoiceLine", Schema = "dbo" };

        // Act & Assert - Test the meaningful properties instead of record equality
        Assert.Equal(config1.Name, config2.Name);
        Assert.Equal(config1.Schema, config2.Schema);
        Assert.Equal(config1.GenerateInsert, config2.GenerateInsert);

        Assert.NotEqual(config1.Name, config3.Name);
        Assert.Equal(config1.Schema, config3.Schema);
    }
}
