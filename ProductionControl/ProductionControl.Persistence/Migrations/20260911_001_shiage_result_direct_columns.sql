-- Store the Shiage endpoint fields used by production scan/reporting directly on RPO details.
-- Source fields: PROJECT_NO, ORDER_NO, LOT_NO, WEIGHT.

SET @add_order_no = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'release_production_order_details' AND COLUMN_NAME = 'order_no') = 0,
    'ALTER TABLE release_production_order_details ADD COLUMN order_no VARCHAR(80) NULL AFTER production_work_order_id',
    'SELECT 1'
);
PREPARE stmt FROM @add_order_no;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @add_project_no = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'release_production_order_details' AND COLUMN_NAME = 'project_no') = 0,
    'ALTER TABLE release_production_order_details ADD COLUMN project_no VARCHAR(80) NULL AFTER lot_no',
    'SELECT 1'
);
PREPARE stmt FROM @add_project_no;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE release_production_order_details rpod
JOIN production_work_orders pwo ON pwo.id = rpod.production_work_order_id
SET rpod.order_no = pwo.order_number
WHERE rpod.order_no IS NULL OR rpod.order_no = '';

UPDATE release_production_order_details rpod
JOIN project_master pm ON pm.id = rpod.project_master_id
SET rpod.project_no = pm.project_no
WHERE rpod.project_no IS NULL OR rpod.project_no = '';

SET @add_order_no_index = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'release_production_order_details' AND INDEX_NAME = 'ix_rpo_details_order_no') = 0,
    'ALTER TABLE release_production_order_details ADD INDEX ix_rpo_details_order_no (order_no)',
    'SELECT 1'
);
PREPARE stmt FROM @add_order_no_index;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @add_project_no_index = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'release_production_order_details' AND INDEX_NAME = 'ix_rpo_details_project_no') = 0,
    'ALTER TABLE release_production_order_details ADD INDEX ix_rpo_details_project_no (project_no)',
    'SELECT 1'
);
PREPARE stmt FROM @add_project_no_index;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
