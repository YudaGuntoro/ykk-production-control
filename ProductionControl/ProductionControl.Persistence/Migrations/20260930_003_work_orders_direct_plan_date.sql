-- Make production_work_orders the direct production source.

SET @add_work_order_plan_date_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'plan_date') = 0,
    'ALTER TABLE production_work_orders ADD COLUMN plan_date DATE NULL AFTER line_code',
    'SELECT 1'
);
PREPARE add_work_order_plan_date_stmt FROM @add_work_order_plan_date_sql;
EXECUTE add_work_order_plan_date_stmt;
DEALLOCATE PREPARE add_work_order_plan_date_stmt;

UPDATE production_work_orders pwo
LEFT JOIN cutting_lists cl ON cl.id = pwo.cutting_list_id
SET pwo.plan_date = COALESCE(pwo.plan_date, cl.plan_date, DATE(pwo.created_at))
WHERE pwo.plan_date IS NULL;

ALTER TABLE production_work_orders MODIFY plan_date DATE NOT NULL;

SET @drop_cutting_list_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_wo_cutting_list') = 1,
    'ALTER TABLE production_work_orders DROP FOREIGN KEY fk_production_wo_cutting_list',
    'SELECT 1'
);
PREPARE drop_cutting_list_fk_stmt FROM @drop_cutting_list_fk_sql;
EXECUTE drop_cutting_list_fk_stmt;
DEALLOCATE PREPARE drop_cutting_list_fk_stmt;

ALTER TABLE production_work_orders MODIFY cutting_list_id INT NULL;

SET @add_plan_line_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_plan_line') = 0,
    'ALTER TABLE production_work_orders ADD INDEX ix_production_work_orders_plan_line (plan_date, line_code)',
    'SELECT 1'
);
PREPARE add_plan_line_index_stmt FROM @add_plan_line_index_sql;
EXECUTE add_plan_line_index_stmt;
DEALLOCATE PREPARE add_plan_line_index_stmt;
