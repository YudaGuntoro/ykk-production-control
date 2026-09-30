-- Rename Area Master schema to Line Master.

SET @drop_old_line_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_work_orders_area_master') = 1,
    'ALTER TABLE production_work_orders DROP FOREIGN KEY fk_production_work_orders_area_master',
    'SELECT 1'
);
PREPARE drop_old_line_fk_stmt FROM @drop_old_line_fk_sql;
EXECUTE drop_old_line_fk_stmt;
DEALLOCATE PREPARE drop_old_line_fk_stmt;

SET @rename_area_table_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'area_master') = 1
    AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master') = 0,
    'RENAME TABLE area_master TO line_master',
    'SELECT 1'
);
PREPARE rename_area_table_stmt FROM @rename_area_table_sql;
EXECUTE rename_area_table_stmt;
DEALLOCATE PREPARE rename_area_table_stmt;

SET @rename_line_no_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master' AND COLUMN_NAME = 'area_code') = 1,
    'ALTER TABLE line_master CHANGE COLUMN area_code line_no VARCHAR(50) NOT NULL',
    'SELECT 1'
);
PREPARE rename_line_no_stmt FROM @rename_line_no_sql;
EXECUTE rename_line_no_stmt;
DEALLOCATE PREPARE rename_line_no_stmt;

SET @rename_line_name_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master' AND COLUMN_NAME = 'area_name') = 1,
    'ALTER TABLE line_master CHANGE COLUMN area_name line_name VARCHAR(150) NOT NULL',
    'SELECT 1'
);
PREPARE rename_line_name_stmt FROM @rename_line_name_sql;
EXECUTE rename_line_name_stmt;
DEALLOCATE PREPARE rename_line_name_stmt;

SET @rename_work_order_line_column_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'area_master_id') = 1
    AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'line_master_id') = 0,
    'ALTER TABLE production_work_orders CHANGE COLUMN area_master_id line_master_id INT NULL',
    'SELECT 1'
);
PREPARE rename_work_order_line_column_stmt FROM @rename_work_order_line_column_sql;
EXECUTE rename_work_order_line_column_stmt;
DEALLOCATE PREPARE rename_work_order_line_column_stmt;

SET @drop_old_line_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_area_master') > 0,
    'ALTER TABLE production_work_orders DROP INDEX ix_production_work_orders_area_master',
    'SELECT 1'
);
PREPARE drop_old_line_index_stmt FROM @drop_old_line_index_sql;
EXECUTE drop_old_line_index_stmt;
DEALLOCATE PREPARE drop_old_line_index_stmt;

SET @add_line_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_line_master') = 0,
    'ALTER TABLE production_work_orders ADD INDEX ix_production_work_orders_line_master (line_master_id)',
    'SELECT 1'
);
PREPARE add_line_index_stmt FROM @add_line_index_sql;
EXECUTE add_line_index_stmt;
DEALLOCATE PREPARE add_line_index_stmt;

SET @add_line_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_work_orders_line_master') = 0,
    'ALTER TABLE production_work_orders ADD CONSTRAINT fk_production_work_orders_line_master FOREIGN KEY (line_master_id) REFERENCES line_master(id) ON DELETE SET NULL',
    'SELECT 1'
);
PREPARE add_line_fk_stmt FROM @add_line_fk_sql;
EXECUTE add_line_fk_stmt;
DEALLOCATE PREPARE add_line_fk_stmt;
