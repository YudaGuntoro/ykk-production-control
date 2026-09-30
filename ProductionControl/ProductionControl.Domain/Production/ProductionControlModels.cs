using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using ProductionControl.Domain.Auth;

namespace ProductionControl.Domain.Production;

public enum CuttingListStatus
{
    WAITING,
    IN_PROGRESS,
    FINISH
}

public enum ProductionWorkOrderStatus
{
    WAITING,
    IN_PROGRESS,
    FINISH
}

public enum ProductionActivityType
{
    PIC_SCAN,
    OPERATOR_REMOVE,
    CUTTING_LIST_SCAN,
    WORK_START,
    PRODUCTION_UPDATE,
    WORK_HOLD,
    WORK_RESUME,
    FINISH_CANCELLED,
    WORK_COMPLETE
}

public enum ProductionOperatorSnapshotType
{
    START,
    FINISH
}

public static class ProductionStatusMaster
{
    public const string WorkOrderGroup = "PRODUCTION_WORK_ORDER";

    public static int ToId(CuttingListStatus status) => status switch
    {
        CuttingListStatus.WAITING => 1,
        CuttingListStatus.IN_PROGRESS => 2,
        CuttingListStatus.FINISH => 3,
        _ => 1
    };

    public static int ToId(ProductionWorkOrderStatus status) => status switch
    {
        ProductionWorkOrderStatus.WAITING => 1,
        ProductionWorkOrderStatus.IN_PROGRESS => 2,
        ProductionWorkOrderStatus.FINISH => 3,
        _ => 1
    };

    public static CuttingListStatus ToCuttingListStatus(int statusMasterId) => statusMasterId switch
    {
        1 => CuttingListStatus.WAITING,
        2 => CuttingListStatus.IN_PROGRESS,
        3 => CuttingListStatus.FINISH,
        _ => CuttingListStatus.WAITING
    };

    public static ProductionWorkOrderStatus ToWorkOrderStatus(int statusMasterId) => statusMasterId switch
    {
        1 => ProductionWorkOrderStatus.WAITING,
        2 => ProductionWorkOrderStatus.IN_PROGRESS,
        3 => ProductionWorkOrderStatus.FINISH,
        _ => ProductionWorkOrderStatus.WAITING
    };
}

public class StatusMaster
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("status_group")]
    public string StatusGroup { get; set; } = string.Empty;

    [JsonPropertyName("status_code")]
    public string StatusCode { get; set; } = string.Empty;

    [JsonPropertyName("status_name")]
    public string StatusName { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class PicCard
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("card_uid")]
    public string CardUid { get; set; } = string.Empty;

    [JsonPropertyName("employee_no")]
    public string EmployeeNo { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; set; } = "Production";

    [JsonPropertyName("shift")]
    public string Shift { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("last_scanned_at")]
    public DateTime? LastScannedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class ShiftMaster
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("shift_code")]
    public string ShiftCode { get; set; } = string.Empty;

    [JsonPropertyName("shift_name")]
    public string ShiftName { get; set; } = string.Empty;

    [JsonPropertyName("shift_type")]
    public string? ShiftType { get; set; }

    [JsonPropertyName("start_schedule")]
    public TimeSpan? StartSchedule { get; set; }

    [JsonPropertyName("finish_schedule")]
    public TimeSpan? FinishSchedule { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class AreaMaster
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("line_no")]
    public string AreaCode { get; set; } = string.Empty;

    [JsonPropertyName("line_name")]
    public string AreaName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class ProjectMaster
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("project_no")]
    public string ProjectNo { get; set; } = string.Empty;

    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class UnitMaster
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("unit_code")]
    public string UnitCode { get; set; } = string.Empty;

    [JsonPropertyName("unit_name")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class ProductionIntegrationSetting
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("setting_key")]
    public string SettingKey { get; set; } = string.Empty;

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("endpoint_path")]
    public string EndpointPath { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("filter_field_name")]
    public string FilterFieldName { get; set; } = "LOT_NO";

    [JsonPropertyName("top")]
    public int Top { get; set; } = 1;

    [JsonPropertyName("skip")]
    public int Skip { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class ReleaseProductionOrderDetail
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("production_work_order_id")]
    public int ProductionWorkOrderId { get; set; }

    [JsonPropertyName("order_no")]
    public string? OrderNo { get; set; }

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }

    [JsonPropertyName("project_no")]
    public string? ProjectNo { get; set; }

    [JsonPropertyName("project_master_id")]
    public int? ProjectMasterId { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public ProductionWorkOrder? ProductionWorkOrder { get; set; }

    [JsonIgnore]
    public ProjectMaster? ProjectMaster { get; set; }
}

