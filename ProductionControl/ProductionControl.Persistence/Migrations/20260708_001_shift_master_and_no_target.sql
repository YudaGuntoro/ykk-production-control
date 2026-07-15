-- Production Control - add shift master and make target/planned quantity optional.

CREATE TABLE IF NOT EXISTS shift_masters (
    id INT AUTO_INCREMENT PRIMARY KEY,
    shift_code VARCHAR(50) NOT NULL,
    shift_name VARCHAR(100) NOT NULL,
    shift_type VARCHAR(30) NULL,
    start_schedule TIME NULL,
    finish_schedule TIME NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE KEY uq_shift_masters_code (shift_code)
);

INSERT INTO shift_masters (shift_code, shift_name, shift_type, start_schedule, finish_schedule, is_active)
VALUES
    ('SHIFT_1', 'Shift 1', 'Day', '07:00:00', '15:00:00', 1),
    ('SHIFT_2', 'Shift 2', 'Middle', '15:00:00', '23:00:00', 1),
    ('SHIFT_3', 'Shift 3', 'Night', '23:00:00', '07:00:00', 1),
    ('LONG_SHIFT_1', 'Long Shift 1', NULL, NULL, NULL, 1),
    ('LONG_SHIFT_2', 'Long Shift 2', NULL, NULL, NULL, 1)
ON DUPLICATE KEY UPDATE
    shift_name = VALUES(shift_name),
    shift_type = VALUES(shift_type),
    start_schedule = VALUES(start_schedule),
    finish_schedule = VALUES(finish_schedule),
    is_active = VALUES(is_active);

ALTER TABLE cutting_lists MODIFY planned_qty INT NOT NULL DEFAULT 0;
ALTER TABLE production_work_orders MODIFY target_qty INT NOT NULL DEFAULT 0;

UPDATE operator_master SET shift = 'Shift 1' WHERE shift IN ('SHIFT 1', 'SHIFT_1');
UPDATE operator_master SET shift = 'Shift 2' WHERE shift IN ('SHIFT 2', 'SHIFT_2');
UPDATE operator_master SET shift = 'Shift 3' WHERE shift IN ('SHIFT 3', 'SHIFT_3');
