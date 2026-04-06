SELECT
    sales_data.product_id,
    COUNT(*) AS total_sales,
    SUM(sales_data.amount) AS total_revenue,
    AVG(sales_data.amount) AS avg_sale,
    MAX(sales_data.amount) AS max_sale,
    MIN(sales_data.amount) AS min_sale,
    SUM(sales_data.amount * sales_data.quantity) AS weighted_revenue,
    ROUND(AVG(sales_data.amount), 2) AS rounded_avg,
    ABS(SUM(sales_data.amount) - AVG(sales_data.amount)) AS deviation
FROM sales_data
GROUP BY sales_data.product_id
HAVING COUNT(*) > 5 AND ABS(sales_data.amount) > 1000