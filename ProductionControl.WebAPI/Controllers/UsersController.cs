using ProductionControl.Domain.Auth;
using ProductionControl.Persistence.Context;
using ProductionControl.Persistence.Services.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProductionControl.WebAPI.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ApiControllerBase
{
    private readonly ProductionControlDbContext _db;

    public UsersController(ProductionControlDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] AppUserStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _db.Users.AsNoTracking().AsQueryable();

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

        var rows = await query
            .OrderBy(x => x.Username)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToResponse(x))
            .ToListAsync();

        return ApiOk(rows);
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

            var username = request.Username.Trim();
            var fullName = request.FullName.Trim();
            var email = NormalizeText(request.Email);
            var phone = NormalizeText(request.Phone);

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException("Username is required.");
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("Full name is required.");
            }

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
            user.Role = request.Role;
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

    private static UserResponse ToResponse(AppUser user)
    {
        return new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            Phone = user.Phone,
            Role = user.Role,
            Status = user.Status,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }
}
