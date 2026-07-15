-- Area Master - normalized master data for future area expansion.

CREATE TABLE IF NOT EXISTS area_master (
    id INT AUTO_INCREMENT PRIMARY KEY,
    area_code VARCHAR(50) NOT NULL,
    area_name VARCHAR(150) NOT NULL,
    description VARCHAR(255) NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_area_master_code (area_code),
    UNIQUE KEY uq_area_master_name (area_name),
    KEY ix_area_master_active_name (is_active, area_name)
);

INSERT IGNORE INTO area_master
    (id, area_code, area_name, description, is_active)
VALUES
    (1, 'CUTTING_OUTER', 'Cutting Outer', 'Layout area: Cutting Outer.', 1),
    (2, 'PROCESS_OUTER', 'Process Outer', 'Layout area: Process Outer.', 1),
    (3, 'PARTS_OUTER', 'Parts Outer', 'Layout area: Parts Outer.', 1),
    (4, 'ASSY_OUTER', 'Assy Outer', 'Layout area: Assy Outer.', 1),
    (5, 'CUTTING_INNER', 'Cutting Inner', 'Layout area: Cutting Inner.', 1),
    (6, 'PROCESS_INNER', 'Process Inner', 'Layout area: Process Inner.', 1),
    (7, 'PARTS_ASSY_INNER', 'Parts & Assy Inner', 'Layout area: Parts & Assy Inner.', 1);

ALTER TABLE area_master AUTO_INCREMENT = 8;
