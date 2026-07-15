-- Rename production work order number column from WO terminology to Order Number.

SET @rename_wo_number_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'wo_number') = 1
    AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'order_number') = 0,
    'ALTER TABLE production_work_orders CHANGE COLUMN wo_number order_number VARCHAR(80) NOT NULL',
    'SELECT 1'
);
PREPARE rename_wo_number_stmt FROM @rename_wo_number_sql;
EXECUTE rename_wo_number_stmt;
DEALLOCATE PREPARE rename_wo_number_stmt;

SET @copy_wo_number_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'wo_number') = 1
    AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'order_number') = 1,
    'UPDATE production_work_orders SET order_number = wo_number WHERE (order_number IS NULL OR order_number = '''') AND wo_number IS NOT NULL',
    'SELECT 1'
);
PREPARE copy_wo_number_stmt FROM @copy_wo_number_sql;
EXECUTE copy_wo_number_stmt;
DEALLOCATE PREPARE copy_wo_number_stmt;

SET @drop_old_wo_number_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'wo_number') = 1
    AND (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'uq_production_work_orders_no' AND COLUMN_NAME = 'wo_number') > 0,
    'ALTER TABLE production_work_orders DROP INDEX uq_production_work_orders_no',
    'SELECT 1'
);
PREPARE drop_old_wo_number_index_stmt FROM @drop_old_wo_number_index_sql;
EXECUTE drop_old_wo_number_index_stmt;
DEALLOCATE PREPARE drop_old_wo_number_index_stmt;

SET @drop_wo_number_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'wo_number') = 1
    AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'order_number') = 1,
    'ALTER TABLE production_work_orders DROP COLUMN wo_number',
    'SELECT 1'
);
PREPARE drop_wo_number_stmt FROM @drop_wo_number_sql;
EXECUTE drop_wo_number_stmt;
DEALLOCATE PREPARE drop_wo_number_stmt;

SET @create_order_number_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'uq_production_work_orders_no') = 0,
    'ALTER TABLE production_work_orders ADD UNIQUE KEY uq_production_work_orders_no (order_number)',
    'SELECT 1'
);
PREPARE create_order_number_index_stmt FROM @create_order_number_index_sql;
EXECUTE create_order_number_index_stmt;
DEALLOCATE PREPARE create_order_number_index_stmt;