public class CuttingListResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("line_code")]
    public string LineCode { get; set; } = string.Empty;

    [JsonPropertyName("planned_qty")]
    public int PlannedQty { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "PCS";

    [JsonPropertyName("plan_date")]
    public DateTime PlanDate { get; set; }

    [JsonPropertyName("status")]
    public CuttingListStatus Status { get; set; } = CuttingListStatus.WAITING;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("order_number")]
    public string? OrderNumber { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("operators")]
    public List<ProductionOperatorResponse> Operators { get; set; } = [];

    [JsonPropertyName("start_operators")]
    public List<ProductionOperatorResponse> StartOperators { get; set; } = [];

    [JsonPropertyName("finish_operators")]
    public List<ProductionOperatorResponse> FinishOperators { get; set; } = [];

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }

    [JsonPropertyName("project_no")]
    public string? ProjectNo { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }
}

public class ProductionWorkOrder
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("shift_master_id")]
    public int? ShiftMasterId { get; set; }

    [JsonPropertyName("line_master_id")]
    public int? AreaMasterId { get; set; }

    [JsonPropertyName("line_code")]
    public string LineCode { get; set; } = string.Empty;

    [JsonPropertyName("plan_date")]
    public DateTime PlanDate { get; set; } = DateTime.Today;

    [JsonIgnore]
    public int TargetQty { get; set; }

    [JsonPropertyName("actual_qty")]
    public int ActualQty { get; set; }

    [JsonPropertyName("reject_qty")]
    public int RejectQty { get; set; }

    [JsonIgnore]
    public int StatusMasterId { get; set; } = ProductionStatusMaster.ToId(ProductionWorkOrderStatus.WAITING);

    [JsonPropertyName("status")]
    [NotMapped]
    public ProductionWorkOrderStatus Status
    {
        get => ProductionStatusMaster.ToWorkOrderStatus(StatusMasterId);
        set => StatusMasterId = ProductionStatusMaster.ToId(value);
    }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public ShiftMaster? ShiftMaster { get; set; }

    [JsonIgnore]
    public AreaMaster? AreaMaster { get; set; }

    [JsonIgnore]
    public StatusMaster? StatusMaster { get; set; }

    [JsonIgnore]
    public ICollection<ProductionWorkOrderOperator> Operators { get; set; } = new List<ProductionWorkOrderOperator>();

    [JsonIgnore]
    public ICollection<ProductionWorkOrderOperatorSnapshot> OperatorSnapshots { get; set; } = new List<ProductionWorkOrderOperatorSnapshot>();

    [JsonIgnore]
    public ICollection<ProductionActivityLog> ActivityLogs { get; set; } = new List<ProductionActivityLog>();

    [JsonIgnore]
    public ReleaseProductionOrderDetail? ReleaseProductionOrderDetail { get; set; }
}

public class ProductionWorkOrderOperator
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("production_work_order_id")]
    public int ProductionWorkOrderId { get; set; }

    [JsonPropertyName("pic_card_id")]
    public int PicCardId { get; set; }

    [JsonPropertyName("production_active_operator_id")]
    public long? ProductionActiveOperatorId { get; set; }

    [JsonPropertyName("shift_master_id")]
    public int? ShiftMasterId { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("scanned_at")]
    public DateTime ScannedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("removed_at")]
    public DateTime? RemovedAt { get; set; }

    [JsonIgnore]
    public ProductionWorkOrder? ProductionWorkOrder { get; set; }

    [JsonIgnore]
    public PicCard? PicCard { get; set; }

    [JsonIgnore]
    public ProductionActiveOperator? ProductionActiveOperator { get; set; }

    [JsonIgnore]
    public ShiftMaster? ShiftMaster { get; set; }
}

