-- Repair Line Master columns for databases that already have line_master with old Area Master column names.

SET @rename_area_table_sql := IF(
    (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'area_master') = 1
    AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master') = 0,
    'RENAME TABLE area_master TO line_master',
    'SELECT 1'
);
PREPARE rename_area_table_stmt FROM @rename_area_table_sql;
EXECUTE rename_area_table_stmt;
DEALLOCATE PREPARE rename_area_table_stmt;

CREATE TABLE IF NOT EXISTS line_master (
    id INT AUTO_INCREMENT PRIMARY KEY,
    line_no VARCHAR(50) NOT NULL,
    line_name VARCHAR(150) NOT NULL,
    description VARCHAR(255) NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_line_master_no (line_no),
    UNIQUE KEY uq_line_master_name (line_name),
    KEY ix_line_master_active_name (is_active, line_name)
);

SET @rename_line_no_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master' AND COLUMN_NAME = 'area_code') = 1
    AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master' AND COLUMN_NAME = 'line_no') = 0,
    'ALTER TABLE line_master CHANGE COLUMN area_code line_no VARCHAR(50) NOT NULL',
    'SELECT 1'
);
PREPARE rename_line_no_stmt FROM @rename_line_no_sql;
EXECUTE rename_line_no_stmt;
DEALLOCATE PREPARE rename_line_no_stmt;

SET @rename_line_name_sql := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master' AND COLUMN_NAME = 'area_name') = 1
    AND (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'line_master' AND COLUMN_NAME = 'line_name') = 0,
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

INSERT IGNORE INTO line_master
    (id, line_no, line_name, description, is_active)
VALUES
    (1, 'CUTTING_OUTER', 'Cutting Outer', 'Layout area: Cutting Outer.', 1),
    (2, 'PROCESS_OUTER', 'Process Outer', 'Layout area: Process Outer.', 1),
    (3, 'PARTS_OUTER', 'Parts Outer', 'Layout area: Parts Outer.', 1),
    (4, 'ASSY_OUTER', 'Assy Outer', 'Layout area: Assy Outer.', 1),
    (5, 'CUTTING_INNER', 'Cutting Inner', 'Layout area: Cutting Inner.', 1),
    (6, 'PROCESS_INNER', 'Process Inner', 'Layout area: Process Inner.', 1),
    (7, 'PARTS_ASSY_INNER', 'Parts & Assy Inner', 'Layout area: Parts & Assy Inner.', 1);
