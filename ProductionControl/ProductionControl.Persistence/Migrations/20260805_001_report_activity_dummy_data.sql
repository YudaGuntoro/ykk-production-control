-- Dynamic dummy data for Production Activity report and export preview.
-- Prefixes:
--   CL-RPT-* = dummy cutting list
--   WO-RPT-* = dummy work order
--   [DUMMY-RPT] = dummy activity log remarks

INSERT INTO shift_masters (shift_code, shift_name, shift_type, start_schedule, finish_schedule, is_active)
VALUES
    ('SHIFT_1', 'Shift 1', 'Day', '07:00:00', '15:00:00', 1),
    ('SHIFT_2', 'Shift 2', 'Middle', '15:00:00', '23:00:00', 1),
    ('SHIFT_3', 'Shift 3', 'Night', '23:00:00', '07:00:00', 1)
ON DUPLICATE KEY UPDATE
    shift_name = VALUES(shift_name),
    shift_type = VALUES(shift_type),
    start_schedule = VALUES(start_schedule),
    finish_schedule = VALUES(finish_schedule),
    is_active = VALUES(is_active);

INSERT INTO area_master
    (area_code, area_name, description, is_active)
VALUES
    ('CUTTING_OUTER', 'Cutting Outer', 'Dummy report area: Cutting Outer.', 1),
    ('PROCESS_OUTER', 'Process Outer', 'Dummy report area: Process Outer.', 1),
    ('PARTS_OUTER', 'Parts Outer', 'Dummy report area: Parts Outer.', 1),
    ('ASSY_OUTER', 'Assy Outer', 'Dummy report area: Assy Outer.', 1),
    ('CUTTING_INNER', 'Cutting Inner', 'Dummy report area: Cutting Inner.', 1),
    ('PROCESS_INNER', 'Process Inner', 'Dummy report area: Process Inner.', 1),
    ('PARTS_ASSY_INNER', 'Parts & Assy Inner', 'Dummy report area: Parts & Assy Inner.', 1)
ON DUPLICATE KEY UPDATE
    area_name = VALUES(area_name),
    description = VALUES(description),
    is_active = VALUES(is_active);

INSERT INTO operator_master
    (card_uid, employee_no, full_name, department, shift, is_active)
VALUES
    ('YKK-RPT-0001', 'RPT001', 'Rina Kusuma', 'Production', 'Shift 1', 1),
    ('YKK-RPT-0002', 'RPT002', 'Dimas Prakoso', 'Production', 'Shift 2', 1),
    ('YKK-RPT-0003', 'RPT003', 'Maya Lestari', 'Production', 'Shift 3', 1)
ON DUPLICATE KEY UPDATE
    full_name = VALUES(full_name),
    department = VALUES(department),
    shift = VALUES(shift),
    is_active = VALUES(is_active);

DROP TEMPORARY TABLE IF EXISTS report_dummy_days;
DROP TEMPORARY TABLE IF EXISTS report_dummy_slots;
DROP TEMPORARY TABLE IF EXISTS report_dummy_orders;

CREATE TEMPORARY TABLE report_dummy_days (
    day_offset INT NOT NULL PRIMARY KEY
);

INSERT INTO report_dummy_days (day_offset)
VALUES (7), (6), (5), (4), (3), (2), (1), (0);

CREATE TEMPORARY TABLE report_dummy_slots (
    slot_no INT NOT NULL PRIMARY KEY,
    shift_code VARCHAR(50) NOT NULL,
    line_code VARCHAR(50) NOT NULL,
    area_code VARCHAR(50) NOT NULL,
    product_code VARCHAR(80) NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    start_time TIME NOT NULL,
    complete_time TIME NOT NULL,
    base_actual INT NOT NULL,
    base_reject INT NOT NULL,
    operator_card_uid VARCHAR(80) NOT NULL
);

INSERT INTO report_dummy_slots
    (slot_no, shift_code, line_code, area_code, product_code, product_name, start_time, complete_time, base_actual, base_reject, operator_card_uid)
VALUES
    (1, 'SHIFT_1', 'LINE-01', 'CUTTING_OUTER', 'RPT-AL-001', 'Report Dummy Frame A', '07:10:00', '08:25:00', 76, 1, 'YKK-RPT-0001'),
    (2, 'SHIFT_1', 'LINE-02', 'PROCESS_OUTER', 'RPT-AL-002', 'Report Dummy Sash A', '08:40:00', '10:05:00', 92, 0, 'YKK-RPT-0001'),
    (3, 'SHIFT_1', 'LINE-03', 'PARTS_OUTER', 'RPT-AL-003', 'Report Dummy Handle A', '10:25:00', '12:10:00', 84, 2, 'YKK-RPT-0002'),
    (4, 'SHIFT_2', 'LINE-04', 'ASSY_OUTER', 'RPT-AL-004', 'Report Dummy Rail A', '15:15:00', '16:55:00', 101, 1, 'YKK-RPT-0002'),
    (5, 'SHIFT_2', 'LINE-05', 'CUTTING_INNER', 'RPT-AL-005', 'Report Dummy Panel A', '17:20:00', '19:05:00', 88, 0, 'YKK-RPT-0002'),
    (6, 'SHIFT_3', 'LINE-06', 'PROCESS_INNER', 'RPT-AL-006', 'Report Dummy Seal A', '22:20:00', '23:40:00', 67, 1, 'YKK-RPT-0003');

