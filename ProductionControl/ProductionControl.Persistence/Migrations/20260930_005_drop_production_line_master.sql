-- Remove legacy production_line_master after Shiage data is reduced to project/order/lot/weight fields.

SET @drop_rpo_line_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'release_production_order_details' AND CONSTRAINT_NAME = 'fk_rpo_details_line') = 1,
    'ALTER TABLE release_production_order_details DROP FOREIGN KEY fk_rpo_details_line',
    'SELECT 1'
);
PREPARE drop_rpo_line_fk_stmt FROM @drop_rpo_line_fk_sql;
EXECUTE drop_rpo_line_fk_stmt;
DEALLOCATE PREPARE drop_rpo_line_fk_stmt;

SET @drop_rpo_line_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'release_production_order_details' AND INDEX_NAME = 'ix_rpo_details_line') > 0,
    'ALTER TABLE release_production_order_details DROP INDEX ix_rpo_details_line',
    'SELECT 1'
);
PREPARE drop_rpo_line_index_stmt FROM @drop_rpo_line_index_sql;
EXECUTE drop_rpo_line_index_stmt;
DEALLOCATE PREPARE drop_rpo_line_index_stmt;

SET @drop_rpo_line_column_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'release_production_order_details' AND COLUMN_NAME = 'production_line_master_id') = 1,
    'ALTER TABLE release_production_order_details DROP COLUMN production_line_master_id',
    'SELECT 1'
);
PREPARE drop_rpo_line_column_stmt FROM @drop_rpo_line_column_sql;
EXECUTE drop_rpo_line_column_stmt;
DEALLOCATE PREPARE drop_rpo_line_column_stmt;

DROP TABLE IF EXISTS production_line_master;
