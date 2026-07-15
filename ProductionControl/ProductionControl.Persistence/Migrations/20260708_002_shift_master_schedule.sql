-- Production Control - normalize shift master schedule columns.

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shift_masters' AND COLUMN_NAME = 'shift_type') = 0,
    'ALTER TABLE shift_masters ADD COLUMN shift_type VARCHAR(30) NULL AFTER shift_name',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shift_masters' AND COLUMN_NAME = 'start_schedule') = 0,
    'ALTER TABLE shift_masters ADD COLUMN start_schedule TIME NULL AFTER shift_type',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shift_masters' AND COLUMN_NAME = 'finish_schedule') = 0,
    'ALTER TABLE shift_masters ADD COLUMN finish_schedule TIME NULL AFTER start_schedule',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE shift_masters
SET shift_type = CASE shift_code
        WHEN 'SHIFT_1' THEN 'Day'
        WHEN 'SHIFT_2' THEN 'Middle'
        WHEN 'SHIFT_3' THEN 'Night'
        ELSE NULL
    END,
    start_schedule = CASE shift_code
        WHEN 'SHIFT_1' THEN '07:00:00'
        WHEN 'SHIFT_2' THEN '15:00:00'
        WHEN 'SHIFT_3' THEN '23:00:00'
        ELSE NULL
    END,
    finish_schedule = CASE shift_code
        WHEN 'SHIFT_1' THEN '15:00:00'
        WHEN 'SHIFT_2' THEN '23:00:00'
        WHEN 'SHIFT_3' THEN '07:00:00'
        ELSE NULL
    END
WHERE shift_code IN ('SHIFT_1', 'SHIFT_2', 'SHIFT_3', 'LONG_SHIFT_1', 'LONG_SHIFT_2');

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shift_masters' AND INDEX_NAME = 'ix_shift_masters_sort_order') > 0,
    'ALTER TABLE shift_masters DROP INDEX ix_shift_masters_sort_order',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shift_masters' AND COLUMN_NAME = 'sort_order') > 0,
    'ALTER TABLE shift_masters DROP COLUMN sort_order',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
