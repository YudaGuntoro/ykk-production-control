-- Production Control - normalized operator snapshots for START and FINISH events.

CREATE TABLE IF NOT EXISTS production_work_order_operator_snapshots (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    production_work_order_id INT NOT NULL,
    pic_card_id INT NOT NULL,
    production_active_operator_id BIGINT NULL,
    shift_master_id INT NULL,
    snapshot_type VARCHAR(30) NOT NULL,
    scanned_at DATETIME NOT NULL,
    snapshot_at DATETIME NOT NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_pwo_operator_snapshots_wo_type_pic (production_work_order_id, snapshot_type, pic_card_id),
    KEY ix_pwo_operator_snapshots_wo_type (production_work_order_id, snapshot_type),
    KEY ix_pwo_operator_snapshots_pic (pic_card_id),
    KEY ix_pwo_operator_snapshots_active_operator (production_active_operator_id),
    KEY ix_pwo_operator_snapshots_shift (shift_master_id),
    CONSTRAINT fk_pwo_operator_snapshots_wo FOREIGN KEY (production_work_order_id) REFERENCES production_work_orders(id) ON DELETE CASCADE,
    CONSTRAINT fk_pwo_operator_snapshots_pic FOREIGN KEY (pic_card_id) REFERENCES operator_master(id),
    CONSTRAINT fk_pwo_operator_snapshots_active_operator FOREIGN KEY (production_active_operator_id) REFERENCES production_active_operators(id) ON DELETE SET NULL,
    CONSTRAINT fk_pwo_operator_snapshots_shift FOREIGN KEY (shift_master_id) REFERENCES shift_masters(id) ON DELETE SET NULL
);

INSERT IGNORE INTO production_work_order_operator_snapshots
    (production_work_order_id, pic_card_id, production_active_operator_id, shift_master_id, snapshot_type, scanned_at, snapshot_at, created_at)
SELECT
    pwoo.production_work_order_id,
    pwoo.pic_card_id,
    pwoo.production_active_operator_id,
    pwoo.shift_master_id,
    'START',
    pwoo.scanned_at,
    pwo.started_at,
    NOW()
FROM production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
WHERE pwo.started_at IS NOT NULL
  AND pwoo.scanned_at <= DATE_ADD(pwo.started_at, INTERVAL 1 SECOND)
  AND (pwoo.removed_at IS NULL OR pwoo.removed_at > pwo.started_at);

INSERT IGNORE INTO production_work_order_operator_snapshots
    (production_work_order_id, pic_card_id, production_active_operator_id, shift_master_id, snapshot_type, scanned_at, snapshot_at, created_at)
SELECT
    pwoo.production_work_order_id,
    pwoo.pic_card_id,
    pwoo.production_active_operator_id,
    pwoo.shift_master_id,
    'FINISH',
    pwoo.scanned_at,
    pwo.completed_at,
    NOW()
FROM production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
WHERE pwo.completed_at IS NOT NULL
  AND pwoo.scanned_at <= DATE_ADD(pwo.completed_at, INTERVAL 1 SECOND)
  AND (pwoo.removed_at IS NULL OR pwoo.removed_at > pwo.completed_at);
