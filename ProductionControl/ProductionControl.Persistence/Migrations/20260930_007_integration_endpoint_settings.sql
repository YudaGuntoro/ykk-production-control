-- Store editable endpoints for external integrations used by production pages.

CREATE TABLE IF NOT EXISTS production_integration_settings (
    id INT AUTO_INCREMENT PRIMARY KEY,
    setting_key VARCHAR(80) NOT NULL,
    base_url VARCHAR(500) NOT NULL,
    endpoint_path VARCHAR(255) NOT NULL,
    filter_field_name VARCHAR(80) NOT NULL DEFAULT 'LOT_NO',
    top INT NOT NULL DEFAULT 1,
    skip INT NOT NULL DEFAULT 0,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_production_integration_settings_key (setting_key)
);

INSERT INTO production_integration_settings
    (setting_key, base_url, endpoint_path, filter_field_name, top, skip, is_active)
VALUES
    ('shiage_lot_no', '', '/fab-shiage-prod-res/', 'LOT_NO', 1, 0, 0),
    ('internal_system_auth', '', '/auth/login', '-', 1, 0, 1)
ON DUPLICATE KEY UPDATE
    endpoint_path = IF(endpoint_path IS NULL OR endpoint_path = '', VALUES(endpoint_path), endpoint_path),
    filter_field_name = IF(filter_field_name IS NULL OR filter_field_name = '', VALUES(filter_field_name), filter_field_name),
    top = IF(top IS NULL OR top < 1, VALUES(top), top),
    skip = IF(skip IS NULL OR skip < 0, VALUES(skip), skip),
    updated_at = CURRENT_TIMESTAMP;
