-- Production Control - active operator team and shift-based WO snapshot.

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND COLUMN_NAME = 'shift_master_id') = 0,
    'ALTER TABLE production_work_orders ADD COLUMN shift_master_id INT NULL AFTER pic_card_id',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND INDEX_NAME = 'ix_production_work_orders_shift_master') = 0,
    'ALTER TABLE production_work_orders ADD INDEX ix_production_work_orders_shift_master (shift_master_id)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_orders' AND CONSTRAINT_NAME = 'fk_production_wo_shift') = 0,
    'ALTER TABLE production_work_orders ADD CONSTRAINT fk_production_wo_shift FOREIGN KEY (shift_master_id) REFERENCES shift_masters(id) ON DELETE SET NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

CREATE TABLE IF NOT EXISTS production_active_operators (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    pic_card_id INT NOT NULL,
    shift_master_id INT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    scanned_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    removed_at DATETIME NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    KEY ix_production_active_operators_active (is_active, scanned_at),
    KEY ix_production_active_operators_pic_active (pic_card_id, is_active),
    KEY ix_production_active_operators_shift (shift_master_id),
    CONSTRAINT fk_production_active_operators_pic FOREIGN KEY (pic_card_id) REFERENCES operator_master(id),
    CONSTRAINT fk_production_active_operators_shift FOREIGN KEY (shift_master_id) REFERENCES shift_masters(id) ON DELETE SET NULL
);

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_order_operators' AND COLUMN_NAME = 'production_active_operator_id') = 0,
    'ALTER TABLE production_work_order_operators ADD COLUMN production_active_operator_id BIGINT NULL AFTER pic_card_id',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_order_operators' AND COLUMN_NAME = 'shift_master_id') = 0,
    'ALTER TABLE production_work_order_operators ADD COLUMN shift_master_id INT NULL AFTER production_active_operator_id',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_order_operators' AND INDEX_NAME = 'ix_production_wo_operators_active_operator') = 0,
    'ALTER TABLE production_work_order_operators ADD INDEX ix_production_wo_operators_active_operator (production_active_operator_id)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_order_operators' AND INDEX_NAME = 'ix_production_wo_operators_shift') = 0,
    'ALTER TABLE production_work_order_operators ADD INDEX ix_production_wo_operators_shift (shift_master_id)',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_order_operators' AND CONSTRAINT_NAME = 'fk_production_wo_operators_active_operator') = 0,
    'ALTER TABLE production_work_order_operators ADD CONSTRAINT fk_production_wo_operators_active_operator FOREIGN KEY (production_active_operator_id) REFERENCES production_active_operators(id) ON DELETE SET NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @sql = IF(
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND TABLE_NAME = 'production_work_order_operators' AND CONSTRAINT_NAME = 'fk_production_wo_operators_shift') = 0,
    'ALTER TABLE production_work_order_operators ADD CONSTRAINT fk_production_wo_operators_shift FOREIGN KEY (shift_master_id) REFERENCES shift_masters(id) ON DELETE SET NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE production_work_orders pwo
JOIN shift_masters sm
    ON sm.is_active = 1
    AND sm.start_schedule IS NOT NULL
    AND sm.finish_schedule IS NOT NULL
    AND (
        (sm.start_schedule < sm.finish_schedule AND TIME(pwo.started_at) >= sm.start_schedule AND TIME(pwo.started_at) < sm.finish_schedule)
        OR (sm.start_schedule > sm.finish_schedule AND (TIME(pwo.started_at) >= sm.start_schedule OR TIME(pwo.started_at) < sm.finish_schedule))
        OR sm.start_schedule = sm.finish_schedule
    )
SET pwo.shift_master_id = sm.id
WHERE pwo.shift_master_id IS NULL
  AND pwo.started_at IS NOT NULL;

UPDATE production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
SET pwoo.shift_master_id = pwo.shift_master_id
WHERE pwoo.shift_master_id IS NULL
  AND pwo.shift_master_id IS NOT NULL;

INSERT INTO production_active_operators (pic_card_id, shift_master_id, is_active, scanned_at, created_at, updated_at)
SELECT
    pwoo.pic_card_id,
    MAX(pwoo.shift_master_id),
    1,
    MAX(pwoo.scanned_at),
    NOW(),
    NOW()
FROM production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
WHERE pwoo.is_active = 1
  AND pwo.status IN ('WAITING', 'READY', 'IN_PROGRESS', 'HOLD')
  AND NOT EXISTS (
      SELECT 1
      FROM production_active_operators pao
      WHERE pao.pic_card_id = pwoo.pic_card_id
        AND pao.is_active = 1
  )
GROUP BY pwoo.pic_card_id;
