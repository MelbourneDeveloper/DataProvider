SELECT
    financial_data.id,
    financial_data.revenue,
    financial_data.expenses,
    financial_data.tax_rate,
    (financial_data.revenue - financial_data.expenses) * (1 - financial_data.tax_rate) AS net_profit,
    ((financial_data.revenue * 0.1) + (financial_data.expenses * 0.05)) / 12 AS monthly_overhead,
    financial_data.revenue / (financial_data.expenses + 1) AS efficiency_ratio
FROM financial_data
WHERE (financial_data.revenue > 1000 AND financial_data.expenses < 500) OR (financial_data.revenue > 2000 AND financial_data.tax_rate < 0.3)