-- Dummy finished orders for Dashboard shift chart visibility.
-- Prefixes:
--   CL-DASH-* = dummy cutting list
--   WO-DASH-* = dummy order number

DROP TEMPORARY TABLE IF EXISTS dashboard_dummy_orders;

CREATE TEMPORARY TABLE dashboard_dummy_orders (
    work_date DATE NOT NULL,
    shift_code VARCHAR(50) NOT NULL,
    slot_no INT NOT NULL,
    line_code VARCHAR(50) NOT NULL,
    product_code VARCHAR(80) NOT NULL,
    product_name VARCHAR(200) NOT NULL,
    actual_qty INT NOT NULL,
    reject_qty INT NOT NULL,
    area_code VARCHAR(50) NOT NULL,
    started_at DATETIME NOT NULL,
    completed_at DATETIME NOT NULL
);

INSERT INTO dashboard_dummy_orders
    (work_date, shift_code, slot_no, line_code, product_code, product_name, actual_qty, reject_qty, area_code, started_at, completed_at)
VALUES
    ('2026-07-09', 'SHIFT_1', 1, 'LINE-01', 'DASH-AL-001', 'Dashboard Dummy Frame A', 120, 1, 'CUTTING_OUTER', '2026-07-09 07:20:00', '2026-07-09 14:35:00'),
    ('2026-07-09', 'SHIFT_1', 2, 'LINE-02', 'DASH-AL-002', 'Dashboard Dummy Sash A', 98, 0, 'PROCESS_OUTER', '2026-07-09 07:30:00', '2026-07-09 14:45:00'),
    ('2026-07-09', 'SHIFT_2', 1, 'LINE-03', 'DASH-AL-003', 'Dashboard Dummy Handle A', 110, 2, 'PARTS_OUTER', '2026-07-09 15:15:00', '2026-07-09 22:20:00'),
    ('2026-07-09', 'SHIFT_3', 1, 'LINE-04', 'DASH-AL-004', 'Dashboard Dummy Rail A', 86, 1, 'ASSY_OUTER', '2026-07-09 23:05:00', '2026-07-09 23:55:00'),

    ('2026-07-10', 'SHIFT_1', 1, 'LINE-01', 'DASH-AL-005', 'Dashboard Dummy Frame B', 135, 1, 'CUTTING_INNER', '2026-07-10 07:15:00', '2026-07-10 14:30:00'),
    ('2026-07-10', 'SHIFT_2', 1, 'LINE-02', 'DASH-AL-006', 'Dashboard Dummy Sash B', 122, 2, 'PROCESS_INNER', '2026-07-10 15:10:00', '2026-07-10 22:10:00'),
    ('2026-07-10', 'SHIFT_2', 2, 'LINE-03', 'DASH-AL-007', 'Dashboard Dummy Handle B', 101, 1, 'PARTS_ASSY_INNER', '2026-07-10 15:25:00', '2026-07-10 22:30:00'),
    ('2026-07-10', 'SHIFT_2', 3, 'LINE-04', 'DASH-AL-008', 'Dashboard Dummy Rail B', 94, 0, 'PROCESS_OUTER', '2026-07-10 15:35:00', '2026-07-10 22:45:00'),
    ('2026-07-10', 'SHIFT_3', 1, 'LINE-05', 'DASH-AL-009', 'Dashboard Dummy Panel B', 88, 1, 'ASSY_OUTER', '2026-07-10 23:05:00', '2026-07-10 23:45:00'),
    ('2026-07-10', 'SHIFT_3', 2, 'LINE-06', 'DASH-AL-010', 'Dashboard Dummy Seal B', 76, 0, 'PARTS_OUTER', '2026-07-10 23:15:00', '2026-07-10 23:50:00'),

    ('2026-07-13', 'SHIFT_1', 1, 'LINE-01', 'DASH-AL-011', 'Dashboard Dummy Frame C', 142, 1, 'CUTTING_OUTER', '2026-07-13 07:10:00', '2026-07-13 14:25:00'),
    ('2026-07-13', 'SHIFT_1', 2, 'LINE-02', 'DASH-AL-012', 'Dashboard Dummy Sash C', 119, 0, 'PROCESS_OUTER', '2026-07-13 07:25:00', '2026-07-13 14:35:00'),
    ('2026-07-13', 'SHIFT_1', 3, 'LINE-03', 'DASH-AL-013', 'Dashboard Dummy Handle C', 104, 2, 'PARTS_OUTER', '2026-07-13 07:35:00', '2026-07-13 14:50:00'),
    ('2026-07-13', 'SHIFT_2', 1, 'LINE-04', 'DASH-AL-014', 'Dashboard Dummy Rail C', 130, 1, 'ASSY_OUTER', '2026-07-13 15:05:00', '2026-07-13 22:25:00'),
    ('2026-07-13', 'SHIFT_2', 2, 'LINE-05', 'DASH-AL-015', 'Dashboard Dummy Panel C', 99, 0, 'CUTTING_INNER', '2026-07-13 15:20:00', '2026-07-13 22:40:00'),
    ('2026-07-13', 'SHIFT_3', 1, 'LINE-06', 'DASH-AL-016', 'Dashboard Dummy Seal C', 80, 1, 'PROCESS_INNER', '2026-07-13 23:10:00', '2026-07-13 23:55:00'),

    ('2026-07-14', 'SHIFT_1', 1, 'LINE-01', 'DASH-AL-017', 'Dashboard Dummy Frame D', 128, 2, 'PARTS_ASSY_INNER', '2026-07-14 07:20:00', '2026-07-14 14:30:00'),
    ('2026-07-14', 'SHIFT_1', 2, 'LINE-02', 'DASH-AL-018', 'Dashboard Dummy Sash D', 116, 1, 'CUTTING_OUTER', '2026-07-14 07:25:00', '2026-07-14 14:45:00'),
    ('2026-07-14', 'SHIFT_2', 1, 'LINE-03', 'DASH-AL-019', 'Dashboard Dummy Handle D', 140, 2, 'PROCESS_OUTER', '2026-07-14 15:15:00', '2026-07-14 22:15:00'),
    ('2026-07-14', 'SHIFT_2', 2, 'LINE-04', 'DASH-AL-020', 'Dashboard Dummy Rail D', 107, 0, 'PARTS_OUTER', '2026-07-14 15:35:00', '2026-07-14 22:45:00'),
    ('2026-07-14', 'SHIFT_3', 1, 'LINE-05', 'DASH-AL-021', 'Dashboard Dummy Panel D', 92, 1, 'ASSY_OUTER', '2026-07-14 23:00:00', '2026-07-14 23:35:00'),
    ('2026-07-14', 'SHIFT_3', 2, 'LINE-06', 'DASH-AL-022', 'Dashboard Dummy Seal D', 84, 0, 'CUTTING_INNER', '2026-07-14 23:10:00', '2026-07-14 23:45:00'),
    ('2026-07-14', 'SHIFT_3', 3, 'LINE-07', 'DASH-AL-023', 'Dashboard Dummy Lock D', 72, 1, 'PROCESS_INNER', '2026-07-14 23:20:00', '2026-07-14 23:55:00'),

    ('2026-07-15', 'SHIFT_1', 1, 'LINE-01', 'DASH-AL-024', 'Dashboard Dummy Frame E', 133, 1, 'PARTS_ASSY_INNER', '2026-07-15 07:10:00', '2026-07-15 14:20:00'),
    ('2026-07-15', 'SHIFT_2', 1, 'LINE-02', 'DASH-AL-025', 'Dashboard Dummy Sash E', 121, 0, 'CUTTING_OUTER', '2026-07-15 15:15:00', '2026-07-15 22:15:00'),
    ('2026-07-15', 'SHIFT_2', 2, 'LINE-03', 'DASH-AL-026', 'Dashboard Dummy Handle E', 109, 1, 'PROCESS_OUTER', '2026-07-15 15:25:00', '2026-07-15 22:35:00'),
    ('2026-07-15', 'SHIFT_3', 1, 'LINE-04', 'DASH-AL-027', 'Dashboard Dummy Rail E', 95, 1, 'PARTS_OUTER', '2026-07-15 23:05:00', '2026-07-15 23:40:00'),
    ('2026-07-15', 'SHIFT_3', 2, 'LINE-05', 'DASH-AL-028', 'Dashboard Dummy Panel E', 81, 0, 'ASSY_OUTER', '2026-07-15 23:20:00', '2026-07-15 23:55:00');

