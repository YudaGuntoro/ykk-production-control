-- Fresh database bootstrap for Production Control Monitoring System
-- PT YKK AP Indonesia / MySQL 8

CREATE DATABASE IF NOT EXISTS db_production_control
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE db_production_control;

CREATE TABLE IF NOT EXISTS users (
    id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(80) NOT NULL,
    full_name VARCHAR(150) NOT NULL,
    email VARCHAR(150) NULL,
    phone VARCHAR(50) NULL,
    role VARCHAR(30) NOT NULL,
    status VARCHAR(30) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    password_salt VARCHAR(255) NOT NULL,
    last_login_at DATETIME NULL,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_users_username (username),
    UNIQUE KEY uq_users_email (email)
);

-- Default login: admin / admin123
INSERT IGNORE INTO users
    (id, username, full_name, email, role, status, password_hash, password_salt)
VALUES
    (1, 'admin', 'Production Control Administrator', 'admin@ykkap.local', 'ADMIN', 'ACTIVE',
     'mV/QhZOhh7mvmWj0P1RgeXm3hZB1AkKHY5jfEcrC7PE=', 'Y21tcy1hZG1pbi1zYWx0LXYx');

SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260707_001_production_control_monitoring.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260707_002_production_work_order_operators.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260708_001_shift_master_and_no_target.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260708_002_shift_master_schedule.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260713_001_active_operator_shift_workflow.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_001_work_order_operator_snapshots.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_002_status_master.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_003_operator_master_table.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_004_order_number_column.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_005_area_master.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_006_work_order_area.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_007_area_master_layout_seed.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_008_area_master_resequence.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_009_area_master_title_case.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_010_dashboard_shift_output_index.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260715_011_dashboard_chart_dummy_data.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260805_001_report_activity_dummy_data.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260811_001_release_production_order_normalization.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260811_002_release_production_order_dummy_data.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260811_003_waiting_and_multi_operator_dummy_data.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260811_004_more_waiting_work_order_dummy_data.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260911_001_shiage_result_direct_columns.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260911_002_drop_cutting_list_product_columns.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_001_unit_master.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_002_rename_area_master_to_line_master.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_003_work_orders_direct_plan_date.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_004_drop_cutting_lists.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_005_drop_production_line_master.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_006_activity_logs_user_and_drop_work_order_pic.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_007_integration_endpoint_settings.sql;
SOURCE ProductionControl/ProductionControl.Persistence/Migrations/20260930_008_repair_line_master_columns.sql;
