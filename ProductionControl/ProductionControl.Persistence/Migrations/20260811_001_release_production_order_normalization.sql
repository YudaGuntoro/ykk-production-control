-- Normalize yellow Release Production Order columns for production history.
-- Source columns: Lot_No, Prod_Line_After_BD, Project_No_, Project_Name, Weight.

CREATE TABLE IF NOT EXISTS production_line_master (
    id INT AUTO_INCREMENT PRIMARY KEY,
    line_code VARCHAR(80) NOT NULL,
    line_name VARCHAR(150) NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_production_line_master_code (line_code)
);

CREATE TABLE IF NOT EXISTS project_master (
    id INT AUTO_INCREMENT PRIMARY KEY,
    project_no VARCHAR(80) NOT NULL,
    project_name VARCHAR(200) NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_project_master_no (project_no)
);

CREATE TABLE IF NOT EXISTS release_production_order_details (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    production_work_order_id INT NOT NULL,
    lot_no VARCHAR(80) NULL,
    production_line_master_id INT NULL,
    project_master_id INT NULL,
    weight DECIMAL(12,3) NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_rpo_details_work_order (production_work_order_id),
    KEY ix_rpo_details_lot_no (lot_no),
    KEY ix_rpo_details_line (production_line_master_id),
    KEY ix_rpo_details_project (project_master_id),
    CONSTRAINT fk_rpo_details_work_order FOREIGN KEY (production_work_order_id) REFERENCES production_work_orders(id) ON DELETE CASCADE,
    CONSTRAINT fk_rpo_details_line FOREIGN KEY (production_line_master_id) REFERENCES production_line_master(id) ON DELETE SET NULL,
    CONSTRAINT fk_rpo_details_project FOREIGN KEY (project_master_id) REFERENCES project_master(id) ON DELETE SET NULL
);

INSERT INTO production_line_master (line_code, line_name)
SELECT DISTINCT NULLIF(TRIM(line_code), ''), NULLIF(TRIM(line_code), '')
FROM production_work_orders
WHERE NULLIF(TRIM(line_code), '') IS NOT NULL
ON DUPLICATE KEY UPDATE
    line_name = COALESCE(VALUES(line_name), production_line_master.line_name),
    is_active = 1;
