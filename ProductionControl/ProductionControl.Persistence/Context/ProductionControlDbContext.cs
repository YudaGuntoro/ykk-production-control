using ProductionControl.Domain.Auth;
using ProductionControl.Domain.Production;
using Microsoft.EntityFrameworkCore;

namespace ProductionControl.Persistence.Context;

public class ProductionControlDbContext : DbContext
{
    public ProductionControlDbContext(DbContextOptions<ProductionControlDbContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppRole> UserRoles => Set<AppRole>();
    public DbSet<StatusMaster> StatusMasters => Set<StatusMaster>();
    public DbSet<PicCard> PicCards => Set<PicCard>();
    public DbSet<ShiftMaster> ShiftMasters => Set<ShiftMaster>();
    public DbSet<AreaMaster> AreaMasters => Set<AreaMaster>();
    public DbSet<ProjectMaster> ProjectMasters => Set<ProjectMaster>();
    public DbSet<UnitMaster> UnitMasters => Set<UnitMaster>();
    public DbSet<ProductionIntegrationSetting> ProductionIntegrationSettings => Set<ProductionIntegrationSetting>();
    public DbSet<ReleaseProductionOrderDetail> ReleaseProductionOrderDetails => Set<ReleaseProductionOrderDetail>();
    public DbSet<ProductionWorkOrder> ProductionWorkOrders => Set<ProductionWorkOrder>();
    public DbSet<ProductionWorkOrderOperator> ProductionWorkOrderOperators => Set<ProductionWorkOrderOperator>();
    public DbSet<ProductionWorkOrderOperatorSnapshot> ProductionWorkOrderOperatorSnapshots => Set<ProductionWorkOrderOperatorSnapshot>();
    public DbSet<ProductionActiveOperator> ProductionActiveOperators => Set<ProductionActiveOperator>();
    public DbSet<ProductionActivityLog> ProductionActivityLogs => Set<ProductionActivityLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasColumnName("username").HasMaxLength(80).IsRequired();
            entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
            entity.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(50);
            entity.Property(x => x.RoleId).HasColumnName("role_id");
            entity.Property(x => x.Role).HasColumnName("role").HasMaxLength(50);
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
            entity.Property(x => x.PasswordSalt).HasColumnName("password_salt").HasMaxLength(255).IsRequired();
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(x => x.RoleMaster).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => x.RoleId);
        });

        modelBuilder.Entity<AppRole>(entity =>
        {
            entity.ToTable("user_roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RoleCode).HasColumnName("role_code").HasMaxLength(50).IsRequired();
            entity.Property(x => x.RoleName).HasColumnName("role_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.RoleCode).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.RoleName });
        });

        modelBuilder.Entity<StatusMaster>(entity =>
        {
            entity.ToTable("status_masters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StatusGroup).HasColumnName("status_group").HasMaxLength(50).IsRequired();
            entity.Property(x => x.StatusCode).HasColumnName("status_code").HasMaxLength(50).IsRequired();
            entity.Property(x => x.StatusName).HasColumnName("status_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => new { x.StatusGroup, x.StatusCode }).IsUnique();
        });

        modelBuilder.Entity<PicCard>(entity =>
        {
            entity.ToTable("operator_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CardUid).HasColumnName("card_uid").HasMaxLength(80).IsRequired();
            entity.Property(x => x.EmployeeNo).HasColumnName("employee_no").HasMaxLength(50).IsRequired();
            entity.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(150).IsRequired();
            entity.Property(x => x.Department).HasColumnName("department").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Shift).HasColumnName("shift").HasMaxLength(30).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.LastScannedAt).HasColumnName("last_scanned_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.CardUid).IsUnique();
            entity.HasIndex(x => x.EmployeeNo).IsUnique();
        });

        modelBuilder.Entity<ShiftMaster>(entity =>
        {
            entity.ToTable("shift_masters");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ShiftCode).HasColumnName("shift_code").HasMaxLength(50).IsRequired();
            entity.Property(x => x.ShiftName).HasColumnName("shift_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ShiftType).HasColumnName("shift_type").HasMaxLength(30);
            entity.Property(x => x.StartSchedule).HasColumnName("start_schedule").HasColumnType("time");
            entity.Property(x => x.FinishSchedule).HasColumnName("finish_schedule").HasColumnType("time");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasIndex(x => x.ShiftCode).IsUnique();
        });

        modelBuilder.Entity<AreaMaster>(entity =>
        {
            entity.ToTable("line_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.AreaCode).HasColumnName("line_no").HasMaxLength(50).IsRequired();
            entity.Property(x => x.AreaName).HasColumnName("line_name").HasMaxLength(150).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.AreaCode).IsUnique();
            entity.HasIndex(x => x.AreaName).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.AreaName });
        });

        modelBuilder.Entity<ProjectMaster>(entity =>
        {
            entity.ToTable("project_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProjectNo).HasColumnName("project_no").HasMaxLength(80).IsRequired();
            entity.Property(x => x.ProjectName).HasColumnName("project_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.ProjectNo).IsUnique();
        });

        modelBuilder.Entity<UnitMaster>(entity =>
        {
            entity.ToTable("unit_master");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UnitCode).HasColumnName("unit_code").HasMaxLength(20).IsRequired();
            entity.Property(x => x.UnitName).HasColumnName("unit_name").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(255);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.UnitCode).IsUnique();
            entity.HasIndex(x => x.UnitName).IsUnique();
            entity.HasIndex(x => new { x.IsActive, x.UnitName });
        });

        modelBuilder.Entity<ProductionIntegrationSetting>(entity =>
        {
            entity.ToTable("production_integration_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SettingKey).HasColumnName("setting_key").HasMaxLength(80).IsRequired();
            entity.Property(x => x.BaseUrl).HasColumnName("base_url").HasMaxLength(500).IsRequired();
            entity.Property(x => x.EndpointPath).HasColumnName("endpoint_path").HasMaxLength(255).IsRequired();
            entity.Property(x => x.Username).HasColumnName("auth_username").HasMaxLength(150);
            entity.Property(x => x.Password).HasColumnName("auth_password").HasMaxLength(500);
            entity.Property(x => x.FilterFieldName).HasColumnName("filter_field_name").HasMaxLength(80).IsRequired();
            entity.Property(x => x.Top).HasColumnName("top");
            entity.Property(x => x.Skip).HasColumnName("skip");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => x.SettingKey).IsUnique();
        });

        modelBuilder.Entity<ReleaseProductionOrderDetail>(entity =>
        {
            entity.ToTable("release_production_order_details");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductionWorkOrderId).HasColumnName("production_work_order_id");
            entity.Property(x => x.OrderNo).HasColumnName("order_no").HasMaxLength(80);
            entity.Property(x => x.LotNo).HasColumnName("lot_no").HasMaxLength(80);
            entity.Property(x => x.ProjectNo).HasColumnName("project_no").HasMaxLength(80);
            entity.Property(x => x.ProjectMasterId).HasColumnName("project_master_id");
            entity.Property(x => x.Weight).HasColumnName("weight").HasPrecision(12, 3);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(x => x.ProductionWorkOrder).WithOne(x => x.ReleaseProductionOrderDetail).HasForeignKey<ReleaseProductionOrderDetail>(x => x.ProductionWorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ProjectMaster).WithMany().HasForeignKey(x => x.ProjectMasterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.ProductionWorkOrderId).IsUnique();
            entity.HasIndex(x => x.OrderNo);
            entity.HasIndex(x => x.LotNo);
            entity.HasIndex(x => x.ProjectNo);
            entity.HasIndex(x => x.ProjectMasterId);
        });

        modelBuilder.Entity<ProductionWorkOrder>(entity =>
        {
            entity.ToTable("production_work_orders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OrderNumber).HasColumnName("order_number").HasMaxLength(80).IsRequired();
            entity.Property(x => x.ShiftMasterId).HasColumnName("shift_master_id");
            entity.Property(x => x.AreaMasterId).HasColumnName("line_master_id");
            entity.Property(x => x.LineCode).HasColumnName("line_code").HasMaxLength(50).IsRequired();
            entity.Property(x => x.PlanDate).HasColumnName("plan_date");
            entity.Property(x => x.TargetQty).HasColumnName("target_qty");
            entity.Property(x => x.ActualQty).HasColumnName("actual_qty");
            entity.Property(x => x.RejectQty).HasColumnName("reject_qty");
            entity.Property(x => x.StatusMasterId).HasColumnName("status_master_id");
            entity.Property(x => x.StartedAt).HasColumnName("started_at");
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(x => x.ShiftMaster).WithMany().HasForeignKey(x => x.ShiftMasterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AreaMaster).WithMany().HasForeignKey(x => x.AreaMasterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.StatusMaster).WithMany().HasForeignKey(x => x.StatusMasterId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.OrderNumber).IsUnique();
            entity.HasIndex(x => new { x.StatusMasterId, x.LineCode });
            entity.HasIndex(x => x.ShiftMasterId);
            entity.HasIndex(x => x.AreaMasterId);
        });

        modelBuilder.Entity<ProductionWorkOrderOperator>(entity =>
        {
            entity.ToTable("production_work_order_operators");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductionWorkOrderId).HasColumnName("production_work_order_id");
            entity.Property(x => x.PicCardId).HasColumnName("pic_card_id");
            entity.Property(x => x.ProductionActiveOperatorId).HasColumnName("production_active_operator_id");
            entity.Property(x => x.ShiftMasterId).HasColumnName("shift_master_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.ScannedAt).HasColumnName("scanned_at");
            entity.Property(x => x.RemovedAt).HasColumnName("removed_at");
            entity.HasOne(x => x.ProductionWorkOrder).WithMany(x => x.Operators).HasForeignKey(x => x.ProductionWorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PicCard).WithMany().HasForeignKey(x => x.PicCardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductionActiveOperator).WithMany().HasForeignKey(x => x.ProductionActiveOperatorId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ShiftMaster).WithMany().HasForeignKey(x => x.ShiftMasterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.ProductionWorkOrderId, x.IsActive });
            entity.HasIndex(x => x.PicCardId);
            entity.HasIndex(x => x.ProductionActiveOperatorId);
            entity.HasIndex(x => x.ShiftMasterId);
        });

        modelBuilder.Entity<ProductionActiveOperator>(entity =>
        {
            entity.ToTable("production_active_operators");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PicCardId).HasColumnName("pic_card_id");
            entity.Property(x => x.ShiftMasterId).HasColumnName("shift_master_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.ScannedAt).HasColumnName("scanned_at");
            entity.Property(x => x.RemovedAt).HasColumnName("removed_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasOne(x => x.PicCard).WithMany().HasForeignKey(x => x.PicCardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ShiftMaster).WithMany().HasForeignKey(x => x.ShiftMasterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.IsActive, x.ScannedAt });
            entity.HasIndex(x => new { x.PicCardId, x.IsActive });
            entity.HasIndex(x => x.ShiftMasterId);
        });

        modelBuilder.Entity<ProductionWorkOrderOperatorSnapshot>(entity =>
        {
            entity.ToTable("production_work_order_operator_snapshots");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductionWorkOrderId).HasColumnName("production_work_order_id");
            entity.Property(x => x.PicCardId).HasColumnName("pic_card_id");
            entity.Property(x => x.ProductionActiveOperatorId).HasColumnName("production_active_operator_id");
            entity.Property(x => x.ShiftMasterId).HasColumnName("shift_master_id");
            entity.Property(x => x.SnapshotType).HasColumnName("snapshot_type").HasConversion<string>().HasMaxLength(30);
            entity.Property(x => x.ScannedAt).HasColumnName("scanned_at");
            entity.Property(x => x.SnapshotAt).HasColumnName("snapshot_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne(x => x.ProductionWorkOrder).WithMany(x => x.OperatorSnapshots).HasForeignKey(x => x.ProductionWorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.PicCard).WithMany().HasForeignKey(x => x.PicCardId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ProductionActiveOperator).WithMany().HasForeignKey(x => x.ProductionActiveOperatorId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ShiftMaster).WithMany().HasForeignKey(x => x.ShiftMasterId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.ProductionWorkOrderId, x.SnapshotType });
            entity.HasIndex(x => new { x.ProductionWorkOrderId, x.SnapshotType, x.PicCardId }).IsUnique();
            entity.HasIndex(x => x.PicCardId);
            entity.HasIndex(x => x.ProductionActiveOperatorId);
            entity.HasIndex(x => x.ShiftMasterId);
        });

        modelBuilder.Entity<ProductionActivityLog>(entity =>
        {
            entity.ToTable("production_activity_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProductionWorkOrderId).HasColumnName("production_work_order_id");
            entity.Property(x => x.UserId).HasColumnName("user_id");
            entity.Property(x => x.ActivityType).HasColumnName("activity_type").HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Remarks).HasColumnName("remarks").HasColumnType("text");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.HasOne(x => x.ProductionWorkOrder).WithMany(x => x.ActivityLogs).HasForeignKey(x => x.ProductionWorkOrderId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.ProductionWorkOrderId, x.CreatedAt });
            entity.HasIndex(x => x.UserId);
        });
    }
}
