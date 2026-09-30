-- Unit Master - normalized master data for cutting list units.

CREATE TABLE IF NOT EXISTS unit_master (
    id INT AUTO_INCREMENT PRIMARY KEY,
    unit_code VARCHAR(20) NOT NULL,
    unit_name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_unit_master_code (unit_code),
    UNIQUE KEY uq_unit_master_name (unit_name),
    KEY ix_unit_master_active_name (is_active, unit_name)
);

INSERT INTO unit_master
    (id, unit_code, unit_name, description, is_active)
VALUES
    (1, 'PCS', 'Pieces', 'Default production quantity unit.', 1),
    (2, 'SET', 'Set', 'Set-based production quantity unit.', 1),
    (3, 'KG', 'Kilogram', 'Weight-based production quantity unit.', 1)
ON DUPLICATE KEY UPDATE
    unit_name = VALUES(unit_name),
    description = VALUES(description),
    is_active = VALUES(is_active);

ALTER TABLE unit_master AUTO_INCREMENT = 4;
