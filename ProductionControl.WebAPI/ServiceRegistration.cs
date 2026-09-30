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
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyMethod()
                    .AllowAnyHeader();

                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins);
                }
                else
                {
                    policy.AllowAnyOrigin();
                }
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

        app.UseCors();
        app.UseHttpsRedirection();
        app.UseRouting();
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

            if (ColumnExists(connection, "line_master", "area_code") && !ColumnExists(connection, "line_master", "line_no"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` CHANGE COLUMN `area_code` `line_no` VARCHAR(50) NOT NULL;");
            }

            if (ColumnExists(connection, "line_master", "area_name") && !ColumnExists(connection, "line_master", "line_name"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `line_master` CHANGE COLUMN `area_name` `line_name` VARCHAR(150) NOT NULL;");
            }

            if (ColumnExists(connection, "production_work_orders", "area_master_id") &&
                !ColumnExists(connection, "production_work_orders", "line_master_id"))
            {
                ExecuteNonQuery(connection, "ALTER TABLE `production_work_orders` CHANGE COLUMN `area_master_id` `line_master_id` INT NULL;");
            }

            DropLegacyProductionLineMaster(connection);
            EnsureProductionIntegrationSettings(connection);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Production schema repair skipped.");
        }
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

        ExecuteNonQuery(connection, """
            INSERT INTO `production_integration_settings`
                (`setting_key`, `base_url`, `endpoint_path`, `filter_field_name`, `top`, `skip`, `is_active`)
            VALUES
                ('shiage_lot_no', '', '/fab-shiage-prod-res/', 'LOT_NO', 1, 0, 0),
                ('internal_system_auth', '', '/auth/login', '-', 1, 0, 1)
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
