CREATE TABLE IF NOT EXISTS role_page_access (
    id INT AUTO_INCREMENT PRIMARY KEY,
    role_id INT NOT NULL,
    page_key VARCHAR(80) NOT NULL,
    can_access TINYINT(1) NOT NULL DEFAULT 0,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    UNIQUE KEY uq_role_page_access_role_page (role_id, page_key),
    KEY ix_role_page_access_page (page_key),
    CONSTRAINT fk_role_page_access_role FOREIGN KEY (role_id) REFERENCES user_roles(id) ON DELETE CASCADE
);

INSERT INTO role_page_access (role_id, page_key, can_access)
SELECT r.id, pages.page_key, 1
FROM user_roles r
JOIN (
    SELECT 'dashboard' AS page_key
    UNION ALL SELECT 'production_control'
    UNION ALL SELECT 'shift_master'
    UNION ALL SELECT 'line_master'
    UNION ALL SELECT 'operator_list'
    UNION ALL SELECT 'users'
    UNION ALL SELECT 'role_access'
    UNION ALL SELECT 'activity_log'
    UNION ALL SELECT 'production_activity'
    UNION ALL SELECT 'production_history'
    UNION ALL SELECT 'setting'
) pages
WHERE r.role_code <> 'ADMIN'
ON DUPLICATE KEY UPDATE can_access = can_access;
