-- Format Area Master names with capitalized first letter per word.

UPDATE area_master SET area_name = 'Cutting Outer', description = 'Layout area: Cutting Outer.' WHERE area_code = 'CUTTING_OUTER';
UPDATE area_master SET area_name = 'Process Outer', description = 'Layout area: Process Outer.' WHERE area_code = 'PROCESS_OUTER';
UPDATE area_master SET area_name = 'Parts Outer', description = 'Layout area: Parts Outer.' WHERE area_code = 'PARTS_OUTER';
UPDATE area_master SET area_name = 'Assy Outer', description = 'Layout area: Assy Outer.' WHERE area_code = 'ASSY_OUTER';
UPDATE area_master SET area_name = 'Cutting Inner', description = 'Layout area: Cutting Inner.' WHERE area_code = 'CUTTING_INNER';
UPDATE area_master SET area_name = 'Process Inner', description = 'Layout area: Process Inner.' WHERE area_code = 'PROCESS_INNER';
UPDATE area_master SET area_name = 'Parts & Assy Inner', description = 'Layout area: Parts & Assy Inner.' WHERE area_code = 'PARTS_ASSY_INNER';

DELETE FROM area_master
WHERE area_code = 'PRODUCTION';

ALTER TABLE area_master AUTO_INCREMENT = 8;
