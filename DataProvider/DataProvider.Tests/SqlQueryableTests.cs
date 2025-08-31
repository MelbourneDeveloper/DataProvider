using System.Linq;
using Selecta;
using Xunit;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes

namespace DataProvider.Tests;

/// <summary>
/// Tests for LINQ query expression support with SelectQueryable
/// </summary>
public sealed class SqlQueryableTests
{
    [Fact]
    public void SimpleSelectQuery_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query = from u in SelectStatement.From<User>("users") select u;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.Tables);
        Assert.Contains(statement.Tables, t => t.Name == "users");
        Assert.Single(statement.SelectList);
        Assert.IsType<WildcardColumn>(statement.SelectList[0]);
    }

    [Fact]
    public void SelectWithWhere_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query =
            from user in SelectStatement.From<User>("users")
            where user.Age > 18
            select user;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.Tables);
        Assert.Single(statement.WhereConditions);

        var condition = statement.WhereConditions[0] as ComparisonCondition;
        Assert.NotNull(condition);
        Assert.Equal(ComparisonOperator.GreaterThan, condition.Operator);
    }

    [Fact]
    public void SelectWithOrderBy_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query =
            from user in SelectStatement.From<User>("users")
            where user.Age >= 21
            orderby user.Name
            select user;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.Tables);
        Assert.Single(statement.OrderByItems);

        var orderBy = statement.OrderByItems[0];
        Assert.Equal("Name", orderBy.Column);
        Assert.Equal("ASC", orderBy.Direction);
    }

    [Fact]
    public void SelectWithComplexWhere_OrCondition_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query =
            from num in SelectStatement.From<Number>("numbers")
            where num.Value < 3 || num.Value > 7
            orderby num.Value
            select num;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.Tables);
        Assert.Contains(statement.Tables, t => t.Name == "numbers");

        // Check for OR condition structure: (condition) OR (condition)
        var conditions = statement.WhereConditions.ToList();
        Assert.True(conditions.Count >= 5); // OpenParen, Condition, CloseParen, OR, OpenParen, Condition, CloseParen

        // Verify we have an OR operator
        Assert.Contains(conditions, c => c == LogicalOperator.OrOperator);

        // Check ORDER BY
        Assert.Single(statement.OrderByItems);
        var orderBy = statement.OrderByItems[0];
        Assert.Equal("Value", orderBy.Column);
        Assert.Equal("ASC", orderBy.Direction);
    }

    [Fact]
    public void SelectWithAndCondition_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query =
            from product in SelectStatement.From<Product>("products")
            where product.Price > 10 && product.Price < 100
            select product;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.Tables);

        var conditions = statement.WhereConditions.ToList();
        Assert.True(conditions.Count >= 3); // Condition, AND, Condition

        // Verify we have an AND operator
        Assert.Contains(conditions, c => c == LogicalOperator.AndOperator);
    }

    [Fact]
    public void SelectWithOrderByDescending_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query =
            from item in SelectStatement.From<Item>("items")
            where item.Quantity > 0
            orderby item.Price descending
            select item;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.OrderByItems);
        var orderBy = statement.OrderByItems[0];
        Assert.Equal("Price", orderBy.Column);
        Assert.Equal("DESC", orderBy.Direction);
    }

    [Fact]
    public void SelectWithMultipleConditions_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query =
            from emp in SelectStatement.From<Employee>("employees")
            where emp.Age >= 18 && emp.Age <= 65 && emp.IsActive == true
            orderby emp.Salary descending
            select emp;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.Tables);

        // Should have multiple AND conditions
        var conditions = statement.WhereConditions.ToList();
        var andConditions = 0;
        foreach (var c in conditions)
        {
            if (c == LogicalOperator.AndOperator)
                andConditions++;
        }
        Assert.Equal(2, andConditions); // Two AND operators for three conditions

        // Check ordering
        Assert.Single(statement.OrderByItems);
        Assert.Equal("Salary", statement.OrderByItems[0].Column);
        Assert.Equal("DESC", statement.OrderByItems[0].Direction);
    }

    [Fact]
    public void SelectWithTableAlias_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query = from u in SelectStatement.From<User>("users", "u") where u.Id > 0 select u;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Single(statement.Tables);
        var table = statement.Tables.FirstOrDefault()!;
        Assert.Equal("users", table.Name);
        Assert.Equal("u", table.Alias);
    }

    [Fact]
    public void SelectWithDistinct_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query = (
            from category in SelectStatement.From<Category>("categories")
            select category
        ).Distinct();

        var statement = query.ToSqlStatement();

        // Assert
        Assert.True(statement.IsDistinct);
    }

    [Fact]
    public void SelectWithTakeAndSkip_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act
        var query = (
            from product in SelectStatement.From<Product>("products")
            where product.InStock == true
            orderby product.Name
            select product
        )
            .Skip(10)
            .Take(20);

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Equal("10", statement.Offset);
        Assert.Equal("20", statement.Limit);
    }

    [Fact]
    public void ComplexNumberQuery_LikeOriginalExample_GeneratesCorrectSqlStatement()
    {
        // Arrange & Act - This mimics the user's example
        var query =
            from num in SelectStatement.From<Number>("numbers")
            where num.Value < 3 || num.Value > 7
            orderby num.Value
            select num;

        var statement = query.ToSqlStatement();

        // Assert
        Assert.Equal("numbers", statement.Tables.FirstOrDefault()?.Name);
        Assert.True(statement.OrderByItems.Count > 0);
        Assert.Equal("Value", statement.OrderByItems[0].Column);
        Assert.Equal("ASC", statement.OrderByItems[0].Direction);

        // Verify the WHERE clause has OR condition
        var whereConditions = statement.WhereConditions.ToList();
        Assert.Contains(
            whereConditions,
            w => w is ComparisonCondition cc && cc.Operator == ComparisonOperator.LessThan
        );
        Assert.Contains(
            whereConditions,
            w => w is ComparisonCondition cc && cc.Operator == ComparisonOperator.GreaterThan
        );
        Assert.Contains(whereConditions, w => w == LogicalOperator.OrOperator);
    }

    [Fact]
    public void SelectWithStringContains_GeneratesLikeCondition()
    {
        // Arrange & Act
        var query =
            from user in SelectStatement.From<User>("users")
            where user.Name.Contains("John")
            select user;

        var statement = query.ToSqlStatement();

        // Assert
        var condition = statement.WhereConditions.OfType<ComparisonCondition>().FirstOrDefault();
        Assert.NotNull(condition);
        Assert.Equal(ComparisonOperator.Like, condition.Operator);
        Assert.Equal("%John%", condition.Right);
    }

    [Fact]
    public void SelectWithStringStartsWith_GeneratesLikeCondition()
    {
        // Arrange & Act
        var query =
            from user in SelectStatement.From<User>("users")
            where user.Email.StartsWith("admin")
            select user;

        var statement = query.ToSqlStatement();

        // Assert
        var condition = statement.WhereConditions.OfType<ComparisonCondition>().FirstOrDefault();
        Assert.NotNull(condition);
        Assert.Equal(ComparisonOperator.Like, condition.Operator);
        Assert.Equal("admin%", condition.Right);
    }

    // Test model classes
    private sealed class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public string Email { get; set; } = "";
    }

    private sealed class Number
    {
        public int Value { get; set; }
    }

    private sealed class Product
    {
        public decimal Price { get; set; }
        public bool InStock { get; set; }
        public string Name { get; set; } = "";
    }

    private sealed class Item
    {
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    private sealed class Employee
    {
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public decimal Salary { get; set; }
    }

    private sealed class Category
    {
        public string Name { get; set; } = "";
    }

    #region PredicateBuilder Tests

    [Fact]
    public void PredicateBuilder_True_GeneratesAlwaysTrueExpression()
    {
        // Arrange & Act
        var predicate = PredicateBuilder.True<User>();
        var query = SelectStatement.From<User>().Where(predicate);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(predicate);
        Assert.NotNull(statement);
    }

    [Fact]
    public void PredicateBuilder_False_GeneratesAlwaysFalseExpression()
    {
        // Arrange & Act
        var predicate = PredicateBuilder.False<User>();
        var query = SelectStatement.From<User>().Where(predicate);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(predicate);
        Assert.NotNull(statement);
    }

    [Fact]
    public void PredicateBuilder_Or_CombinesTwoPredicatesWithOrLogic()
    {
        // Arrange
        var predicate1 = PredicateBuilder.False<User>();
        var predicate2 = PredicateBuilder.False<User>();

        // Act
        var combined = predicate1.Or(predicate2);
        var query = SelectStatement.From<User>().Where(combined);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(combined);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions);
    }

    [Fact]
    public void PredicateBuilder_And_CombinesTwoPredicatesWithAndLogic()
    {
        // Arrange
        var predicate1 = PredicateBuilder.True<User>();
        var predicate2 = PredicateBuilder.True<User>();

        // Act
        var combined = predicate1.And(predicate2);
        var query = SelectStatement.From<User>().Where(combined);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(combined);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions);
    }

    [Fact]
    public void PredicateBuilder_Not_NegatesPredicateLogic()
    {
        // Arrange
        var predicate = PredicateBuilder.True<User>();

        // Act
        var negated = predicate.Not();
        var query = SelectStatement.From<User>().Where(negated);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(negated);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions);
    }

    [Fact]
    public void PredicateBuilder_ChainedOrOperations_BuildsComplexPredicate()
    {
        // Arrange
        var ids = new[] { 1, 2, 3, 4, 5 };
        var predicate = PredicateBuilder.False<User>();

        // Act - simulate building dynamic OR conditions
        foreach (var id in ids)
        {
            var tempId = id; // Capture for closure
            predicate = predicate.Or(u => u.Id == tempId);
        }

        var query = SelectStatement.From<User>().Where(predicate);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(predicate);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions); // Complex condition gets flattened
    }

    [Fact]
    public void PredicateBuilder_ChainedAndOperations_BuildsComplexPredicate()
    {
        // Arrange
        var minAge = 18;
        var maxAge = 65;
        var predicate = PredicateBuilder.True<User>();

        // Act - simulate building dynamic AND conditions
        predicate = predicate.And(u => u.Age >= minAge);
        predicate = predicate.And(u => u.Age <= maxAge);
        predicate = predicate.And(u => u.Email != null);

        var query = SelectStatement.From<User>().Where(predicate);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(predicate);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions); // Complex condition gets flattened
    }

    [Fact]
    public void PredicateBuilder_MixedAndOrOperations_BuildsComplexPredicate()
    {
        // Arrange - simulate search scenario with multiple filters
        var searchNames = new[] { "John", "Jane" };
        var minAge = 21;

        // Act - build name filter with OR
        var namePredicate = PredicateBuilder.False<User>();
        foreach (var name in searchNames)
        {
            var tempName = name; // Capture for closure
            namePredicate = namePredicate.Or(u => u.Name == tempName);
        }

        // Combine with age filter using AND
        var finalPredicate = namePredicate.And(u => u.Age >= minAge);

        var query = SelectStatement.From<User>().Where(finalPredicate);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(finalPredicate);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions);
    }

    [Fact]
    public void PredicateBuilder_ConditionalBuilding_EliminatesDuplication()
    {
        // Arrange - simulate conditional filtering scenario
        var searchByAge = true;
        var searchByEmail = false;
        var minAge = 25;

        var predicate = PredicateBuilder.True<User>();

        // Act - conditional predicate building (eliminates if/else duplication)
        if (searchByAge)
        {
            predicate = predicate.And(u => u.Age >= minAge);
        }
        if (searchByEmail)
        {
            predicate = predicate.And(u => u.Email != null);
        }

        var query = SelectStatement.From<User>().Where(predicate);
        var statement = query.ToSqlStatement();

        // Assert - demonstrates zero duplication approach
        Assert.NotNull(predicate);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions);
    }

    [Fact]
    public void PredicateBuilder_ComplexBusinessLogic_GeneratesCorrectSql()
    {
        // Arrange - simulate complex business filtering
        var activeAgeRanges = new[] { (18, 30), (40, 55) };
        var predicate = PredicateBuilder.False<User>();

        // Act - build complex business logic: (Age 18-30 OR Age 40-55) AND Email exists
        foreach (var (minAge, maxAge) in activeAgeRanges)
        {
            predicate = predicate.Or(u => u.Age >= minAge && u.Age <= maxAge);
        }
        predicate = predicate.And(u => u.Email != null);

        var query = SelectStatement.From<User>().Where(predicate);
        var statement = query.ToSqlStatement();

        // Assert
        Assert.NotNull(predicate);
        Assert.NotNull(statement);
        Assert.Single(statement.WhereConditions);
    }

    #endregion
}
