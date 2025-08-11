open System
open System.IO
open Microsoft.Data.Sqlite
open Lql.TypeProvider.FSharp
open Lql
open Lql.SQLite

/// <summary>
/// F# example demonstrating LQL usage with SQLite database
/// </summary>
[<EntryPoint>]
let main argv =
    let connectionString = "Data Source=invoices.db"
    
    // Ensure database file exists and create tables if needed
    let setupDatabase () =
        use connection = new SqliteConnection(connectionString)
        connection.Open()
        
        // Create tables
        let createTablesSql = """
            CREATE TABLE IF NOT EXISTS Invoice (
                Id INTEGER PRIMARY KEY,
                InvoiceNumber TEXT NOT NULL,
                InvoiceDate TEXT NOT NULL,
                CustomerName TEXT NOT NULL,
                CustomerEmail TEXT NULL,
                TotalAmount REAL NOT NULL,
                DiscountAmount REAL NULL,
                Notes TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS InvoiceLine (
                Id INTEGER PRIMARY KEY,
                InvoiceId SMALLINT NOT NULL,
                Description TEXT NOT NULL,
                Quantity REAL NOT NULL,
                UnitPrice REAL NOT NULL,
                Amount REAL NOT NULL,
                DiscountPercentage REAL NULL,
                Notes TEXT NULL,
                FOREIGN KEY (InvoiceId) REFERENCES Invoice (Id)
            );

            CREATE TABLE IF NOT EXISTS Customer (
                Id INTEGER PRIMARY KEY,
                CustomerName TEXT NOT NULL,
                Email TEXT NULL,
                Phone TEXT NULL,
                CreatedDate TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Address (
                Id INTEGER PRIMARY KEY,
                CustomerId SMALLINT NOT NULL,
                Street TEXT NOT NULL,
                City TEXT NOT NULL,
                State TEXT NOT NULL,
                ZipCode TEXT NOT NULL,
                Country TEXT NOT NULL,
                FOREIGN KEY (CustomerId) REFERENCES Customer (Id)
            );
        """
        
        use command = new SqliteCommand(createTablesSql, connection)
        command.ExecuteNonQuery() |> ignore
        
        // Clear existing data
        let clearDataSql = "DELETE FROM InvoiceLine; DELETE FROM Invoice; DELETE FROM Address; DELETE FROM Customer;"
        use clearCommand = new SqliteCommand(clearDataSql, connection)
        clearCommand.ExecuteNonQuery() |> ignore
        
        // Insert sample data
        let insertDataSql = """
            INSERT INTO Invoice (InvoiceNumber, InvoiceDate, CustomerName, CustomerEmail, TotalAmount, DiscountAmount, Notes) 
            VALUES ('INV-001', '2024-01-15', 'Acme Corp', 'billing@acme.com', 1250.00, 50.00, 'Sample invoice'),
                   ('INV-002', '2024-01-20', 'Tech Solutions', 'billing@techsolutions.com', 800.00, 25.00, 'Monthly service');

            INSERT INTO InvoiceLine (InvoiceId, Description, Quantity, UnitPrice, Amount, DiscountPercentage, Notes) 
            VALUES 
                (1, 'Software License', 1.0, 1000.00, 1000.00, NULL, NULL),
                (1, 'Support Package', 1.0, 250.00, 250.00, NULL, 'First year'),
                (2, 'Consulting Hours', 8.0, 100.00, 800.00, NULL, 'Development work');

            INSERT INTO Customer (CustomerName, Email, Phone, CreatedDate)
            VALUES 
                ('Acme Corp', 'contact@acme.com', '555-0100', '2024-01-01'),
                ('Tech Solutions', 'info@techsolutions.com', '555-0200', '2024-01-02'),
                ('Global Industries', 'hello@global.com', '555-0300', '2024-01-03');

            INSERT INTO Address (CustomerId, Street, City, State, ZipCode, Country)
            VALUES 
                (1, '123 Business Ave', 'New York', 'NY', '10001', 'USA'),
                (1, '456 Main St', 'Albany', 'NY', '12201', 'USA'),
                (2, '789 Tech Blvd', 'San Francisco', 'CA', '94105', 'USA'),
                (3, '321 Corporate Dr', 'Chicago', 'IL', '60601', 'USA');
        """
        
        use insertCommand = new SqliteCommand(insertDataSql, connection)
        insertCommand.ExecuteNonQuery() |> ignore
    
    // Function to execute LQL queries using the extension functions
    let testLqlQueries () =
        async {
            printfn "=== Testing LQL Queries in F# ==="
            
            // Test GetCustomers query
            let customersLql = File.ReadAllText("GetCustomers.lql")
            printfn "\n--- Executing GetCustomers.lql ---"
            printfn "LQL Query:\n%s\n" customersLql
            
            let! customersResult = executeLqlQuery connectionString customersLql
            match customersResult with
            | Ok customers ->
                printfn "Found %d customers:" customers.Length
                for customer in customers do
                    let customerName = customer.["CustomerName"] :?> string
                    let email = customer.["Email"]
                    let city = customer.["City"] :?> string
                    let state = customer.["State"] :?> string
                    printfn "  - %s (%A) from %s, %s" customerName email city state
            | Error errorMsg ->
                printfn "Error executing customers query: %s" errorMsg
            
            // Test GetInvoices query with parameter
            let invoicesLql = File.ReadAllText("GetInvoices.lql")
            printfn "\n--- Executing GetInvoices.lql ---"
            printfn "LQL Query:\n%s\n" invoicesLql
            
            let! invoicesResult = executeLqlQuery connectionString invoicesLql
            match invoicesResult with
            | Ok invoices ->
                printfn "Found %d invoice lines:" invoices.Length
                for invoice in invoices do
                    let invoiceNumber = invoice.["InvoiceNumber"] :?> string
                    let customerName = invoice.["CustomerName"] :?> string
                    let description = invoice.["Description"] :?> string
                    let amount = invoice.["Amount"] :?> float
                    printfn "  - %s for %s: %s ($%.2f)" invoiceNumber customerName description amount
            | Error errorMsg ->
                printfn "Error executing invoices query: %s" errorMsg
            
            // Test a simple inline query
            printfn "\n--- Executing inline LQL query ---"
            let inlineLql = """
                Customer
                |> select(Customer.Id, Customer.CustomerName, Customer.Email)
                |> filter(fn(row) => Customer.CustomerName LIKE '%Corp%')
                |> order_by(Customer.CustomerName)
            """
            printfn "LQL Query:\n%s\n" inlineLql
            
            let! inlineResult = executeLqlQuery connectionString inlineLql
            match inlineResult with
            | Ok results ->
                printfn "Found %d matching customers:" results.Length
                for result in results do
                    let id = result.["Id"] :?> int64
                    let name = result.["CustomerName"] :?> string
                    let email = result.["Email"]
                    printfn "  - ID: %d, Name: %s, Email: %A" id name email
            | Error errorMsg ->
                printfn "Error executing inline query: %s" errorMsg
        }
    
    // Function to demonstrate direct SQL conversion using the type provider functions
    let testSqlConversion () =
        printfn "\n=== Testing LQL to SQL Conversion ==="
        
        let testQueries = [
            "Simple Select", "Customer |> select(Customer.Id, Customer.CustomerName)"
            "With Filter", "Customer |> filter(fn(row) => Customer.Id > 1) |> select(Customer.CustomerName)"
            "With Join", "Customer |> join(Address, on = Customer.Id = Address.CustomerId) |> select(Customer.CustomerName, Address.City)"
        ]
        
        for (name, lql) in testQueries do
            printfn "\n--- %s ---" name
            printfn "LQL: %s" lql
            
            match lqlToSql lql with
            | Ok sql ->
                printfn "SQL: %s" sql
            | Error errorMsg ->
                printfn "Error: %s" errorMsg
    
    try
        setupDatabase()
        printfn "Database setup completed successfully."
        
        testSqlConversion()
        
        testLqlQueries() |> Async.RunSynchronously
        
        printfn "\nF# LQL example completed successfully!"
        0
    with
    | ex ->
        printfn "Error: %s" ex.Message
        printfn "Stack trace: %s" ex.StackTrace
        1