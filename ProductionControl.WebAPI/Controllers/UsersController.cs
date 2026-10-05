using ProductionControl.Domain.Auth;
using ProductionControl.Persistence.Context;
using ProductionControl.Persistence.Services.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace ProductionControl.WebAPI.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly ProductionControlDbContext _db;
    private static readonly PageAccessDefinitionResponse[] PageDefinitions =
    [
        new() { PageKey = "dashboard", PageName = "Dashboard", Path = "/", GroupName = "Main" },
        new() { PageKey = "production_control", PageName = "Production Control", Path = "/production-control", GroupName = "Operation" },
        new() { PageKey = "shift_master", PageName = "Shift", Path = "/shift-master", GroupName = "Master Data" },
        new() { PageKey = "line_master", PageName = "Line", Path = "/line-master", GroupName = "Master Data" },
        new() { PageKey = "operator_list", PageName = "Operator List", Path = "/pic-cards", GroupName = "Master Data" },
        new() { PageKey = "users", PageName = "Users", Path = "/users", GroupName = "Master Data" },
        new() { PageKey = "role_access", PageName = "Role Access", Path = "/role-access", GroupName = "Master Data" },
        new() { PageKey = "activity_log", PageName = "Activity Log", Path = "/log", GroupName = "Traceability" },
        new() { PageKey = "production_activity", PageName = "Production Activity", Path = "/production-history", GroupName = "Production Activity" },
        new() { PageKey = "production_history", PageName = "Production History", Path = "/cutting-lists", GroupName = "Production Activity" },
        new() { PageKey = "setting", PageName = "Setting", Path = "/production-setting", GroupName = "System" }
    ];

    public UsersController(ProductionControlDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] AppUserStatus? status,
        [FromQuery] int? roleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.Users.AsNoTracking()
            .Include(x => x.RoleMaster)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();
            query = query.Where(x =>
                x.Username.Contains(keyword) ||
                x.FullName.Contains(keyword) ||
                (x.Email != null && x.Email.Contains(keyword)) ||
                (x.Phone != null && x.Phone.Contains(keyword)));
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (roleId.HasValue)
        {
            query = query.Where(x => x.RoleId == roleId.Value);
        }

        var rows = await query
            .OrderBy(x => x.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToResponse(x))
            .ToListAsync();

        return ApiOk(rows);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles([FromQuery] bool? isActive = null)
    {
        var query = _db.UserRoles.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var roles = await query
            .OrderBy(x => x.RoleName)
            .ToListAsync();

        return ApiOk(roles);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] SaveRoleRequest request)
    {
        try
        {
            var roleCode = NormalizeRoleCode(request.RoleCode);
            var roleName = NormalizeRequired(request.RoleName, "Role name is required.");

            if (await _db.UserRoles.AnyAsync(x => x.RoleCode == roleCode))
            {
                throw new InvalidOperationException("Role code is already used.");
            }

            var role = new AppRole
            {
                RoleCode = roleCode,
                RoleName = roleName,
                Description = NormalizeText(request.Description),
                IsActive = request.IsActive,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _db.UserRoles.Add(role);
            await _db.SaveChangesAsync();

            return ApiCreated(role, "Role created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("roles/{roleId:int}/page-access")]
    public async Task<IActionResult> GetRolePageAccess(int roleId)
    {
        var role = await _db.UserRoles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roleId);
        if (role is null)
        {
            return ApiNotFound("Role was not found.");
        }

        var allowed = await _db.RolePageAccesses.AsNoTracking()
            .Where(x => x.RoleId == roleId && x.CanAccess)
            .Select(x => x.PageKey)
            .ToListAsync();
        var allowedSet = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var isAdmin = IsAdminRole(role.RoleCode);

        var rows = PageDefinitions.Select(page => new RolePageAccessResponse
        {
            PageKey = page.PageKey,
            PageName = page.PageName,
            Path = page.Path,
            GroupName = page.GroupName,
            CanAccess = isAdmin || allowedSet.Contains(page.PageKey)
        }).ToList();

        return ApiOk(rows);
    }

    [HttpPut("roles/{roleId:int}/page-access")]
    public async Task<IActionResult> SaveRolePageAccess(int roleId, [FromBody] SaveRolePageAccessRequest request)
    {
        try
        {
            var role = await _db.UserRoles.FirstOrDefaultAsync(x => x.Id == roleId);
            if (role is null)
            {
                return ApiNotFound("Role was not found.");
            }

            if (IsAdminRole(role.RoleCode))
            {
                throw new InvalidOperationException("Admin role always has access to all pages.");
            }

            var validPageKeys = PageDefinitions.Select(x => x.PageKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var selectedPageKeys = request.PageKeys
                .Where(x => validPageKeys.Contains(x))
                .Select(x => PageDefinitions.First(page => string.Equals(page.PageKey, x, StringComparison.OrdinalIgnoreCase)).PageKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existing = await _db.RolePageAccesses
                .Where(x => x.RoleId == roleId)
                .ToListAsync();

            foreach (var page in PageDefinitions)
            {
                var row = existing.FirstOrDefault(x => string.Equals(x.PageKey, page.PageKey, StringComparison.OrdinalIgnoreCase));
                if (row is null)
                {
                    row = new RolePageAccess
                    {
                        RoleId = roleId,
                        PageKey = page.PageKey,
                        CreatedAt = DateTime.Now
                    };
                    _db.RolePageAccesses.Add(row);
                }

                row.CanAccess = selectedPageKeys.Contains(page.PageKey);
                row.UpdatedAt = DateTime.Now;
            }

            await _db.SaveChangesAsync();
            return ApiOk(await BuildRolePageAccess(roleId, false), "Role page access updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("me/page-access")]
    public async Task<IActionResult> GetCurrentUserPageAccess()
    {
        var userIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdText, out var userId))
        {
            return ApiUnauthorized("Invalid user session.");
        }

        var user = await _db.Users.AsNoTracking()
            .Include(x => x.RoleMaster)
            .FirstOrDefaultAsync(x => x.Id == userId);
        if (user is null)
        {
            return ApiUnauthorized("Invalid user session.");
        }

        if (IsAdminRole(ResolveRoleCode(user)))
        {
            return ApiOk(PageDefinitions.Select(x => x.PageKey).ToList());
        }

        var pageKeys = await _db.RolePageAccesses.AsNoTracking()
            .Where(x => x.RoleId == user.RoleId && x.CanAccess)
            .Select(x => x.PageKey)
            .ToListAsync();

        return ApiOk(pageKeys);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var username = NormalizeRequired(request.Username, "Username is required.");
            var fullName = NormalizeRequired(request.FullName, "Full name is required.");
            var email = NormalizeText(request.Email);
            var phone = NormalizeText(request.Phone);
            var role = await GetActiveRole(request.RoleId);

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                throw new ArgumentException("Password minimal 6 karakter.");
            }

            if (await _db.Users.AnyAsync(x => x.Username == username))
            {
                throw new InvalidOperationException("Username is already used.");
            }

            if (email is not null && await _db.Users.AnyAsync(x => x.Email == email))
            {
                throw new InvalidOperationException("Email is already used.");
            }

            var salt = AuthPasswordHasher.CreateSalt();
            var user = new AppUser
            {
                Username = username,
                FullName = fullName,
                Email = email,
                Phone = phone,
                RoleId = role.Id,
                Role = role.RoleCode,
                Status = request.Status,
                PasswordSalt = salt,
                PasswordHash = AuthPasswordHasher.HashPassword(request.Password, salt),
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            await _db.Entry(user).Reference(x => x.RoleMaster).LoadAsync();

            return ApiCreated(ToResponse(user), "User created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
            if (user is null)
            {
                return ApiNotFound("User was not found.");
            }

            var username = NormalizeRequired(request.Username, "Username is required.");
            var fullName = NormalizeRequired(request.FullName, "Full name is required.");
            var email = NormalizeText(request.Email);
            var phone = NormalizeText(request.Phone);
            var role = await GetActiveRole(request.RoleId);

            if (await _db.Users.AnyAsync(x => x.Id != id && x.Username == username))
            {
                throw new InvalidOperationException("Username is already used.");
            }

            if (email is not null && await _db.Users.AnyAsync(x => x.Id != id && x.Email == email))
            {
                throw new InvalidOperationException("Email is already used.");
            }

            user.Username = username;
            user.FullName = fullName;
            user.Email = email;
            user.Phone = phone;
            user.RoleId = role.Id;
            user.Role = role.RoleCode;
            user.Status = request.Status;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                if (request.Password.Length < 6)
                {
                    throw new ArgumentException("Password minimal 6 karakter.");
                }

                user.PasswordSalt = AuthPasswordHasher.CreateSalt();
                user.PasswordHash = AuthPasswordHasher.HashPassword(request.Password, user.PasswordSalt);
            }

            user.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            await _db.Entry(user).Reference(x => x.RoleMaster).LoadAsync();

            return ApiOk(ToResponse(user), "User updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    private static string? NormalizeText(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private async Task<AppRole> GetActiveRole(int roleId)
    {
        var role = await _db.UserRoles.FirstOrDefaultAsync(x => x.Id == roleId && x.IsActive);
        return role ?? throw new ArgumentException("Role is required or inactive.");
    }

    private static string NormalizeRequired(string? value, string message) =>
        NormalizeText(value) ?? throw new ArgumentException(message);

    private static string NormalizeRoleCode(string? value)
    {
        var normalized = NormalizeRequired(value, "Role code is required.").ToUpperInvariant().Replace(" ", "_");
        if (normalized.Any(x => !char.IsLetterOrDigit(x) && x != '_'))
        {
            throw new ArgumentException("Role code hanya boleh huruf, angka, dan underscore.");
        }

        return normalized.Length <= 50 ? normalized : normalized[..50];
    }

    private static UserResponse ToResponse(AppUser user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            RoleId = user.RoleId,
            Role = ResolveRoleCode(user),
            RoleName = user.RoleMaster?.RoleName ?? ResolveRoleCode(user),
            Status = user.Status,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static string ResolveRoleCode(AppUser user) =>
        string.IsNullOrWhiteSpace(user.RoleMaster?.RoleCode)
            ? user.Role
            : user.RoleMaster.RoleCode;

    private async Task<List<RolePageAccessResponse>> BuildRolePageAccess(int roleId, bool isAdmin)
    {
        var allowed = await _db.RolePageAccesses.AsNoTracking()
            .Where(x => x.RoleId == roleId && x.CanAccess)
            .Select(x => x.PageKey)
            .ToListAsync();
        var allowedSet = allowed.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return PageDefinitions.Select(page => new RolePageAccessResponse
        {
            PageKey = page.PageKey,
            PageName = page.PageName,
            Path = page.Path,
            GroupName = page.GroupName,
            CanAccess = isAdmin || allowedSet.Contains(page.PageKey)
        }).ToList();
    }

    private static bool IsAdminRole(string? roleCode) =>
        string.Equals(roleCode, AppUserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
}
