SELECT 
    inventory.product_id,
    inventory.current_stock,
    inventory.reorder_point,
    inventory.max_stock,
    inventory.unit_cost,
    inventory.selling_price,
    ((inventory.selling_price - inventory.unit_cost) / inventory.unit_cost) * 100 AS profit_margin_percent,
    CASE WHEN inventory.current_stock <= inventory.reorder_point THEN (inventory.max_stock - inventory.current_stock) * inventory.unit_cost * 1.1 ELSE 0 END AS reorder_cost_with_buffer
FROM inventory
WHERE ((inventory.current_stock > 0 AND inventory.selling_price > inventory.unit_cost) OR (inventory.current_stock = 0 AND inventory.reorder_point > 0)) AND (inventory.unit_cost > 0 AND inventory.selling_price > 0 AND (inventory.selling_price / inventory.unit_cost) >= 1.2)