CREATE TEMPORARY TABLE report_dummy_orders (
    work_date DATE NOT NULL,
    slot_no INT NOT NULL,
    cutting_list_no VARCHAR(80) NOT NULL,
    order_number VARCHAR(80) NOT NULL,
    shift_code VARCHAR(50) NOT NULL,
    line_code VARCHAR(50) NOT NULL,
    area_code VARCHAR(50) NOT NULL,
    product_code VARCHAR(80) NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    planned_qty INT NOT NULL,
    actual_qty INT NOT NULL,
    reject_qty INT NOT NULL,
    status_master_id INT NOT NULL,
    operator_card_uid VARCHAR(80) NOT NULL,
    started_at DATETIME NOT NULL,
    completed_at DATETIME NULL,
    updated_at DATETIME NOT NULL,
    PRIMARY KEY (order_number)
);

INSERT INTO report_dummy_orders
    (work_date, slot_no, cutting_list_no, order_number, shift_code, line_code, area_code, product_code, product_name,
     planned_qty, actual_qty, reject_qty, status_master_id, operator_card_uid, started_at, completed_at, updated_at)
SELECT
    DATE_SUB(CURRENT_DATE(), INTERVAL d.day_offset DAY),
    s.slot_no,
    CONCAT('CL-RPT-', DATE_FORMAT(DATE_SUB(CURRENT_DATE(), INTERVAL d.day_offset DAY), '%Y%m%d'), '-', LPAD(s.slot_no, 2, '0')),
    CONCAT('WO-RPT-', DATE_FORMAT(DATE_SUB(CURRENT_DATE(), INTERVAL d.day_offset DAY), '%Y%m%d'), '-', LPAD(s.slot_no, 2, '0')),
    s.shift_code,
    s.line_code,
    s.area_code,
    s.product_code,
    s.product_name,
    s.base_actual + s.base_reject + (7 - d.day_offset),
    s.base_actual + (7 - d.day_offset),
    s.base_reject,
    3,
    s.operator_card_uid,
    TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL d.day_offset DAY), s.start_time),
    TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL d.day_offset DAY), s.complete_time),
    TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL d.day_offset DAY), s.complete_time)
FROM report_dummy_days d
JOIN report_dummy_slots s
WHERE d.day_offset > 0
   OR TIMESTAMP(CURRENT_DATE(), s.complete_time) <= NOW();

INSERT INTO report_dummy_orders
    (work_date, slot_no, cutting_list_no, order_number, shift_code, line_code, area_code, product_code, product_name,
     planned_qty, actual_qty, reject_qty, status_master_id, operator_card_uid, started_at, completed_at, updated_at)
SELECT
    CURRENT_DATE(),
    99,
    CONCAT('CL-RPT-CURRENT-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d')),
    CONCAT('WO-RPT-CURRENT-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d')),
    CASE
        WHEN CURRENT_TIME() >= '15:00:00' AND CURRENT_TIME() < '23:00:00' THEN 'SHIFT_2'
        WHEN CURRENT_TIME() >= '23:00:00' OR CURRENT_TIME() < '07:00:00' THEN 'SHIFT_3'
        ELSE 'SHIFT_1'
    END,
    'LINE-07',
    'PARTS_ASSY_INNER',
    'RPT-AL-CURRENT',
    'Report Dummy Current Hour',
    120,
    45 + HOUR(NOW()),
    0,
    2,
    'YKK-RPT-0001',
    DATE_SUB(NOW(), INTERVAL 55 MINUTE),
    NULL,
    NOW();

DELETE pwoo
FROM production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-%';

DELETE pwos
FROM production_work_order_operator_snapshots pwos
JOIN production_work_orders pwo ON pwo.id = pwos.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-%';

DELETE pal
FROM production_activity_logs pal
JOIN production_work_orders pwo ON pwo.id = pal.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-%';

DELETE FROM production_work_orders
WHERE order_number LIKE 'WO-RPT-%';

DELETE FROM cutting_lists
WHERE cutting_list_no LIKE 'CL-RPT-%';

INSERT INTO cutting_lists
    (cutting_list_no, product_code, product_name, line_code, planned_qty, unit, plan_date, status_master_id, created_at)
