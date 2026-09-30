-- Dummy data for waiting work orders and multi-operator history.
-- Prefixes:
--   CL-RPT-WAIT-* = not-started cutting lists
--   WO-RPT-WAIT-* = not-started work orders
--   CL-RPT-TEAM-* = finished history with more than one operator
--   WO-RPT-TEAM-* = finished history with more than one operator

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

DELETE pwoo
FROM production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%'
   OR pwo.order_number LIKE 'WO-RPT-TEAM-%';

DELETE pwos
FROM production_work_order_operator_snapshots pwos
JOIN production_work_orders pwo ON pwo.id = pwos.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%'
   OR pwo.order_number LIKE 'WO-RPT-TEAM-%';

DELETE pal
FROM production_activity_logs pal
JOIN production_work_orders pwo ON pwo.id = pal.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%'
   OR pwo.order_number LIKE 'WO-RPT-TEAM-%';

DELETE rpod
FROM release_production_order_details rpod
JOIN production_work_orders pwo ON pwo.id = rpod.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%'
   OR pwo.order_number LIKE 'WO-RPT-TEAM-%';

DELETE FROM production_work_orders
WHERE order_number LIKE 'WO-RPT-WAIT-%'
   OR order_number LIKE 'WO-RPT-TEAM-%';

DELETE FROM cutting_lists
WHERE cutting_list_no LIKE 'CL-RPT-WAIT-%'
   OR cutting_list_no LIKE 'CL-RPT-TEAM-%';

INSERT INTO cutting_lists
    (cutting_list_no, product_code, product_name, line_code, planned_qty, unit, plan_date, status_master_id, created_at)
VALUES
    (CONCAT('CL-RPT-WAIT-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d'), '-01'), 'RPT-WAIT-001', 'Waiting Dummy Window Frame', 'LINE-08', 64, 'PCS', CURRENT_DATE(), 1, NOW()),
    (CONCAT('CL-RPT-WAIT-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d'), '-02'), 'RPT-WAIT-002', 'Waiting Dummy Sliding Door', 'LINE-09', 42, 'PCS', CURRENT_DATE(), 1, DATE_SUB(NOW(), INTERVAL 12 MINUTE)),
    (CONCAT('CL-RPT-TEAM-', DATE_FORMAT(DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY), '%Y%m%d'), '-01'), 'RPT-TEAM-001', 'Team Dummy Multi Operator Frame', 'LINE-10', 118, 'PCS', DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY), 3, TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY), '07:05:00')),
    (CONCAT('CL-RPT-TEAM-', DATE_FORMAT(DATE_SUB(CURRENT_DATE(), INTERVAL 2 DAY), '%Y%m%d'), '-02'), 'RPT-TEAM-002', 'Team Dummy Multi Operator Sash', 'LINE-11', 96, 'PCS', DATE_SUB(CURRENT_DATE(), INTERVAL 2 DAY), 3, TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 2 DAY), '15:10:00'));

INSERT INTO production_work_orders
    (order_number, cutting_list_id, pic_card_id, shift_master_id, area_master_id, line_code, target_qty, actual_qty, reject_qty,
     started_at, completed_at, created_at, updated_at, status_master_id)
SELECT
    REPLACE(cl.cutting_list_no, 'CL-RPT-', 'WO-RPT-'),
    cl.id,
    NULL,
    NULL,
    NULL,
    cl.line_code,
    cl.planned_qty,
    0,
    0,
    NULL,
    NULL,
    cl.created_at,
    cl.created_at,
    1
FROM cutting_lists cl
WHERE cl.cutting_list_no LIKE CONCAT('CL-RPT-WAIT-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d'), '-%');

INSERT INTO production_work_orders
    (order_number, cutting_list_id, pic_card_id, shift_master_id, area_master_id, line_code, target_qty, actual_qty, reject_qty,
     started_at, completed_at, created_at, updated_at, status_master_id)
