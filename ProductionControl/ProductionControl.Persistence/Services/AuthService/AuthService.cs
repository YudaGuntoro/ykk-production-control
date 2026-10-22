using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ProductionControl.Domain.Auth;
using ProductionControl.Persistence.Context;
using ProductionControl.Persistence.Services.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ProductionControl.Persistence.Services.AuthService;

public class AuthService : IAuthService
{
    private readonly ProductionControlDbContext _db;
    private readonly JwtSettings _jwtSettings;

    public AuthService(ProductionControlDbContext db, IOptions<JwtSettings> jwtOptions)
    {
        _db = db;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        var username = request.Username.Trim();
        var user = await _db.Users
            .Include(x => x.RoleMaster)
            .FirstOrDefaultAsync(x => x.Username == username);
        if (user == null || !AuthPasswordHasher.VerifyPassword(request.Password, user.PasswordSalt, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (user.Status != AppUserStatus.ACTIVE)
        {
            throw new UnauthorizedAccessException("User is inactive.");
        }

        var loginAt = DateTime.Now;
        var expiresAt = loginAt.AddHours(_jwtSettings.ExpiresHours);
        user.LastLoginAt = loginAt;
        user.UpdatedAt = loginAt;
        await _db.SaveChangesAsync();

        return new LoginResponse
        {
            AccessToken = CreateJwt(user, loginAt, expiresAt),
            RefreshToken = CreateRefreshJwt(user, loginAt),
            TokenType = "Bearer",
            ExpiresAt = expiresAt,
            User = ToResponse(user)
        };
    }

    public async Task<LoginResponse> RefreshAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out var validatedToken);
            if (validatedToken is not JwtSecurityToken jwt ||
                jwt.Claims.FirstOrDefault(x => x.Type == "token_use")?.Value != "refresh" ||
                !int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            var user = await _db.Users.Include(x => x.RoleMaster).FirstOrDefaultAsync(x => x.Id == userId);
            if (user is null || user.Status != AppUserStatus.ACTIVE)
            {
                throw new UnauthorizedAccessException("User is inactive or no longer exists.");
            }

            var now = DateTime.Now;
            var expiresAt = now.AddHours(_jwtSettings.ExpiresHours);
            return new LoginResponse
            {
                AccessToken = CreateJwt(user, now, expiresAt),
                RefreshToken = CreateRefreshJwt(user, now),
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = ToResponse(user)
            };
        }
        catch (SecurityTokenException)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
        }
    }

    private string CreateRefreshJwt(AppUser user, DateTime issuedAt)
    {
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("token_use", "refresh")
            },
            notBefore: issuedAt,
            expires: issuedAt.AddDays(30),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string CreateJwt(AppUser user, DateTime issuedAt, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, ResolveRoleCode(user)),
            new("username", user.Username),
            new("full_name", user.FullName),
            new("role", ResolveRoleCode(user))
        };

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
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
}
