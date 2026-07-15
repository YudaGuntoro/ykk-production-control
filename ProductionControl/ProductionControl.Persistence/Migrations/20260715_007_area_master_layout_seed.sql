-- Seed Area Master based on production layout names.

DELETE FROM area_master
WHERE area_code = 'PRODUCTION';

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