SELECT
    REPLACE(cl.cutting_list_no, 'CL-RPT-', 'WO-RPT-'),
    cl.id,
    om.id,
    sm.id,
    am.id,
    cl.line_code,
    cl.planned_qty,
    CASE WHEN cl.product_code = 'RPT-TEAM-001' THEN 116 ELSE 93 END,
    CASE WHEN cl.product_code = 'RPT-TEAM-001' THEN 2 ELSE 1 END,
    CASE WHEN cl.product_code = 'RPT-TEAM-001'
        THEN TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY), '07:20:00')
        ELSE TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 2 DAY), '15:25:00')
    END,
    CASE WHEN cl.product_code = 'RPT-TEAM-001'
        THEN TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY), '10:45:00')
        ELSE TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 2 DAY), '18:20:00')
    END,
    cl.created_at,
    CASE WHEN cl.product_code = 'RPT-TEAM-001'
        THEN TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 1 DAY), '10:45:00')
        ELSE TIMESTAMP(DATE_SUB(CURRENT_DATE(), INTERVAL 2 DAY), '18:20:00')
    END,
    3
FROM cutting_lists cl
JOIN operator_master om ON om.card_uid = 'YKK-RPT-0001'
JOIN shift_masters sm ON sm.shift_code = CASE WHEN cl.product_code = 'RPT-TEAM-001' THEN 'SHIFT_1' ELSE 'SHIFT_2' END
LEFT JOIN area_master am ON am.area_code = CASE WHEN cl.product_code = 'RPT-TEAM-001' THEN 'ASSY_OUTER' ELSE 'PROCESS_INNER' END
WHERE cl.cutting_list_no LIKE 'CL-RPT-TEAM-%';

INSERT INTO production_work_order_operators
    (production_work_order_id, pic_card_id, production_active_operator_id, shift_master_id, is_active, scanned_at, removed_at)
SELECT
    pwo.id,
    om.id,
    NULL,
    pwo.shift_master_id,
    0,
    DATE_SUB(pwo.started_at, INTERVAL seq.scan_minute MINUTE),
    pwo.completed_at
FROM production_work_orders pwo
JOIN (
    SELECT 'YKK-RPT-0001' AS card_uid, 15 AS scan_minute
    UNION ALL SELECT 'YKK-RPT-0002', 12
    UNION ALL SELECT 'YKK-RPT-0003', 9
) seq
JOIN operator_master om ON om.card_uid = seq.card_uid
WHERE pwo.order_number LIKE 'WO-RPT-TEAM-%';

INSERT INTO production_work_order_operator_snapshots
    (production_work_order_id, pic_card_id, production_active_operator_id, shift_master_id, snapshot_type, scanned_at, snapshot_at, created_at)
SELECT
    pwo.id,
    om.id,
    NULL,
    pwo.shift_master_id,
    snap.snapshot_type,
    DATE_SUB(pwo.started_at, INTERVAL seq.scan_minute MINUTE),
    CASE WHEN snap.snapshot_type = 'START' THEN pwo.started_at ELSE pwo.completed_at END,
    CASE WHEN snap.snapshot_type = 'START' THEN pwo.started_at ELSE pwo.completed_at END
FROM production_work_orders pwo
JOIN (
    SELECT 'YKK-RPT-0001' AS card_uid, 15 AS scan_minute
    UNION ALL SELECT 'YKK-RPT-0002', 12
    UNION ALL SELECT 'YKK-RPT-0003', 9
) seq
JOIN operator_master om ON om.card_uid = seq.card_uid
JOIN (
    SELECT 'START' AS snapshot_type
    UNION ALL SELECT 'FINISH'
) snap
WHERE pwo.order_number LIKE 'WO-RPT-TEAM-%';

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    NULL,
    'CUTTING_LIST_SCAN',
    CONCAT('[DUMMY-WAIT] Order is waiting and has not been started: ', cl.cutting_list_no),
    pwo.created_at
FROM production_work_orders pwo
JOIN cutting_lists cl ON cl.id = pwo.cutting_list_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%';

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    om.id,
    'PIC_SCAN',
    CONCAT('[DUMMY-TEAM] Operator ', om.employee_no, ' - ', om.full_name),
    DATE_SUB(pwo.started_at, INTERVAL seq.scan_minute MINUTE)
