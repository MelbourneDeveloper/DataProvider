SELECT 
    i.Id,
    i.InvoiceNumber,
    i.InvoiceDate,
    i.CustomerName,
    i.CustomerEmail,
    i.TotalAmount,
    i.DiscountAmount,
    i.Notes AS InvoiceNotes,
    l.Id AS LineId,
    l.InvoiceId,
    l.Description,
    l.Quantity,
    l.UnitPrice,
    l.Amount,
    l.DiscountPercentage,
    l.Notes AS LineNotes
FROM Invoice i
JOIN InvoiceLine l ON l.InvoiceId = i.Id
WHERE i.CustomerName = @customerName
    AND (@startDate IS NULL OR i.InvoiceDate >= @startDate)
    AND (@endDate IS NULL OR i.InvoiceDate <= @endDate)
ORDER BY i.InvoiceDate DESC, i.Id
