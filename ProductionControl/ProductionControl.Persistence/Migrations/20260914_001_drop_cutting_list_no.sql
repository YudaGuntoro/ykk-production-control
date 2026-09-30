-- Remove cutting list number from production history storage.
SET @drop_cutting_list_no_index := IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND INDEX_NAME = 'uq_cutting_lists_no') = 1,
    'ALTER TABLE cutting_lists DROP INDEX uq_cutting_lists_no',
    'SELECT 1'
);
PREPARE stmt FROM @drop_cutting_list_no_index;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @drop_cutting_list_no := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND COLUMN_NAME = 'cutting_list_no') = 1,
    'ALTER TABLE cutting_lists DROP COLUMN cutting_list_no',
    'SELECT 1'
);
PREPARE stmt FROM @drop_cutting_list_no;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
