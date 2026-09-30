export type ProductionWorkOrderStatus =
  | "WAITING"
  | "IN_PROGRESS"
  | "FINISH";

export type ProductionWorkOrder = {
  id: number;
  order_number: string;
  pic_card_id?: number | null;
  pic_name?: string | null;
  employee_no?: string | null;
  operator_shift?: string | null;
  operator_department?: string | null;
  work_shift_code?: string | null;
  work_shift_name?: string | null;
  work_shift_type?: string | null;
  line_master_id?: number | null;
  line_no?: string | null;
  line_name?: string | null;
  operators: ProductionOperator[];
  line_code: string;
  actual_qty: number;
  reject_qty: number;
  status: ProductionWorkOrderStatus;
  plan_date: string;
  started_at?: string | null;
  completed_at?: string | null;
  updated_at: string;
  lot_no?: string | null;
  project_no?: string | null;
  weight?: number | null;
};

export type ProductionOperator = {
  id: number;
  pic_card_id: number;
  card_uid: string;
  employee_no: string;
  full_name: string;
  department: string;
  shift: string;
  work_shift_code?: string | null;
  work_shift_name?: string | null;
  work_shift_type?: string | null;
  scanned_at: string;
};

export type ProductionActiveOperator = {
  id: number;
  pic_card_id: number;
  card_uid: string;
  employee_no: string;
  full_name: string;
  department: string;
  operator_shift: string;
  shift_master_id?: number | null;
  shift_code?: string | null;
  shift_name?: string | null;
  shift_type?: string | null;
  scanned_at: string;
};

export type ActiveOperatorSummary = {
  current_shift?: ShiftMaster | null;
  has_shift_changed: boolean;
  operators: ProductionActiveOperator[];
};

export type ProductionDashboardSummary = {
  total_work_orders: number;
  waiting_work_orders: number;
  running_work_orders: number;
  completed_work_orders: number;
  actual_qty: number;
  reject_qty: number;
  work_orders: ProductionDashboardWorkOrder[];
  daily_shift_outputs: ProductionDashboardShiftOutput[];
};

export type ProductionDashboardWorkOrder = {
  id: number;
  project_no?: string | null;
  order_no?: string | null;
  lot_no?: string | null;
  weight?: number | null;
  status: ProductionWorkOrderStatus;
};

export type ProductionDashboardShiftOutput = {
  date: string;
  date_label: string;
  shift_1_count: number;
  shift_2_count: number;
  shift_3_count: number;
  total_count: number;
};

export type CuttingList = {
  id: number;
  line_code: string;
  planned_qty: number;
  unit: string;
  plan_date: string;
  status: "WAITING" | "IN_PROGRESS" | "FINISH";
  created_at: string;
  order_number?: string | null;
  started_at?: string | null;
  completed_at?: string | null;
  operators?: ProductionOperator[];
  start_operators?: ProductionOperator[];
  finish_operators?: ProductionOperator[];
  lot_no?: string | null;
  project_no?: string | null;
  weight?: number | null;
};

export type PicCard = {
  id: number;
  card_uid: string;
  employee_no: string;
  full_name: string;
  department: string;
  shift: string;
  is_active: boolean;
  last_scanned_at?: string | null;
  created_at: string;
};

export type ShiftMaster = {
  id: number;
  shift_code: string;
  shift_name: string;
  shift_type?: string | null;
  start_schedule?: string | null;
  finish_schedule?: string | null;
  is_active: boolean;
  created_at: string;
};

export type AreaMaster = {
  id: number;
  line_no: string;
  line_name: string;
  description?: string | null;
  is_active: boolean;
  created_at: string;
  updated_at: string;
};

export type ProductionActivityLog = {
  id: number;
  production_work_order_id: number;
  order_number?: string | null;
  user_id?: number | null;
  username?: string | null;
  pic_name?: string | null;
  employee_no?: string | null;
  activity_type: string;
  remarks?: string | null;
  lot_no?: string | null;
  project_no?: string | null;
  weight?: number | null;
  created_at: string;
};
