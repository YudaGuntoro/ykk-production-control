-- Normalize login user roles into user_roles master table.

CREATE TABLE IF NOT EXISTS user_roles (
    id INT AUTO_INCREMENT PRIMARY KEY,
    role_code VARCHAR(50) NOT NULL,
    role_name VARCHAR(100) NOT NULL,
    description VARCHAR(255) NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_user_roles_code (role_code),
    KEY ix_user_roles_active_name (is_active, role_name)
);

INSERT INTO user_roles (role_code, role_name, description, is_active)
VALUES
    ('ADMIN', 'Admin', 'Full access user.', 1),
    ('SUPERVISOR', 'Supervisor', 'Production supervisor user.', 1),
    ('OPERATOR', 'Operator', 'Production operator user.', 1),
    ('VIEWER', 'Viewer', 'Read only user.', 1)
ON DUPLICATE KEY UPDATE
    role_name = VALUES(role_name),
    description = IF(description IS NULL OR description = '', VALUES(description), description),
    updated_at = CURRENT_TIMESTAMP;

SET @add_role_id = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'users' AND COLUMN_NAME = 'role_id') = 0,
    'ALTER TABLE users ADD COLUMN role_id INT NULL AFTER phone',
    'SELECT 1'
);
PREPARE stmt FROM @add_role_id;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

UPDATE users u
JOIN user_roles r ON r.role_code = UPPER(COALESCE(NULLIF(u.role, ''), 'VIEWER'))
SET u.role_id = r.id,
    u.role = r.role_code
WHERE u.role_id IS NULL;

UPDATE users u
JOIN user_roles r ON r.role_code = 'VIEWER'
SET u.role_id = r.id,
    u.role = r.role_code
WHERE u.role_id IS NULL;

SET @add_role_index = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'users' AND INDEX_NAME = 'ix_users_role_id') = 0,
    'ALTER TABLE users ADD INDEX ix_users_role_id (role_id)',
    'SELECT 1'
);
PREPARE stmt FROM @add_role_index;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
