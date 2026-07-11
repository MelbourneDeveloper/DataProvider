SELECT 
    i.Id,
    i.InvoiceNumber,
    i.Amount,
    c.Name as CustomerName,
    ii.ProductName,
    ii.Quantity,
    ii.UnitPrice
FROM Invoices i
INNER JOIN Customers c ON i.CustomerId = c.Id AND c.IsActive = 1
INNER JOIN InvoiceItems ii ON i.Id = ii.InvoiceId
LEFT JOIN Products p ON ii.ProductId = p.Id AND p.IsDeleted = 0
WHERE i.InvoiceDate >= @startDate 
  AND i.InvoiceDate <= @endDate
  AND (@customerId IS NULL OR i.CustomerId = @customerId) 