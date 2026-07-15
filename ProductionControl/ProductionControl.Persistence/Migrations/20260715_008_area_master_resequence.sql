-- Delete legacy Production area and resequence Area Master IDs from 1.

INSERT INTO area_master
    (area_code, area_name, description, is_active)
VALUES
    ('CUTTING_OUTER', 'Cutting Outer', 'Layout area: Cutting Outer.', 1),
    ('PROCESS_OUTER', 'Process Outer', 'Layout area: Process Outer.', 1),
    ('PARTS_OUTER', 'Parts Outer', 'Layout area: Parts Outer.', 1),
    ('ASSY_OUTER', 'Assy Outer', 'Layout area: Assy Outer.', 1),
    ('CUTTING_INNER', 'Cutting Inner', 'Layout area: Cutting Inner.', 1),
    ('PROCESS_INNER', 'Process Inner', 'Layout area: Process Inner.', 1),
    ('PARTS_ASSY_INNER', 'Parts & Assy Inner', 'Layout area: Parts & Assy Inner.', 1)
ON DUPLICATE KEY UPDATE
    area_name = VALUES(area_name),
    description = VALUES(description),
    is_active = VALUES(is_active);

SET @production_id := (SELECT id FROM area_master WHERE area_code = 'PRODUCTION' LIMIT 1);
SET @cutting_outer_id := (SELECT id FROM area_master WHERE area_code = 'CUTTING_OUTER' LIMIT 1);
SET @process_outer_id := (SELECT id FROM area_master WHERE area_code = 'PROCESS_OUTER' LIMIT 1);
SET @parts_outer_id := (SELECT id FROM area_master WHERE area_code = 'PARTS_OUTER' LIMIT 1);
SET @assy_outer_id := (SELECT id FROM area_master WHERE area_code = 'ASSY_OUTER' LIMIT 1);
SET @cutting_inner_id := (SELECT id FROM area_master WHERE area_code = 'CUTTING_INNER' LIMIT 1);
SET @process_inner_id := (SELECT id FROM area_master WHERE area_code = 'PROCESS_INNER' LIMIT 1);
SET @parts_assy_inner_id := (SELECT id FROM area_master WHERE area_code = 'PARTS_ASSY_INNER' LIMIT 1);

SET FOREIGN_KEY_CHECKS = 0;

UPDATE production_work_orders
SET area_master_id = NULL
WHERE area_master_id = @production_id;

DELETE FROM area_master
WHERE area_code = 'PRODUCTION';

UPDATE production_work_orders SET area_master_id = 1 WHERE area_master_id = @cutting_outer_id;
UPDATE production_work_orders SET area_master_id = 2 WHERE area_master_id = @process_outer_id;
UPDATE production_work_orders SET area_master_id = 3 WHERE area_master_id = @parts_outer_id;
UPDATE production_work_orders SET area_master_id = 4 WHERE area_master_id = @assy_outer_id;
UPDATE production_work_orders SET area_master_id = 5 WHERE area_master_id = @cutting_inner_id;
UPDATE production_work_orders SET area_master_id = 6 WHERE area_master_id = @process_inner_id;
UPDATE production_work_orders SET area_master_id = 7 WHERE area_master_id = @parts_assy_inner_id;

UPDATE area_master SET id = 1, area_name = 'Cutting Outer', description = 'Layout area: Cutting Outer.', is_active = 1 WHERE area_code = 'CUTTING_OUTER';
UPDATE area_master SET id = 2, area_name = 'Process Outer', description = 'Layout area: Process Outer.', is_active = 1 WHERE area_code = 'PROCESS_OUTER';
UPDATE area_master SET id = 3, area_name = 'Parts Outer', description = 'Layout area: Parts Outer.', is_active = 1 WHERE area_code = 'PARTS_OUTER';
UPDATE area_master SET id = 4, area_name = 'Assy Outer', description = 'Layout area: Assy Outer.', is_active = 1 WHERE area_code = 'ASSY_OUTER';
UPDATE area_master SET id = 5, area_name = 'Cutting Inner', description = 'Layout area: Cutting Inner.', is_active = 1 WHERE area_code = 'CUTTING_INNER';
UPDATE area_master SET id = 6, area_name = 'Process Inner', description = 'Layout area: Process Inner.', is_active = 1 WHERE area_code = 'PROCESS_INNER';
UPDATE area_master SET id = 7, area_name = 'Parts & Assy Inner', description = 'Layout area: Parts & Assy Inner.', is_active = 1 WHERE area_code = 'PARTS_ASSY_INNER';

SET FOREIGN_KEY_CHECKS = 1;

ALTER TABLE area_master AUTO_INCREMENT = 8;
