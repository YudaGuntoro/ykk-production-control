-- Use the logged-in user as the production activity actor and remove the legacy work-order PIC column.

SET @add_activity_user_column_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_activity_logs' AND COLUMN_NAME = 'user_id') = 0,
    'ALTER TABLE production_activity_logs ADD COLUMN user_id INT NULL AFTER production_work_order_id',
    'SELECT 1'
);
PREPARE add_activity_user_column_stmt FROM @add_activity_user_column_sql;
EXECUTE add_activity_user_column_stmt;
DEALLOCATE PREPARE add_activity_user_column_stmt;

UPDATE production_activity_logs
SET user_id = COALESCE(user_id, 1)
WHERE user_id IS NULL
  AND (SELECT COUNT(*) FROM users WHERE id = 1) = 1;

SET @drop_activity_pic_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_activity_logs' AND CONSTRAINT_NAME = 'fk_production_logs_pic') = 1,
    'ALTER TABLE production_activity_logs DROP FOREIGN KEY fk_production_logs_pic',
    'SELECT 1'
);
PREPARE drop_activity_pic_fk_stmt FROM @drop_activity_pic_fk_sql;
EXECUTE drop_activity_pic_fk_stmt;
DEALLOCATE PREPARE drop_activity_pic_fk_stmt;

SET @drop_activity_pic_column_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_activity_logs' AND COLUMN_NAME = 'pic_card_id') = 1,
    'ALTER TABLE production_activity_logs DROP COLUMN pic_card_id',
    'SELECT 1'
);
PREPARE drop_activity_pic_column_stmt FROM @drop_activity_pic_column_sql;
EXECUTE drop_activity_pic_column_stmt;
DEALLOCATE PREPARE drop_activity_pic_column_stmt;

SET @add_activity_user_index_sql := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_activity_logs' AND INDEX_NAME = 'ix_production_activity_logs_user') = 0,
    'ALTER TABLE production_activity_logs ADD INDEX ix_production_activity_logs_user (user_id)',
    'SELECT 1'
);
PREPARE add_activity_user_index_stmt FROM @add_activity_user_index_sql;
EXECUTE add_activity_user_index_stmt;
DEALLOCATE PREPARE add_activity_user_index_stmt;

SET @add_activity_user_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_activity_logs' AND CONSTRAINT_NAME = 'fk_production_activity_logs_user') = 0,
    'ALTER TABLE production_activity_logs ADD CONSTRAINT fk_production_activity_logs_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL',
    'SELECT 1'
);
PREPARE add_activity_user_fk_stmt FROM @add_activity_user_fk_sql;
EXECUTE add_activity_user_fk_stmt;
DEALLOCATE PREPARE add_activity_user_fk_stmt;

SET @drop_work_order_pic_fk_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_wo_pic') = 1,
    'ALTER TABLE production_work_orders DROP FOREIGN KEY fk_production_wo_pic',
    'SELECT 1'
);
PREPARE drop_work_order_pic_fk_stmt FROM @drop_work_order_pic_fk_sql;
EXECUTE drop_work_order_pic_fk_stmt;
DEALLOCATE PREPARE drop_work_order_pic_fk_stmt;

SET @drop_work_order_pic_column_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'pic_card_id') = 1,
    'ALTER TABLE production_work_orders DROP COLUMN pic_card_id',
    'SELECT 1'
);
PREPARE drop_work_order_pic_column_stmt FROM @drop_work_order_pic_column_sql;
EXECUTE drop_work_order_pic_column_stmt;
DEALLOCATE PREPARE drop_work_order_pic_column_stmt;
