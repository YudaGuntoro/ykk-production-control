-- Additional not-started work order dummy data.
-- Prefixes:
--   CL-RPT-WAIT-MORE-* = not-started cutting lists
--   WO-RPT-WAIT-MORE-* = not-started work orders

DELETE pwoo
FROM production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-MORE-%';

DELETE pwos
FROM production_work_order_operator_snapshots pwos
JOIN production_work_orders pwo ON pwo.id = pwos.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-MORE-%';

DELETE pal
FROM production_activity_logs pal
JOIN production_work_orders pwo ON pwo.id = pal.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-MORE-%';

DELETE rpod
FROM release_production_order_details rpod
JOIN production_work_orders pwo ON pwo.id = rpod.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-MORE-%';

DELETE FROM production_work_orders
WHERE order_number LIKE 'WO-RPT-WAIT-MORE-%';

DELETE FROM cutting_lists
WHERE cutting_list_no LIKE 'CL-RPT-WAIT-MORE-%';

INSERT INTO cutting_lists
    (cutting_list_no, product_code, product_name, line_code, planned_qty, unit, plan_date, status_master_id, created_at)
VALUES
    (CONCAT('CL-RPT-WAIT-MORE-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d'), '-01'), 'RPT-WAIT-003', 'Waiting Dummy Curtain Wall Panel', 'LINE-12', 38, 'PCS', CURRENT_DATE(), 1, DATE_SUB(NOW(), INTERVAL 5 MINUTE)),
    (CONCAT('CL-RPT-WAIT-MORE-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d'), '-02'), 'RPT-WAIT-004', 'Waiting Dummy Ventilation Frame', 'LINE-13', 72, 'PCS', CURRENT_DATE(), 1, DATE_SUB(NOW(), INTERVAL 18 MINUTE)),
    (CONCAT('CL-RPT-WAIT-MORE-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d'), '-03'), 'RPT-WAIT-005', 'Waiting Dummy Fixed Window', 'LINE-14', 55, 'PCS', CURRENT_DATE(), 1, DATE_SUB(NOW(), INTERVAL 31 MINUTE)),
    (CONCAT('CL-RPT-WAIT-MORE-', DATE_FORMAT(CURRENT_DATE(), '%Y%m%d'), '-04'), 'RPT-WAIT-006', 'Waiting Dummy Door Rail Set', 'LINE-15', 44, 'SET', CURRENT_DATE(), 1, DATE_SUB(NOW(), INTERVAL 44 MINUTE)),
    (CONCAT('CL-RPT-WAIT-MORE-', DATE_FORMAT(DATE_ADD(CURRENT_DATE(), INTERVAL 1 DAY), '%Y%m%d'), '-05'), 'RPT-WAIT-007', 'Waiting Dummy Tomorrow Sash', 'LINE-16', 88, 'PCS', DATE_ADD(CURRENT_DATE(), INTERVAL 1 DAY), 1, DATE_SUB(NOW(), INTERVAL 57 MINUTE));

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
WHERE cl.cutting_list_no LIKE 'CL-RPT-WAIT-MORE-%';

INSERT INTO production_line_master (line_code, line_name, is_active)
VALUES
    ('WAIT-03', 'Waiting Dummy Line 03', 1),
    ('WAIT-04', 'Waiting Dummy Line 04', 1),
    ('WAIT-05', 'Waiting Dummy Line 05', 1),
    ('WAIT-06', 'Waiting Dummy Line 06', 1),
    ('WAIT-07', 'Waiting Dummy Line 07', 1)
ON DUPLICATE KEY UPDATE
    line_name = VALUES(line_name),
    is_active = 1,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO project_master (project_no, project_name, is_active)
VALUES
    ('WAIT-PRJ-003', 'WAITING CURTAIN WALL PROJECT', 1),
    ('WAIT-PRJ-004', 'WAITING VENTILATION PROJECT', 1),
    ('WAIT-PRJ-005', 'WAITING FIXED WINDOW PROJECT', 1),
    ('WAIT-PRJ-006', 'WAITING DOOR RAIL PROJECT', 1),
    ('WAIT-PRJ-007', 'WAITING TOMORROW SASH PROJECT', 1)