INSERT INTO cutting_lists
    (cutting_list_no, product_code, product_name, line_code, planned_qty, unit, plan_date, status_master_id)
SELECT
    CONCAT('CL-DASH-', DATE_FORMAT(work_date, '%Y%m%d'), '-', REPLACE(shift_code, 'SHIFT_', 'S'), '-', LPAD(slot_no, 2, '0')),
    product_code,
    product_name,
    line_code,
    actual_qty + reject_qty,
    'PCS',
    work_date,
    3
FROM dashboard_dummy_orders
ON DUPLICATE KEY UPDATE
    product_code = VALUES(product_code),
    product_name = VALUES(product_name),
    line_code = VALUES(line_code),
    planned_qty = VALUES(planned_qty),
    unit = VALUES(unit),
    plan_date = VALUES(plan_date),
    status_master_id = VALUES(status_master_id);

INSERT INTO production_work_orders
    (order_number, cutting_list_id, pic_card_id, shift_master_id, area_master_id, line_code, target_qty, actual_qty, reject_qty, started_at, completed_at, created_at, updated_at, status_master_id)
SELECT
    CONCAT('WO-DASH-', DATE_FORMAT(d.work_date, '%Y%m%d'), '-', REPLACE(d.shift_code, 'SHIFT_', 'S'), '-', LPAD(d.slot_no, 2, '0')),
    cl.id,
    NULL,
    sm.id,
    am.id,
    d.line_code,
    d.actual_qty + d.reject_qty,
    d.actual_qty,
    d.reject_qty,
    d.started_at,
    d.completed_at,
    d.started_at,
    d.completed_at,
    3
FROM dashboard_dummy_orders d
JOIN cutting_lists cl
    ON cl.cutting_list_no = CONCAT('CL-DASH-', DATE_FORMAT(d.work_date, '%Y%m%d'), '-', REPLACE(d.shift_code, 'SHIFT_', 'S'), '-', LPAD(d.slot_no, 2, '0'))
JOIN shift_masters sm
    ON sm.shift_code = d.shift_code
LEFT JOIN area_master am
    ON am.area_code = d.area_code
ON DUPLICATE KEY UPDATE
    cutting_list_id = VALUES(cutting_list_id),
    shift_master_id = VALUES(shift_master_id),
    area_master_id = VALUES(area_master_id),
    line_code = VALUES(line_code),
    target_qty = VALUES(target_qty),
    actual_qty = VALUES(actual_qty),
    reject_qty = VALUES(reject_qty),
    started_at = VALUES(started_at),
    completed_at = VALUES(completed_at),
    updated_at = VALUES(updated_at),
    status_master_id = VALUES(status_master_id);

DROP TEMPORARY TABLE IF EXISTS dashboard_dummy_orders;