FROM production_work_orders pwo
JOIN (
    SELECT 'YKK-RPT-0001' AS card_uid, 15 AS scan_minute
    UNION ALL SELECT 'YKK-RPT-0002', 12
    UNION ALL SELECT 'YKK-RPT-0003', 9
) seq
JOIN operator_master om ON om.card_uid = seq.card_uid
WHERE pwo.order_number LIKE 'WO-RPT-TEAM-%';

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    pwo.pic_card_id,
    'WORK_START',
    CONCAT('[DUMMY-TEAM] Production started with 3 operators at ', COALESCE(am.area_name, '-')),
    pwo.started_at
FROM production_work_orders pwo
LEFT JOIN area_master am ON am.id = pwo.area_master_id
WHERE pwo.order_number LIKE 'WO-RPT-TEAM-%';

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    pwo.pic_card_id,
    'PRODUCTION_UPDATE',
    CONCAT('[DUMMY-TEAM] Output update actual ', pwo.actual_qty, ', reject ', pwo.reject_qty),
    DATE_SUB(pwo.completed_at, INTERVAL 20 MINUTE)
FROM production_work_orders pwo
WHERE pwo.order_number LIKE 'WO-RPT-TEAM-%';

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    pwo.pic_card_id,
    'WORK_COMPLETE',
    CONCAT('[DUMMY-TEAM] Production finished with 3 operators. Actual ', pwo.actual_qty, ', reject ', pwo.reject_qty),
    pwo.completed_at
FROM production_work_orders pwo
WHERE pwo.order_number LIKE 'WO-RPT-TEAM-%';

INSERT INTO production_line_master (line_code, line_name, is_active)
VALUES
    ('WAIT-01', 'Waiting Dummy Line 01', 1),
    ('WAIT-02', 'Waiting Dummy Line 02', 1),
    ('TEAM-01', 'Team Dummy Line 01', 1),
    ('TEAM-02', 'Team Dummy Line 02', 1)
ON DUPLICATE KEY UPDATE
    line_name = VALUES(line_name),
    is_active = 1,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO project_master (project_no, project_name, is_active)
VALUES
    ('WAIT-PRJ-001', 'WAITING ORDER SAMPLE PROJECT', 1),
    ('WAIT-PRJ-002', 'WAITING DOOR SAMPLE PROJECT', 1),
    ('TEAM-PRJ-001', 'MULTI OPERATOR FRAME PROJECT', 1),
    ('TEAM-PRJ-002', 'MULTI OPERATOR SASH PROJECT', 1)
ON DUPLICATE KEY UPDATE
    project_name = VALUES(project_name),
    is_active = 1,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO release_production_order_details
    (production_work_order_id, lot_no, production_line_master_id, project_master_id, weight, created_at, updated_at)
SELECT
    pwo.id,
    CASE
        WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-01' THEN 'WAIT260801X001'
        WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-02' THEN 'WAIT260801X002'
        WHEN pwo.order_number LIKE 'WO-RPT-TEAM-%-01' THEN 'TEAM260801X101'
        ELSE 'TEAM260801X102'
    END,
    plm.id,
    pm.id,
    CASE
        WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-01' THEN 3.420
        WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-02' THEN 5.125
        WHEN pwo.order_number LIKE 'WO-RPT-TEAM-%-01' THEN 8.735
        ELSE 7.280
    END,
    pwo.created_at,
    CURRENT_TIMESTAMP
FROM production_work_orders pwo
JOIN production_line_master plm ON plm.line_code = CASE
    WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-01' THEN 'WAIT-01'
    WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-02' THEN 'WAIT-02'
    WHEN pwo.order_number LIKE 'WO-RPT-TEAM-%-01' THEN 'TEAM-01'
    ELSE 'TEAM-02'
END
JOIN project_master pm ON pm.project_no = CASE
    WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-01' THEN 'WAIT-PRJ-001'
    WHEN pwo.order_number LIKE 'WO-RPT-WAIT-%-02' THEN 'WAIT-PRJ-002'
    WHEN pwo.order_number LIKE 'WO-RPT-TEAM-%-01' THEN 'TEAM-PRJ-001'
    ELSE 'TEAM-PRJ-002'
END
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%'
   OR pwo.order_number LIKE 'WO-RPT-TEAM-%'
ON DUPLICATE KEY UPDATE
    lot_no = VALUES(lot_no),
    production_line_master_id = VALUES(production_line_master_id),
    project_master_id = VALUES(project_master_id),
    weight = VALUES(weight),
    updated_at = CURRENT_TIMESTAMP;
