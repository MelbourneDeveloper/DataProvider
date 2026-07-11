SELECT 
    o.Id,
    o.OrderNumber,
    o.OrderDate,
    o.CustomerId,
    o.TotalAmount,
    o.Status,
    i.Id AS ItemId,
    i.OrderId,
    i.ProductName,
    i.Quantity,
    i.Price,
    i.Subtotal
FROM Orders o
JOIN OrderItem i ON i.OrderId = o.Id
WHERE (@customerId IS NULL OR o.CustomerId = @customerId)
    AND (@status IS NULL OR o.Status = @status)
    AND (@startDate IS NULL OR o.OrderDate >= @startDate)
    AND (@endDate IS NULL OR o.OrderDate <= @endDate)
ORDER BY o.OrderDate DESC, o.Id 