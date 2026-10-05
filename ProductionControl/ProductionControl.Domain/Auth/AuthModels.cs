using System.Text.Json.Serialization;

namespace ProductionControl.Domain.Auth;

public enum AppUserRole
{
    ADMIN,
    SUPERVISOR,
    OPERATOR,
    VIEWER
}

public enum AppUserStatus
{
    ACTIVE,
    INACTIVE
}

public class AppRole
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("role_code")]
    public string RoleCode { get; set; } = string.Empty;

    [JsonPropertyName("role_name")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public class RolePageAccess
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("role_id")]
    public int RoleId { get; set; }

    [JsonPropertyName("page_key")]
    public string PageKey { get; set; } = string.Empty;

    [JsonPropertyName("can_access")]
    public bool CanAccess { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public AppRole? Role { get; set; }
}

public class AppUser
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("role_id")]
    public int? RoleId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = AppUserRole.VIEWER.ToString();

    [JsonPropertyName("status")]
    public AppUserStatus Status { get; set; } = AppUserStatus.ACTIVE;

    [JsonIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonIgnore]
    public string PasswordSalt { get; set; } = string.Empty;

    [JsonPropertyName("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public AppRole? RoleMaster { get; set; }
}

public class UserResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("role_id")]
    public int? RoleId { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("role_name")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public AppUserStatus Status { get; set; }

    [JsonPropertyName("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("accessible_pages")]
    public List<string> AccessiblePages { get; set; } = [];
}

public class LoginRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public UserResponse User { get; set; } = new();
}

public class UpdateUserRequest
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("role_id")]
    public int RoleId { get; set; }

    [JsonPropertyName("status")]
    public AppUserStatus Status { get; set; } = AppUserStatus.ACTIVE;

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class CreateUserRequest : UpdateUserRequest
{
}

public class SaveRoleRequest
{
    [JsonPropertyName("role_code")]
    public string RoleCode { get; set; } = string.Empty;

    [JsonPropertyName("role_name")]
    public string RoleName { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; } = true;
}

public class PageAccessDefinitionResponse
{
    [JsonPropertyName("page_key")]
    public string PageKey { get; set; } = string.Empty;

    [JsonPropertyName("page_name")]
    public string PageName { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("group_name")]
    public string GroupName { get; set; } = string.Empty;
}

public class RolePageAccessResponse : PageAccessDefinitionResponse
{
    [JsonPropertyName("can_access")]
    public bool CanAccess { get; set; }
}

public class SaveRolePageAccessRequest
{
    [JsonPropertyName("page_keys")]
    public List<string> PageKeys { get; set; } = [];
}