public class ProductionActiveOperator
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("pic_card_id")]
    public int PicCardId { get; set; }

    [JsonPropertyName("shift_master_id")]
    public int? ShiftMasterId { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("scanned_at")]
    public DateTime ScannedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("removed_at")]
    public DateTime? RemovedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public PicCard? PicCard { get; set; }

    [JsonIgnore]
    public ShiftMaster? ShiftMaster { get; set; }
}

public class ProductionWorkOrderOperatorSnapshot
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("production_work_order_id")]
    public int ProductionWorkOrderId { get; set; }

    [JsonPropertyName("pic_card_id")]
    public int PicCardId { get; set; }

    [JsonPropertyName("production_active_operator_id")]
    public long? ProductionActiveOperatorId { get; set; }

    [JsonPropertyName("shift_master_id")]
    public int? ShiftMasterId { get; set; }

    [JsonPropertyName("snapshot_type")]
    public ProductionOperatorSnapshotType SnapshotType { get; set; }

    [JsonPropertyName("scanned_at")]
    public DateTime ScannedAt { get; set; }

    [JsonPropertyName("snapshot_at")]
    public DateTime SnapshotAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public ProductionWorkOrder? ProductionWorkOrder { get; set; }

    [JsonIgnore]
    public PicCard? PicCard { get; set; }

    [JsonIgnore]
    public ProductionActiveOperator? ProductionActiveOperator { get; set; }

    [JsonIgnore]
    public ShiftMaster? ShiftMaster { get; set; }
}

public class ProductionActivityLog
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("production_work_order_id")]
    public int ProductionWorkOrderId { get; set; }

    [JsonPropertyName("user_id")]
    public int? UserId { get; set; }

    [JsonPropertyName("activity_type")]
    public ProductionActivityType ActivityType { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public ProductionWorkOrder? ProductionWorkOrder { get; set; }

    [JsonIgnore]
    public AppUser? User { get; set; }
}

public class ProductionDashboardSummary
{
    [JsonPropertyName("total_work_orders")]
    public int TotalWorkOrders { get; set; }

    [JsonPropertyName("waiting_work_orders")]
    public int WaitingWorkOrders { get; set; }

    [JsonPropertyName("running_work_orders")]
    public int RunningWorkOrders { get; set; }

    [JsonPropertyName("completed_work_orders")]
    public int CompletedWorkOrders { get; set; }

    [JsonPropertyName("actual_qty")]
    public int ActualQty { get; set; }

    [JsonPropertyName("reject_qty")]
    public int RejectQty { get; set; }

    [JsonPropertyName("work_orders")]
    public List<ProductionDashboardWorkOrderResponse> WorkOrders { get; set; } = [];

    [JsonPropertyName("daily_shift_outputs")]
    public List<ProductionDailyShiftOutput> DailyShiftOutputs { get; set; } = [];
}

public class ProductionDailyShiftOutput
{
    [JsonPropertyName("date")]
    public DateTime Date { get; set; }

    [JsonPropertyName("date_label")]
    public string DateLabel { get; set; } = string.Empty;

    [JsonPropertyName("shift_1_count")]
    public int Shift1Count { get; set; }

    [JsonPropertyName("shift_2_count")]
    public int Shift2Count { get; set; }

    [JsonPropertyName("shift_3_count")]
    public int Shift3Count { get; set; }

    [JsonPropertyName("total_count")]
    public int TotalCount => Shift1Count + Shift2Count + Shift3Count;
}

public class ProductionDashboardWorkOrderResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("project_no")]
    public string? ProjectNo { get; set; }

    [JsonPropertyName("order_no")]
    public string? OrderNo { get; set; }

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

    [JsonPropertyName("status")]
    public ProductionWorkOrderStatus Status { get; set; }
}

