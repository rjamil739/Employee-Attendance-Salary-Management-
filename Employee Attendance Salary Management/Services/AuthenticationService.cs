using System.Security.Claims;
using Employee_Attendance_Salary_Management.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Npgsql;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class AuthenticationService
{
    private readonly PasswordHasher<PasswordIdentity> passwordHasher = new();

    public async Task<AuthenticatedUser?> ValidateAsync(
        string userName,
        string password,
        string requiredRole,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            return null;

        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                ua.id,
                ua.user_name,
                ua.display_name,
                ua.password_hash,
                ua.has_all_branch_access,
                COALESCE(array_agg(r.code) FILTER (WHERE r.code IS NOT NULL), ARRAY[]::varchar[]) AS roles
            FROM auth.user_account ua
            LEFT JOIN auth.user_role ur ON ur.user_id = ua.id
            LEFT JOIN auth.role r ON r.id = ur.role_id
            WHERE lower(ua.user_name) = lower(@user_name)
              AND ua.is_active = true
            GROUP BY ua.id
            LIMIT 1
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_name", userName.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var id = reader.GetGuid(0);
        var storedUserName = reader.GetString(1);
        var displayName = reader.GetString(2);
        var passwordHash = reader.IsDBNull(3) ? null : reader.GetString(3);
        var hasAllBranchAccess = reader.GetBoolean(4);
        var roles = reader.GetFieldValue<string[]>(5);
        await reader.CloseAsync();

        if (passwordHash is null || !roles.Contains(requiredRole, StringComparer.OrdinalIgnoreCase))
        {
            await RecordFailedAttemptAsync(connection, id, cancellationToken);
            return null;
        }

        var identity = new PasswordIdentity(id, storedUserName);
        var verification = passwordHasher.VerifyHashedPassword(identity, passwordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            await RecordFailedAttemptAsync(connection, id, cancellationToken);
            return null;
        }

        var newHash = verification == PasswordVerificationResult.SuccessRehashNeeded
            ? passwordHasher.HashPassword(identity, password)
            : null;

        await using var successCommand = new NpgsqlCommand("""
            UPDATE auth.user_account
            SET last_login_at = now(),
                access_failed_count = 0,
                password_hash = COALESCE(@new_hash, password_hash),
                updated_at = now()
            WHERE id = @id
            """, connection);
        successCommand.Parameters.AddWithValue("id", id);
        successCommand.Parameters.AddWithValue("new_hash", newHash is null ? DBNull.Value : newHash);
        await successCommand.ExecuteNonQueryAsync(cancellationToken);

        return new AuthenticatedUser(id, storedUserName, displayName, hasAllBranchAccess, roles);
    }

    public ClaimsPrincipal CreatePrincipal(AuthenticatedUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName),
            new("all_branch_access", user.HasAllBranchAccess.ToString())
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role));
    }

    public static string HashPassword(Guid userId, string userName, string password)
    {
        var identity = new PasswordIdentity(userId, userName);
        return new PasswordHasher<PasswordIdentity>().HashPassword(identity, password);
    }

    private static async Task RecordFailedAttemptAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE auth.user_account
            SET access_failed_count = access_failed_count + 1,
                updated_at = now()
            WHERE id = @id
            """, connection);
        command.Parameters.AddWithValue("id", userId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record PasswordIdentity(Guid Id, string UserName);
}
