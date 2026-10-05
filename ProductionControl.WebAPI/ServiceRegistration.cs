using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using ProductionControl.Persistence.Context;
using ProductionControl.Persistence.IoC;
using ProductionControl.Persistence.Services.AuthService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace ProductionControl.WebAPI;

public static class ServiceRegistration
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddEndpointsApiExplorer();
        services.AddHttpClient();
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "Production Control Monitoring API", Version = "v1" });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header. Example: Bearer {token}",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        var allowedOrigins = (configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .ToArray();
        var allowAnyOrigin = allowedOrigins.Length == 0 || allowedOrigins.Any(origin => origin.Trim() == "*");
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyMethod()
                    .AllowAnyHeader();

                policy.SetIsOriginAllowed(origin =>
                    allowAnyOrigin || allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase));
            });
        });

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role
                };
            });

        services.AddAuthorization(options =>
        {
            var authenticatedPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();

            options.DefaultPolicy = authenticatedPolicy;
            options.FallbackPolicy = authenticatedPolicy;
        });

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=127.0.0.1;Port=3306;User ID=root;Password=YOUR_PASSWORD;Database=db_production_control;SslMode=None;AllowPublicKeyRetrieval=True;";
        services.AddDbContext<ProductionControlDbContext>(options =>
        {
            options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 34)));
        });

        services.AddIoCService();
    }

    public static void UseProductionControlApiPipeline(this WebApplication app)
    {
        RepairProductionSchema(app);

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseForwardedHeaders();

        var swaggerEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false);
        if (swaggerEnabled)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Production Control Monitoring API v1"));
        }

        app.UseHttpsRedirection();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
    }

    private static void RepairProductionSchema(WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ProductionControlDbContext>();
            var connection = db.Database.GetDbConnection();
            connection.Open();

            if (TableExists(connection, "area_master") && !TableExists(connection, "line_master"))
            {
                ExecuteNonQuery(connection, "RENAME TABLE `area_master` TO `line_master`;");
            }

            if (!TableExists(connection, "line_master"))
            {
                ExecuteNonQuery(connection, """
                    CREATE TABLE `line_master` (
                        `id` INT AUTO_INCREMENT PRIMARY KEY,
                        `line_no` VARCHAR(50) NOT NULL,
                        `line_name` VARCHAR(150) NOT NULL,
                        `description` VARCHAR(255) NULL,
                        `is_active` TINYINT(1) NOT NULL DEFAULT 1,
                        `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                        UNIQUE KEY `uq_line_master_no` (`line_no`),
                        UNIQUE KEY `uq_line_master_name` (`line_name`),
                        KEY `ix_line_master_active_name` (`is_active`, `line_name`)
                    );
                    """);
            }

            EnsureLineMasterColumns(connection);

            if (ColumnExists(connection, "production_work_orders", "area_master_id") &&
                !ColumnExists(connection, "production_work_orders", "line_master_id"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `production_work_orders` CHANGE COLUMN `area_master_id` `line_master_id` INT NULL;");
            }

            EnsureProductionWorkOrderSchema(connection);
            EnsureProductionIntegrationSettings(connection);
            EnsureUserRoleSchema(connection);
            EnsureReleaseProductionOrderDetailSchema(connection);
            EnsureProductionOperatorSchema(connection);
            EnsureProductionActivityLogSchema(connection);
            DropLegacyProductionLineMaster(connection);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Production schema repair skipped.");
        }
    }

    private static void EnsureProductionWorkOrderSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "production_work_orders"))
        {
            return;
        }

        AddColumnIfMissing(connection, "production_work_orders", "shift_master_id", "INT NULL");
        if (ColumnExists(connection, "production_work_orders", "area_master_id") &&
            !ColumnExists(connection, "production_work_orders", "line_master_id"))
        {
            ExecuteNonQuery(connection, "ALTER TABLE `production_work_orders` CHANGE COLUMN `area_master_id` `line_master_id` INT NULL;");
        }
        else
        {
            AddColumnIfMissing(connection, "production_work_orders", "line_master_id", "INT NULL");
        }

        AddColumnIfMissing(connection, "production_work_orders", "line_code", "VARCHAR(50) NOT NULL DEFAULT '-'");
        AddColumnIfMissing(connection, "production_work_orders", "target_qty", "INT NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "production_work_orders", "actual_qty", "INT NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "production_work_orders", "reject_qty", "INT NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "production_work_orders", "started_at", "DATETIME NULL");
        AddColumnIfMissing(connection, "production_work_orders", "completed_at", "DATETIME NULL");
        AddColumnIfMissing(connection, "production_work_orders", "created_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddColumnIfMissing(connection, "production_work_orders", "updated_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

        if (!ColumnExists(connection, "production_work_orders", "plan_date"))
        {
            ExecuteNonQuery(connection, "ALTER TABLE `production_work_orders` ADD COLUMN `plan_date` DATE NULL;");
            ExecuteNonQuery(connection, "UPDATE `production_work_orders` SET `plan_date` = DATE(COALESCE(`created_at`, CURRENT_TIMESTAMP)) WHERE `plan_date` IS NULL;");
        }

        if (!ColumnExists(connection, "production_work_orders", "status_master_id"))
        {
            ExecuteNonQuery(connection, "ALTER TABLE `production_work_orders` ADD COLUMN `status_master_id` INT NULL;");
        }

        if (ColumnExists(connection, "production_work_orders", "status"))
        {
            ExecuteNonQuery(connection, """
                UPDATE `production_work_orders`
                SET `status_master_id` = CASE
                    WHEN `status` IN ('COMPLETED', 'FINISH') OR `completed_at` IS NOT NULL THEN 3
                    WHEN `status` IN ('IN_PROGRESS', 'HOLD') OR `started_at` IS NOT NULL THEN 2
                    ELSE 1
                END
                WHERE `status_master_id` IS NULL;
                """);
        }

        ExecuteNonQuery(connection, "UPDATE `production_work_orders` SET `status_master_id` = 1 WHERE `status_master_id` IS NULL;");
        ExecuteNonQuery(connection, "UPDATE `production_work_orders` SET `plan_date` = DATE(COALESCE(`created_at`, CURRENT_TIMESTAMP)) WHERE `plan_date` IS NULL;");

        AddIndexIfMissing(connection, "production_work_orders", "ix_production_work_orders_status_line", "(`status_master_id`, `line_code`)");
        AddIndexIfMissing(connection, "production_work_orders", "ix_production_work_orders_plan_line", "(`plan_date`, `line_code`)");
        AddIndexIfMissing(connection, "production_work_orders", "ix_production_work_orders_shift_master", "(`shift_master_id`)");
        AddIndexIfMissing(connection, "production_work_orders", "ix_production_work_orders_line_master", "(`line_master_id`)");
    }

    private static void EnsureUserRoleSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "user_roles"))
        {
            ExecuteNonQuery(connection, """
                CREATE TABLE `user_roles` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `role_code` VARCHAR(50) NOT NULL,
                    `role_name` VARCHAR(100) NOT NULL,
                    `description` VARCHAR(255) NULL,
                    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
                    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE KEY `uq_user_roles_code` (`role_code`),
                    KEY `ix_user_roles_active_name` (`is_active`, `role_name`)
                );
                """);
        }

        ExecuteNonQuery(connection, """
            INSERT INTO `user_roles` (`role_code`, `role_name`, `description`, `is_active`)
            VALUES
                ('ADMIN', 'Admin', 'Full access user.', 1),
                ('SUPERVISOR', 'Supervisor', 'Production supervisor user.', 1),
                ('OPERATOR', 'Operator', 'Production operator user.', 1),
                ('VIEWER', 'Viewer', 'Read only user.', 1)
            ON DUPLICATE KEY UPDATE
                `role_name` = VALUES(`role_name`),
                `description` = IF(`description` IS NULL OR `description` = '', VALUES(`description`), `description`),
                `updated_at` = CURRENT_TIMESTAMP;
            """);

        if (!TableExists(connection, "users"))
        {
            return;
        }

        AddColumnIfMissing(connection, "users", "role_id", "INT NULL");
        if (!ColumnExists(connection, "users", "role"))
        {
            AddColumnIfMissing(connection, "users", "role", "VARCHAR(50) NOT NULL DEFAULT 'VIEWER'");
        }

        ExecuteNonQuery(connection, """
            UPDATE `users` u
            JOIN `user_roles` r ON r.`role_code` = UPPER(COALESCE(NULLIF(u.`role`, ''), 'VIEWER'))
            SET u.`role_id` = r.`id`,
                u.`role` = r.`role_code`
            WHERE u.`role_id` IS NULL;
            """);

        ExecuteNonQuery(connection, """
            UPDATE `users` u
            JOIN `user_roles` r ON r.`role_code` = 'VIEWER'
            SET u.`role_id` = r.`id`,
                u.`role` = r.`role_code`
            WHERE u.`role_id` IS NULL;
            """);

        AddIndexIfMissing(connection, "users", "ix_users_role_id", "(`role_id`)");
        EnsureRolePageAccessSchema(connection);
    }

    private static void EnsureRolePageAccessSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "role_page_access"))
        {
            ExecuteNonQuery(connection, """
                CREATE TABLE `role_page_access` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `role_id` INT NOT NULL,
                    `page_key` VARCHAR(80) NOT NULL,
                    `can_access` TINYINT(1) NOT NULL DEFAULT 0,
                    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE KEY `uq_role_page_access_role_page` (`role_id`, `page_key`),
                    KEY `ix_role_page_access_page` (`page_key`),
                    CONSTRAINT `fk_role_page_access_role` FOREIGN KEY (`role_id`) REFERENCES `user_roles` (`id`) ON DELETE CASCADE
                );
                """);
        }

        AddColumnIfMissing(connection, "role_page_access", "role_id", "INT NOT NULL");
        AddColumnIfMissing(connection, "role_page_access", "page_key", "VARCHAR(80) NOT NULL");
        AddColumnIfMissing(connection, "role_page_access", "can_access", "TINYINT(1) NOT NULL DEFAULT 0");
        AddColumnIfMissing(connection, "role_page_access", "created_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddColumnIfMissing(connection, "role_page_access", "updated_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
        AddUniqueIndexIfMissing(connection, "role_page_access", "uq_role_page_access_role_page", "(`role_id`, `page_key`)");
        AddIndexIfMissing(connection, "role_page_access", "ix_role_page_access_page", "(`page_key`)");

        ExecuteNonQuery(connection, """
            INSERT INTO `role_page_access` (`role_id`, `page_key`, `can_access`)
            SELECT r.`id`, pages.`page_key`, 1
            FROM `user_roles` r
            JOIN (
                SELECT 'dashboard' AS `page_key`
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
            WHERE r.`role_code` <> 'ADMIN'
            ON DUPLICATE KEY UPDATE `can_access` = `can_access`;
            """);
    }

    private static void EnsureLineMasterColumns(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "line_master"))
        {
            return;
        }

        if (!ColumnExists(connection, "line_master", "line_no"))
        {
            if (ColumnExists(connection, "line_master", "area_code"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` CHANGE COLUMN `area_code` `line_no` VARCHAR(50) NULL;");
            }
            else if (ColumnExists(connection, "line_master", "line_code"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` CHANGE COLUMN `line_code` `line_no` VARCHAR(50) NULL;");
            }
            else
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` ADD COLUMN `line_no` VARCHAR(50) NULL;");
            }

            ExecuteNonQuery(connection, "UPDATE `line_master` SET `line_no` = CONCAT('LINE-', LPAD(`id`, 2, '0')) WHERE `line_no` IS NULL OR `line_no` = '';");
            ExecuteNonQuery(connection, "ALTER TABLE `line_master` MODIFY COLUMN `line_no` VARCHAR(50) NOT NULL;");
        }

        if (!ColumnExists(connection, "line_master", "line_name"))
        {
            if (ColumnExists(connection, "line_master", "area_name"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` CHANGE COLUMN `area_name` `line_name` VARCHAR(150) NULL;");
            }
            else if (ColumnExists(connection, "line_master", "name"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` CHANGE COLUMN `name` `line_name` VARCHAR(150) NULL;");
            }
            else
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` ADD COLUMN `line_name` VARCHAR(150) NULL;");
            }

            ExecuteNonQuery(connection, "UPDATE `line_master` SET `line_name` = `line_no` WHERE `line_name` IS NULL OR `line_name` = '';");
            ExecuteNonQuery(connection, "ALTER TABLE `line_master` MODIFY COLUMN `line_name` VARCHAR(150) NOT NULL;");
        }

        AddColumnIfMissing(connection, "line_master", "description", "VARCHAR(255) NULL");
        AddColumnIfMissing(connection, "line_master", "is_active", "TINYINT(1) NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "line_master", "created_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddColumnIfMissing(connection, "line_master", "updated_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
    }

    private static void EnsureReleaseProductionOrderDetailSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "release_production_order_details"))
        {
            ExecuteNonQuery(connection, """
                CREATE TABLE `release_production_order_details` (
                    `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
                    `production_work_order_id` INT NOT NULL,
                    `order_no` VARCHAR(80) NULL,
                    `lot_no` VARCHAR(80) NULL,
                    `project_no` VARCHAR(80) NULL,
                    `project_master_id` INT NULL,
                    `weight` DECIMAL(12,3) NULL,
                    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE KEY `uq_rpo_details_work_order` (`production_work_order_id`),
                    KEY `ix_rpo_details_order_no` (`order_no`),
                    KEY `ix_rpo_details_lot_no` (`lot_no`),
                    KEY `ix_rpo_details_project_no` (`project_no`),
                    KEY `ix_rpo_details_project` (`project_master_id`)
                );
                """);
            return;
        }

        AddColumnIfMissing(connection, "release_production_order_details", "production_work_order_id", "INT NOT NULL");
        AddColumnIfMissing(connection, "release_production_order_details", "order_no", "VARCHAR(80) NULL");
        AddColumnIfMissing(connection, "release_production_order_details", "lot_no", "VARCHAR(80) NULL");
        AddColumnIfMissing(connection, "release_production_order_details", "project_no", "VARCHAR(80) NULL");
        AddColumnIfMissing(connection, "release_production_order_details", "project_master_id", "INT NULL");
        AddColumnIfMissing(connection, "release_production_order_details", "weight", "DECIMAL(12,3) NULL");
        AddColumnIfMissing(connection, "release_production_order_details", "created_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddColumnIfMissing(connection, "release_production_order_details", "updated_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");

        AddIndexIfMissing(connection, "release_production_order_details", "ix_rpo_details_order_no", "(`order_no`)");
        AddIndexIfMissing(connection, "release_production_order_details", "ix_rpo_details_lot_no", "(`lot_no`)");
        AddIndexIfMissing(connection, "release_production_order_details", "ix_rpo_details_project_no", "(`project_no`)");
        AddIndexIfMissing(connection, "release_production_order_details", "ix_rpo_details_project", "(`project_master_id`)");
    }

    private static void EnsureProductionOperatorSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "production_work_order_operators"))
        {
            return;
        }

        AddColumnIfMissing(connection, "production_work_order_operators", "production_active_operator_id", "BIGINT NULL");
        AddColumnIfMissing(connection, "production_work_order_operators", "shift_master_id", "INT NULL");
        AddColumnIfMissing(connection, "production_work_order_operators", "is_active", "TINYINT(1) NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "production_work_order_operators", "scanned_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddColumnIfMissing(connection, "production_work_order_operators", "removed_at", "DATETIME NULL");
        AddIndexIfMissing(connection, "production_work_order_operators", "ix_production_wo_operators_active", "(`production_work_order_id`, `is_active`)");
        AddIndexIfMissing(connection, "production_work_order_operators", "ix_production_wo_operators_active_operator", "(`production_active_operator_id`)");
        AddIndexIfMissing(connection, "production_work_order_operators", "ix_production_wo_operators_shift", "(`shift_master_id`)");
    }

    private static void EnsureProductionActivityLogSchema(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "production_activity_logs"))
        {
            return;
        }

        AddColumnIfMissing(connection, "production_activity_logs", "user_id", "INT NULL");
        AddColumnIfMissing(connection, "production_activity_logs", "remarks", "TEXT NULL");
        AddColumnIfMissing(connection, "production_activity_logs", "created_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP");
        AddIndexIfMissing(connection, "production_activity_logs", "ix_production_activity_logs_user", "(`user_id`)");
    }

    private static void DropLegacyProductionLineMaster(System.Data.Common.DbConnection connection)
    {
        if (ConstraintExists(connection, "release_production_order_details", "fk_rpo_details_line"))
        {
            ExecuteNonQuery(connection, "ALTER TABLE `release_production_order_details` DROP FOREIGN KEY `fk_rpo_details_line`;");
        }

        if (IndexExists(connection, "release_production_order_details", "ix_rpo_details_line"))
        {
            ExecuteNonQuery(connection, "ALTER TABLE `release_production_order_details` DROP INDEX `ix_rpo_details_line`;");
        }

        if (ColumnExists(connection, "release_production_order_details", "production_line_master_id"))
        {
            ExecuteNonQuery(connection, "ALTER TABLE `release_production_order_details` DROP COLUMN `production_line_master_id`;");
        }

        if (TableExists(connection, "production_line_master"))
        {
            ExecuteNonQuery(connection, "DROP TABLE `production_line_master`;");
        }
    }

    private static void EnsureProductionIntegrationSettings(System.Data.Common.DbConnection connection)
    {
        if (!TableExists(connection, "production_integration_settings"))
        {
            ExecuteNonQuery(connection, """
                CREATE TABLE `production_integration_settings` (
                    `id` INT AUTO_INCREMENT PRIMARY KEY,
                    `setting_key` VARCHAR(80) NOT NULL,
                    `base_url` VARCHAR(500) NOT NULL,
                    `endpoint_path` VARCHAR(255) NOT NULL,
                    `auth_username` VARCHAR(150) NULL,
                    `auth_password` VARCHAR(500) NULL,
                    `filter_field_name` VARCHAR(80) NOT NULL DEFAULT 'LOT_NO',
                    `top` INT NOT NULL DEFAULT 1,
                    `skip` INT NOT NULL DEFAULT 0,
                    `is_active` TINYINT(1) NOT NULL DEFAULT 1,
                    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    UNIQUE KEY `uq_production_integration_settings_key` (`setting_key`)
                );
                """);
        }

        AddColumnIfMissing(connection, "production_integration_settings", "auth_username", "VARCHAR(150) NULL");
        AddColumnIfMissing(connection, "production_integration_settings", "auth_password", "VARCHAR(500) NULL");

        ExecuteNonQuery(connection, """
            INSERT INTO `production_integration_settings`
                (`setting_key`, `base_url`, `endpoint_path`, `auth_username`, `auth_password`, `filter_field_name`, `top`, `skip`, `is_active`)
            VALUES
                ('shiage_lot_no', '', '/fab-shiage-prod-res/', NULL, NULL, 'LOT_NO', 1, 0, 0),
                ('internal_system_auth', '', '/auth/login', NULL, NULL, '-', 1, 0, 1),
                ('internal_system_refresh', '', '/auth/refresh', NULL, NULL, '-', 1, 0, 1)
            ON DUPLICATE KEY UPDATE
                `endpoint_path` = IF(`endpoint_path` IS NULL OR `endpoint_path` = '', VALUES(`endpoint_path`), `endpoint_path`),
                `filter_field_name` = IF(`filter_field_name` IS NULL OR `filter_field_name` = '', VALUES(`filter_field_name`), `filter_field_name`),
                `top` = IF(`top` IS NULL OR `top` < 1, VALUES(`top`), `top`),
                `skip` = IF(`skip` IS NULL OR `skip` < 0, VALUES(`skip`), `skip`),
                `updated_at` = CURRENT_TIMESTAMP;
            """);
    }

    private static bool TableExists(System.Data.Common.DbConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.TABLES
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName;
            """;
        AddParameter(command, "@tableName", tableName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ColumnExists(System.Data.Common.DbConnection connection, string tableName, string columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName
              AND COLUMN_NAME = @columnName;
            """;
        AddParameter(command, "@tableName", tableName);
        AddParameter(command, "@columnName", columnName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool ConstraintExists(System.Data.Common.DbConnection connection, string tableName, string constraintName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName
              AND CONSTRAINT_NAME = @constraintName;
            """;
        AddParameter(command, "@tableName", tableName);
        AddParameter(command, "@constraintName", constraintName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static bool IndexExists(System.Data.Common.DbConnection connection, string tableName, string indexName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = @tableName
              AND INDEX_NAME = @indexName;
            """;
        AddParameter(command, "@tableName", tableName);
        AddParameter(command, "@indexName", indexName);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static void AddColumnIfMissing(
        System.Data.Common.DbConnection connection,
        string tableName,
        string columnName,
        string definition)
    {
        if (!ColumnExists(connection, tableName, columnName))
        {
            ExecuteNonQuery(connection, $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {definition};");
        }
    }

    private static void AddIndexIfMissing(
        System.Data.Common.DbConnection connection,
        string tableName,
        string indexName,
        string columns)
    {
        if (!IndexExists(connection, tableName, indexName))
        {
            ExecuteNonQuery(connection, $"ALTER TABLE `{tableName}` ADD INDEX `{indexName}` {columns};");
        }
    }

    private static void AddUniqueIndexIfMissing(
        System.Data.Common.DbConnection connection,
        string tableName,
        string indexName,
        string columns)
    {
        if (!IndexExists(connection, tableName, indexName))
        {
            ExecuteNonQuery(connection, $"ALTER TABLE `{tableName}` ADD UNIQUE INDEX `{indexName}` {columns};");
        }
    }

    private static void ExecuteNonQuery(System.Data.Common.DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