SELECT
    cutting_list_no,
    product_code,
    product_name,
    line_code,
    planned_qty,
    'PCS',
    work_date,
    status_master_id,
    started_at
FROM report_dummy_orders;

INSERT INTO production_work_orders
    (order_number, cutting_list_id, pic_card_id, shift_master_id, area_master_id, line_code, target_qty, actual_qty, reject_qty,
     started_at, completed_at, created_at, updated_at, status_master_id)
SELECT
    d.order_number,
    cl.id,
    om.id,
    sm.id,
    am.id,
    d.line_code,
    d.planned_qty,
    d.actual_qty,
    d.reject_qty,
    d.started_at,
    d.completed_at,
    d.started_at,
    d.updated_at,
    d.status_master_id
FROM report_dummy_orders d
JOIN cutting_lists cl ON cl.cutting_list_no = d.cutting_list_no
JOIN operator_master om ON om.card_uid = d.operator_card_uid
JOIN shift_masters sm ON sm.shift_code = d.shift_code
LEFT JOIN area_master am ON am.area_code = d.area_code;

INSERT INTO production_work_order_operators
    (production_work_order_id, pic_card_id, production_active_operator_id, shift_master_id, is_active, scanned_at, removed_at)
SELECT
    pwo.id,
    om.id,
    NULL,
    sm.id,
    CASE WHEN d.completed_at IS NULL THEN 1 ELSE 0 END,
    DATE_SUB(d.started_at, INTERVAL 10 MINUTE),
    d.completed_at
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid
JOIN shift_masters sm ON sm.shift_code = d.shift_code;

INSERT INTO production_work_order_operator_snapshots
    (production_work_order_id, pic_card_id, production_active_operator_id, shift_master_id, snapshot_type, scanned_at, snapshot_at, created_at)
SELECT
    pwo.id,
    om.id,
    NULL,
    sm.id,
    'START',
    DATE_SUB(d.started_at, INTERVAL 10 MINUTE),
    d.started_at,
    d.started_at
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid
JOIN shift_masters sm ON sm.shift_code = d.shift_code;

INSERT INTO production_work_order_operator_snapshots
    (production_work_order_id, pic_card_id, production_active_operator_id, shift_master_id, snapshot_type, scanned_at, snapshot_at, created_at)
SELECT
    pwo.id,
    om.id,
    NULL,
    sm.id,
    'FINISH',
    DATE_SUB(d.started_at, INTERVAL 10 MINUTE),
    d.completed_at,
    d.completed_at
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid
JOIN shift_masters sm ON sm.shift_code = d.shift_code
WHERE d.completed_at IS NOT NULL;

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    om.id,
    'CUTTING_LIST_SCAN',
    CONCAT('[DUMMY-RPT] Cutting list scan ', d.cutting_list_no),
    DATE_SUB(d.started_at, INTERVAL 15 MINUTE)
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid;

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    om.id,
    'PIC_SCAN',
    CONCAT('[DUMMY-RPT] Operator ', om.employee_no, ' - ', om.full_name),
    DATE_SUB(d.started_at, INTERVAL 10 MINUTE)
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid;

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    om.id,
    'WORK_START',
    CONCAT('[DUMMY-RPT] Production started at ', COALESCE(am.area_name, d.area_code)),
    d.started_at
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid
LEFT JOIN area_master am ON am.area_code = d.area_code;

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    om.id,
    'PRODUCTION_UPDATE',
    CONCAT('[DUMMY-RPT] Output update actual ', d.actual_qty, ', reject ', d.reject_qty),
    CASE WHEN d.completed_at IS NULL THEN d.updated_at ELSE DATE_SUB(d.completed_at, INTERVAL 20 MINUTE) END
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid;

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    om.id,
    'WORK_COMPLETE',
    CONCAT('[DUMMY-RPT] Production finished. Actual ', d.actual_qty, ', reject ', d.reject_qty),
    d.completed_at
FROM report_dummy_orders d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
JOIN operator_master om ON om.card_uid = d.operator_card_uid
WHERE d.completed_at IS NOT NULL;

UPDATE operator_master om
JOIN (
    SELECT pic_card_id, MAX(created_at) AS last_scanned_at
    FROM production_activity_logs
    WHERE remarks LIKE '[DUMMY-RPT]%'
      AND pic_card_id IS NOT NULL
    GROUP BY pic_card_id
) x ON x.pic_card_id = om.id
SET om.last_scanned_at = x.last_scanned_at;

DROP TEMPORARY TABLE IF EXISTS report_dummy_orders;
DROP TEMPORARY TABLE IF EXISTS report_dummy_slots;
DROP TEMPORARY TABLE IF EXISTS report_dummy_days;
