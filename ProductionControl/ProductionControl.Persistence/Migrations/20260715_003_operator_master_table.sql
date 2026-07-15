-- Production Control - rename PIC card master table to operator_master.

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'operator_master') = 0
    AND (SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'pic_cards') = 1,
    'RENAME TABLE pic_cards TO operator_master',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'operator_master' AND INDEX_NAME = 'uq_pic_cards_card_uid') > 0
    AND (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'operator_master' AND INDEX_NAME = 'uq_operator_master_card_uid') = 0,
    'ALTER TABLE operator_master RENAME INDEX uq_pic_cards_card_uid TO uq_operator_master_card_uid',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'operator_master' AND INDEX_NAME = 'uq_pic_cards_employee_no') > 0
    AND (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'operator_master' AND INDEX_NAME = 'uq_operator_master_employee_no') = 0,
    'ALTER TABLE operator_master RENAME INDEX uq_pic_cards_employee_no TO uq_operator_master_employee_no',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