public class ProductionWorkOrderResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("pic_card_id")]
    public int? PicCardId { get; set; }

    [JsonPropertyName("pic_name")]
    public string? PicName { get; set; }

    [JsonPropertyName("employee_no")]
    public string? EmployeeNo { get; set; }

    [JsonPropertyName("operator_shift")]
    public string? OperatorShift { get; set; }

    [JsonPropertyName("operator_department")]
    public string? OperatorDepartment { get; set; }

    [JsonPropertyName("work_shift_code")]
    public string? WorkShiftCode { get; set; }

    [JsonPropertyName("work_shift_name")]
    public string? WorkShiftName { get; set; }

    [JsonPropertyName("work_shift_type")]
    public string? WorkShiftType { get; set; }

    [JsonPropertyName("line_master_id")]
    public int? AreaMasterId { get; set; }

    [JsonPropertyName("line_no")]
    public string? AreaCode { get; set; }

    [JsonPropertyName("line_name")]
    public string? AreaName { get; set; }

    [JsonPropertyName("operators")]
    public List<ProductionOperatorResponse> Operators { get; set; } = [];

    [JsonPropertyName("line_code")]
    public string LineCode { get; set; } = string.Empty;

    [JsonPropertyName("actual_qty")]
    public int ActualQty { get; set; }

    [JsonPropertyName("reject_qty")]
    public int RejectQty { get; set; }

    [JsonPropertyName("status")]
    public ProductionWorkOrderStatus Status { get; set; }

    [JsonPropertyName("plan_date")]
    public DateTime PlanDate { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime? StartedAt { get; set; }

    [JsonPropertyName("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }

    [JsonPropertyName("project_no")]
    public string? ProjectNo { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }
}

public class CreateProductionWorkOrderRequest
{
    [JsonPropertyName("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("line_code")]
    public string? LineCode { get; set; }

    [JsonPropertyName("plan_date")]
    public DateTime? PlanDate { get; set; }

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }

    [JsonPropertyName("project_no")]
    public string? ProjectNo { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

}

public class ScanOrderNumberRequest
{
    [JsonPropertyName("order_number")]
    public string OrderNumber { get; set; } = string.Empty;

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }
}

public class FabShiageProductionResultResponse
{
    [JsonPropertyName("project_no")]
    public string? ProjectNo { get; set; }

    [JsonPropertyName("order_no")]
    public string? OrderNo { get; set; }

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }

    [JsonPropertyName("project_name")]
    public string? ProjectName { get; set; }
}

public class ProductionIntegrationSettingResponse
{
    [JsonPropertyName("setting_key")]
    public string SettingKey { get; set; } = string.Empty;

    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("endpoint_path")]
    public string EndpointPath { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password_set")]
    public bool PasswordSet { get; set; }

    [JsonPropertyName("filter_field_name")]
    public string FilterFieldName { get; set; } = "LOT_NO";

    [JsonPropertyName("top")]
    public int Top { get; set; } = 1;

    [JsonPropertyName("skip")]
    public int Skip { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}

public class SaveProductionIntegrationSettingRequest
{
    [JsonPropertyName("base_url")]
    public string BaseUrl { get; set; } = string.Empty;

    [JsonPropertyName("endpoint_path")]
    public string EndpointPath { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string? Username { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("filter_field_name")]
    public string FilterFieldName { get; set; } = "LOT_NO";

    [JsonPropertyName("top")]
    public int Top { get; set; } = 1;

    [JsonPropertyName("skip")]
    public int Skip { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}

public class InternalSystemLoginTestResponse
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("status_code")]
    public int StatusCode { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("token_preview")]
    public string? TokenPreview { get; set; }

    [JsonPropertyName("response_preview")]
    public string ResponsePreview { get; set; } = string.Empty;
}

public class StartWorkOrderRequest
{
    [JsonPropertyName("line_master_id")]
    public int? AreaMasterId { get; set; }
}

public class ScanPicRequest
{
    [JsonPropertyName("card_uid")]
    public string CardUid { get; set; } = string.Empty;
}

public class RegisterPicCardRequest
{
    [JsonPropertyName("card_uid")]
    public string CardUid { get; set; } = string.Empty;

    [JsonPropertyName("employee_no")]
    public string EmployeeNo { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;
}

public class ProductionOperatorResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("pic_card_id")]
    public int PicCardId { get; set; }

    [JsonPropertyName("card_uid")]
    public string CardUid { get; set; } = string.Empty;

    [JsonPropertyName("employee_no")]
    public string EmployeeNo { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("shift")]
    public string Shift { get; set; } = string.Empty;

    [JsonPropertyName("work_shift_code")]
    public string? WorkShiftCode { get; set; }

    [JsonPropertyName("work_shift_name")]
    public string? WorkShiftName { get; set; }

    [JsonPropertyName("work_shift_type")]
    public string? WorkShiftType { get; set; }

    [JsonPropertyName("scanned_at")]
    public DateTime ScannedAt { get; set; }
}

public class ProductionActiveOperatorResponse
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("pic_card_id")]
    public int PicCardId { get; set; }

    [JsonPropertyName("card_uid")]
    public string CardUid { get; set; } = string.Empty;

    [JsonPropertyName("employee_no")]
    public string EmployeeNo { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("department")]
    public string Department { get; set; } = string.Empty;

    [JsonPropertyName("operator_shift")]
    public string OperatorShift { get; set; } = string.Empty;

    [JsonPropertyName("shift_master_id")]
    public int? ShiftMasterId { get; set; }

    [JsonPropertyName("shift_code")]
    public string? ShiftCode { get; set; }

    [JsonPropertyName("shift_name")]
    public string? ShiftName { get; set; }

    [JsonPropertyName("shift_type")]
    public string? ShiftType { get; set; }

    [JsonPropertyName("scanned_at")]
    public DateTime ScannedAt { get; set; }
}

