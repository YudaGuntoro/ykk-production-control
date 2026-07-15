-- Production Control - normalize production statuses into status_masters.

CREATE TABLE IF NOT EXISTS status_masters (
    id INT PRIMARY KEY,
    status_group VARCHAR(50) NOT NULL,
    status_code VARCHAR(50) NOT NULL,
    status_name VARCHAR(100) NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_status_masters_group_code (status_group, status_code)
);

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'status_masters' AND INDEX_NAME = 'ix_status_masters_group_sort') > 0,
    'ALTER TABLE status_masters DROP INDEX ix_status_masters_group_sort',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'status_masters' AND COLUMN_NAME = 'sort_order') > 0,
    'ALTER TABLE status_masters DROP COLUMN sort_order',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND CONSTRAINT_NAME = 'fk_cutting_lists_status_master') > 0,
    'ALTER TABLE cutting_lists DROP FOREIGN KEY fk_cutting_lists_status_master',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_work_orders_status_master') > 0,
    'ALTER TABLE production_work_orders DROP FOREIGN KEY fk_production_work_orders_status_master',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

DELETE FROM status_masters
WHERE status_group IN ('CUTTING_LIST', 'PRODUCTION_WORK_ORDER')
   OR id IN (101, 102, 103, 104, 105, 106, 201, 202, 203, 204, 205);

INSERT INTO status_masters (id, status_group, status_code, status_name, is_active)
VALUES
    (1, 'PRODUCTION_WORK_ORDER', 'WAITING', 'Waiting', 1),
    (2, 'PRODUCTION_WORK_ORDER', 'IN_PROGRESS', 'In Progress', 1),
    (3, 'PRODUCTION_WORK_ORDER', 'FINISH', 'Finish', 1)
ON DUPLICATE KEY UPDATE
    status_group = VALUES(status_group),
    status_code = VALUES(status_code),
    status_name = VALUES(status_name),
    is_active = VALUES(is_active);

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND COLUMN_NAME = 'status_master_id') = 0,
    'ALTER TABLE cutting_lists ADD COLUMN status_master_id INT NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND COLUMN_NAME = 'status') = 1,
    'UPDATE cutting_lists SET status_master_id = CASE WHEN status IN (''COMPLETED'', ''FINISH'') THEN 3 WHEN status = ''IN_PROGRESS'' THEN 2 ELSE 1 END WHERE status_master_id IS NULL OR status_master_id NOT IN (1, 2, 3)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE cutting_lists
SET status_master_id = CASE
    WHEN status_master_id IN (3, 105, 204) THEN 3
    WHEN status_master_id IN (2, 103, 104, 203) THEN 2
    ELSE 1
END;

ALTER TABLE cutting_lists MODIFY status_master_id INT NOT NULL;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND INDEX_NAME = 'ix_cutting_lists_status_master') = 0,
    'ALTER TABLE cutting_lists ADD INDEX ix_cutting_lists_status_master (status_master_id)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND CONSTRAINT_NAME = 'fk_cutting_lists_status_master') = 0,
    'ALTER TABLE cutting_lists ADD CONSTRAINT fk_cutting_lists_status_master FOREIGN KEY (status_master_id) REFERENCES status_masters(id)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND COLUMN_NAME = 'status') = 1,
    'ALTER TABLE cutting_lists DROP COLUMN status',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'status_master_id') = 0,
    'ALTER TABLE production_work_orders ADD COLUMN status_master_id INT NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'status') = 1,
    'UPDATE production_work_orders SET status_master_id = CASE WHEN status IN (''COMPLETED'', ''FINISH'') OR completed_at IS NOT NULL THEN 3 WHEN status IN (''IN_PROGRESS'', ''HOLD'') OR started_at IS NOT NULL THEN 2 ELSE 1 END',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE production_work_orders
SET status_master_id = CASE
    WHEN status_master_id IN (3, 105) OR completed_at IS NOT NULL THEN 3
    WHEN status_master_id IN (2, 103, 104) OR started_at IS NOT NULL THEN 2
    ELSE 1
END;

ALTER TABLE production_work_orders MODIFY status_master_id INT NOT NULL;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'status') = 1
    AND (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_status_line' AND COLUMN_NAME = 'status') > 0,
    'ALTER TABLE production_work_orders DROP INDEX ix_production_work_orders_status_line',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_status_line') = 0,
    'ALTER TABLE production_work_orders ADD INDEX ix_production_work_orders_status_line (status_master_id, line_code)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_work_orders_status_master') = 0,
    'ALTER TABLE production_work_orders ADD CONSTRAINT fk_production_work_orders_status_master FOREIGN KEY (status_master_id) REFERENCES status_masters(id)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'status') = 1,
    'ALTER TABLE production_work_orders DROP COLUMN status',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
