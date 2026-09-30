-- Remove unused product fields from cutting list storage.
SET @drop_product_code := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND COLUMN_NAME = 'product_code') = 1,
    'ALTER TABLE cutting_lists DROP COLUMN product_code',
    'SELECT 1'
);
PREPARE stmt FROM @drop_product_code;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @drop_product_name := IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'cutting_lists' AND COLUMN_NAME = 'product_name') = 1,
    'ALTER TABLE cutting_lists DROP COLUMN product_name',
    'SELECT 1'
);
PREPARE stmt FROM @drop_product_name;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