public class ActiveOperatorSummaryResponse
{
    [JsonPropertyName("current_shift")]
    public ShiftMaster? CurrentShift { get; set; }

    [JsonPropertyName("has_shift_changed")]
    public bool HasShiftChanged { get; set; }

    [JsonPropertyName("operators")]
    public List<ProductionActiveOperatorResponse> Operators { get; set; } = [];
}

public class UpdateProductionRequest
{
    [JsonPropertyName("actual_qty")]
    public int? ActualQty { get; set; }

    [JsonPropertyName("reject_qty")]
    public int? RejectQty { get; set; }

    [JsonPropertyName("remarks")]
    public string? Remarks { get; set; }
}

public class UpdateShiftMasterRequest
{
    [JsonPropertyName("shift_name")]
    public string ShiftName { get; set; } = string.Empty;

    [JsonPropertyName("shift_type")]
    public string? ShiftType { get; set; }

    [JsonPropertyName("start_schedule")]
    public string? StartSchedule { get; set; }

    [JsonPropertyName("finish_schedule")]
    public string? FinishSchedule { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}

public class SaveAreaMasterRequest
{
    [JsonPropertyName("line_no")]
    public string? LineNo { get; set; }

    [JsonPropertyName("line_name")]
    public string? LineName { get; set; }

    [JsonPropertyName("area_code")]
    public string? AreaCode { get; set; }

    [JsonPropertyName("area_name")]
    public string? AreaName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonIgnore]
    public string RawLineNo => LineNo ?? AreaCode ?? string.Empty;

    [JsonIgnore]
    public string RawLineName => LineName ?? AreaName ?? string.Empty;
}

public class SaveUnitMasterRequest
{
    [JsonPropertyName("unit_code")]
    public string UnitCode { get; set; } = string.Empty;

    [JsonPropertyName("unit_name")]
    public string UnitName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}

public class CreateCuttingListRequest
{
    [JsonPropertyName("line_code")]
    public string LineCode { get; set; } = string.Empty;

    [JsonPropertyName("planned_qty")]
    public int PlannedQty { get; set; }

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = "PCS";

    [JsonPropertyName("plan_date")]
    public DateTime PlanDate { get; set; }

    [JsonPropertyName("lot_no")]
    public string? LotNo { get; set; }

    [JsonPropertyName("project_no")]
    public string? ProjectNo { get; set; }

    [JsonPropertyName("weight")]
    public decimal? Weight { get; set; }
}
