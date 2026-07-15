-- Dashboard shift output chart performance index.

SET @add_dashboard_shift_output_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_status_completed_shift') = 0,
    'ALTER TABLE production_work_orders ADD INDEX ix_production_work_orders_status_completed_shift (status_master_id, completed_at, shift_master_id)',
    'SELECT 1'
);
PREPARE add_dashboard_shift_output_index_stmt FROM @add_dashboard_shift_output_index_sql;
EXECUTE add_dashboard_shift_output_index_stmt;
DEALLOCATE PREPARE add_dashboard_shift_output_index_stmt;
