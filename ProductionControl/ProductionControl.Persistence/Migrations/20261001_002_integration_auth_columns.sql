-- Ensure external integration settings can store login credentials.

SET @has_auth_username := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'production_integration_settings'
      AND COLUMN_NAME = 'auth_username'
);

SET @sql := IF(
    @has_auth_username = 0,
    'ALTER TABLE production_integration_settings ADD COLUMN auth_username VARCHAR(150) NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

SET @has_auth_password := (
    SELECT COUNT(*)
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'production_integration_settings'
      AND COLUMN_NAME = 'auth_password'
);

SET @sql := IF(
    @has_auth_password = 0,
    'ALTER TABLE production_integration_settings ADD COLUMN auth_password VARCHAR(500) NULL',
    'SELECT 1'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
