-- Dummy Release Production Order details for history preview.
-- Populates the normalized yellow columns:
-- Lot_No, Prod_Line_After_BD, Project_No_, Project_Name, Weight.

DROP TEMPORARY TABLE IF EXISTS rpo_dummy_details;

CREATE TEMPORARY TABLE rpo_dummy_details (
    order_number VARCHAR(80) NOT NULL PRIMARY KEY,
    lot_no VARCHAR(80) NULL,
    prod_line_after_bd VARCHAR(80) NULL,
    project_no VARCHAR(80) NULL,
    project_name VARCHAR(200) NULL,
    weight DECIMAL(12,3) NULL
);

INSERT INTO rpo_dummy_details
    (order_number, lot_no, prod_line_after_bd, project_no, project_name, weight)
SELECT
    pwo.order_number,
    CONCAT(
        DATE_FORMAT(COALESCE(pwo.started_at, pwo.created_at), '%y%m'),
        LPAD(pwo.id MOD 10000, 4, '0'),
        'X',
        LPAD((pwo.id * 7) MOD 999, 3, '0')
    ),
    CASE (pwo.id MOD 7)
        WHEN 0 THEN 'M1'
        WHEN 1 THEN 'M2'
        WHEN 2 THEN 'M3'
        WHEN 3 THEN 'P1'
        WHEN 4 THEN 'P2'
        WHEN 5 THEN 'A1'
        ELSE 'A2'
    END,
    CASE (pwo.id MOD 6)
        WHEN 0 THEN '1028370'
        WHEN 1 THEN '1025908'
        WHEN 2 THEN '1031142'
        WHEN 3 THEN '1032881'
        WHEN 4 THEN '1040017'
        ELSE '1042190'
    END,
    CASE (pwo.id MOD 6)
        WHEN 0 THEN 'POCHAN-1025908'
        WHEN 1 THEN 'KAWANA RESIDENCE'
        WHEN 2 THEN 'TOKYO RIVERSIDE'
        WHEN 3 THEN 'BSD OFFICE TOWER'
        WHEN 4 THEN 'SURABAYA MALL FACADE'
        ELSE 'YKK SHOWROOM SAMPLE'
    END,
    ROUND(2.750 + ((pwo.id MOD 19) * 0.347), 3)
FROM production_work_orders pwo
WHERE pwo.order_number LIKE 'WO-RPT-%'
   OR pwo.order_number IN ('WO-YKK-001', 'WO-YKK-002', 'WO-YKK-003');

INSERT INTO production_line_master (line_code, line_name, is_active)
SELECT DISTINCT prod_line_after_bd, CONCAT('Production Line ', prod_line_after_bd), 1
FROM rpo_dummy_details
WHERE prod_line_after_bd IS NOT NULL
ON DUPLICATE KEY UPDATE
    line_name = VALUES(line_name),
    is_active = 1,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO project_master (project_no, project_name, is_active)
SELECT DISTINCT project_no, project_name, 1
FROM rpo_dummy_details
WHERE project_no IS NOT NULL
ON DUPLICATE KEY UPDATE
    project_name = VALUES(project_name),
    is_active = 1,
    updated_at = CURRENT_TIMESTAMP;

INSERT INTO release_production_order_details
    (production_work_order_id, lot_no, production_line_master_id, project_master_id, weight, created_at, updated_at)
SELECT
    pwo.id,
    d.lot_no,
    plm.id,
    pm.id,
    d.weight,
    COALESCE(pwo.started_at, pwo.created_at, CURRENT_TIMESTAMP),
    CURRENT_TIMESTAMP
FROM rpo_dummy_details d
JOIN production_work_orders pwo ON pwo.order_number = d.order_number
LEFT JOIN production_line_master plm ON plm.line_code = d.prod_line_after_bd
LEFT JOIN project_master pm ON pm.project_no = d.project_no
ON DUPLICATE KEY UPDATE
    lot_no = VALUES(lot_no),
    production_line_master_id = VALUES(production_line_master_id),
    project_master_id = VALUES(project_master_id),
    weight = VALUES(weight),
    updated_at = CURRENT_TIMESTAMP;

DROP TEMPORARY TABLE IF EXISTS rpo_dummy_details;
