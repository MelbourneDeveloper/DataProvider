SELECT
    orders.id,
    orders.total_amount,
    orders.discount_percent,
    CASE WHEN orders.total_amount > 1000 THEN orders.total_amount * 0.95 WHEN orders.total_amount > 500 THEN orders.total_amount * 0.97 ELSE orders.total_amount END AS discounted_amount,
    CASE WHEN orders.discount_percent > 0 THEN (orders.total_amount * (1 - orders.discount_percent / 100)) ELSE orders.total_amount END AS final_amount
FROM orders
WHERE CASE WHEN orders.total_amount > 2000 THEN orders.discount_percent >= 10 WHEN orders.total_amount > 1000 THEN orders.discount_percent >= 5 ELSE orders.discount_percent >= 0 END