ON DUPLICATE KEY UPDATE
    project_name = VALUES(project_name),
    is_active = 1,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO release_production_order_details
    (production_work_order_id, lot_no, production_line_master_id, project_master_id, weight, created_at, updated_at)
SELECT
    pwo.id,
    CASE
        WHEN pwo.order_number LIKE '%-01' THEN 'WAIT260801X003'
        WHEN pwo.order_number LIKE '%-02' THEN 'WAIT260801X004'
        WHEN pwo.order_number LIKE '%-03' THEN 'WAIT260801X005'
        WHEN pwo.order_number LIKE '%-04' THEN 'WAIT260801X006'
        ELSE 'WAIT260801X007'
    END,
    plm.id,
    pm.id,
    CASE
        WHEN pwo.order_number LIKE '%-01' THEN 4.210
        WHEN pwo.order_number LIKE '%-02' THEN 2.875
        WHEN pwo.order_number LIKE '%-03' THEN 3.665
        WHEN pwo.order_number LIKE '%-04' THEN 6.430
        ELSE 5.980
    END,
    pwo.created_at,
    CURRENT_TIMESTAMP
FROM production_work_orders pwo
JOIN production_line_master plm ON plm.line_code = CASE
    WHEN pwo.order_number LIKE '%-01' THEN 'WAIT-03'
    WHEN pwo.order_number LIKE '%-02' THEN 'WAIT-04'
    WHEN pwo.order_number LIKE '%-03' THEN 'WAIT-05'
    WHEN pwo.order_number LIKE '%-04' THEN 'WAIT-06'
    ELSE 'WAIT-07'
END
JOIN project_master pm ON pm.project_no = CASE
    WHEN pwo.order_number LIKE '%-01' THEN 'WAIT-PRJ-003'
    WHEN pwo.order_number LIKE '%-02' THEN 'WAIT-PRJ-004'
    WHEN pwo.order_number LIKE '%-03' THEN 'WAIT-PRJ-005'
    WHEN pwo.order_number LIKE '%-04' THEN 'WAIT-PRJ-006'
    ELSE 'WAIT-PRJ-007'
END
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-MORE-%'
ON DUPLICATE KEY UPDATE
    lot_no = VALUES(lot_no),
    production_line_master_id = VALUES(production_line_master_id),
    project_master_id = VALUES(project_master_id),
    weight = VALUES(weight),
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO production_activity_logs
    (production_work_order_id, pic_card_id, activity_type, remarks, created_at)
SELECT
    pwo.id,
    NULL,
    'CUTTING_LIST_SCAN',
    CONCAT('[DUMMY-WAIT-MORE] Order is waiting and has not been started: ', cl.cutting_list_no),
    pwo.created_at
FROM production_work_orders pwo
JOIN cutting_lists cl ON cl.id = pwo.cutting_list_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-MORE-%';

DELETE pwoo
FROM production_work_order_operators pwoo
JOIN production_work_orders pwo ON pwo.id = pwoo.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%';

DELETE pwos
FROM production_work_order_operator_snapshots pwos
JOIN production_work_orders pwo ON pwo.id = pwos.production_work_order_id
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%';

UPDATE production_work_orders
SET
    pic_card_id = NULL,
    shift_master_id = NULL,
    area_master_id = NULL,
    actual_qty = 0,
    reject_qty = 0,
    started_at = NULL,
    completed_at = NULL,
    status_master_id = 1,
    updated_at = created_at
WHERE order_number LIKE 'WO-RPT-WAIT-%';

UPDATE cutting_lists cl
JOIN production_work_orders pwo ON pwo.cutting_list_id = cl.id
SET cl.status_master_id = 1
WHERE pwo.order_number LIKE 'WO-RPT-WAIT-%';
