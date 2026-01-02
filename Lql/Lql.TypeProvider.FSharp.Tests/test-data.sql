-- Test data for F# Type Provider E2E tests
-- Schema is created by Migration.CLI from YAML - this file contains DATA ONLY

-- Clear existing data
DELETE FROM Orders;
DELETE FROM Products;
DELETE FROM Users;
DELETE FROM Customer;

-- Customer test data
INSERT INTO Customer (Id, Name, Email, Age, Status) VALUES
    ('cust-001', 'Acme Corp', 'acme@example.com', 10, 'active'),
    ('cust-002', 'Tech Corp', 'tech@example.com', 5, 'active'),
    ('cust-003', 'Old Corp', 'old@example.com', 50, 'inactive');

-- Users test data
INSERT INTO Users (Id, Name, Email, Age, Status, Role, CreatedAt) VALUES
    ('user-001', 'Alice', 'alice@example.com', 30, 'active', 'admin', '2024-01-01'),
    ('user-002', 'Bob', 'bob@example.com', 17, 'active', 'user', '2024-01-02'),
    ('user-003', 'Charlie', 'charlie@example.com', 25, 'inactive', 'user', '2024-01-03'),
    ('user-004', 'Diana', 'diana@example.com', 16, 'active', 'admin', '2024-01-04');

-- Products test data
INSERT INTO Products (Id, Name, Price, Quantity) VALUES
    ('prod-001', 'Widget', 10.0, 100),
    ('prod-002', 'Gadget', 25.0, 50),
    ('prod-003', 'Gizmo', 15.0, 75);

-- Orders test data
INSERT INTO Orders (Id, UserId, ProductId, Total, Subtotal, Tax, Discount, Status) VALUES
    ('ord-001', 'user-001', 'prod-001', 100.0, 90.0, 15.0, 5.0, 'completed'),
    ('ord-002', 'user-001', 'prod-002', 200.0, 180.0, 30.0, 10.0, 'completed'),
    ('ord-003', 'user-002', 'prod-001', 50.0, 45.0, 7.5, 2.5, 'pending'),
    ('ord-004', 'user-001', 'prod-003', 150.0, 135.0, 22.5, 7.5, 'completed'),
    ('ord-005', 'user-001', 'prod-001', 75.0, 67.5, 11.25, 3.75, 'completed'),
    ('ord-006', 'user-001', 'prod-002', 300.0, 270.0, 45.0, 15.0, 'completed'),
    ('ord-007', 'user-001', 'prod-003', 125.0, 112.5, 18.75, 6.25, 'completed');
