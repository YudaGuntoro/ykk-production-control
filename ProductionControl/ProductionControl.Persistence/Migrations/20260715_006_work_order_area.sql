-- Bind production work orders to an Area Master.

SET @add_work_order_area_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'area_master_id') = 0,
    'ALTER TABLE production_work_orders ADD COLUMN area_master_id INT NULL AFTER shift_master_id',
    'SELECT 1'
);
PREPARE add_work_order_area_stmt FROM @add_work_order_area_sql;
EXECUTE add_work_order_area_stmt;
DEALLOCATE PREPARE add_work_order_area_stmt;

SET @add_work_order_area_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_area_master') = 0,
    'ALTER TABLE production_work_orders ADD INDEX ix_production_work_orders_area_master (area_master_id)',
    'SELECT 1'
);
PREPARE add_work_order_area_index_stmt FROM @add_work_order_area_index_sql;
EXECUTE add_work_order_area_index_stmt;
DEALLOCATE PREPARE add_work_order_area_index_stmt;

SET @add_work_order_area_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_work_orders_area_master') = 0,
    'ALTER TABLE production_work_orders ADD CONSTRAINT fk_production_work_orders_area_master FOREIGN KEY (area_master_id) REFERENCES area_master(id) ON DELETE SET NULL',
    'SELECT 1'
);
PREPARE add_work_order_area_fk_stmt FROM @add_work_order_area_fk_sql;
EXECUTE add_work_order_area_fk_stmt;
DEALLOCATE PREPARE add_work_order_area_fk_stmt;
