-- Remove legacy cutting_lists after production_work_orders becomes the direct production source.

SET @drop_work_order_cutting_list_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_wo_cutting_list') = 1,
    'ALTER TABLE production_work_orders DROP FOREIGN KEY fk_production_wo_cutting_list',
    'SELECT 1'
);
PREPARE drop_work_order_cutting_list_fk_stmt FROM @drop_work_order_cutting_list_fk_sql;
EXECUTE drop_work_order_cutting_list_fk_stmt;
DEALLOCATE PREPARE drop_work_order_cutting_list_fk_stmt;

SET @drop_cutting_list_status_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND CONSTRAINT_NAME = 'fk_cutting_lists_status_master') = 1,
    'ALTER TABLE cutting_lists DROP FOREIGN KEY fk_cutting_lists_status_master',
    'SELECT 1'
);
PREPARE drop_cutting_list_status_fk_stmt FROM @drop_cutting_list_status_fk_sql;
EXECUTE drop_cutting_list_status_fk_stmt;
DEALLOCATE PREPARE drop_cutting_list_status_fk_stmt;

DROP TABLE IF EXISTS cutting_lists;
