using Employee_Attendance_Salary_Management.Models;
using Npgsql;
using NpgsqlTypes;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class UserAccountService
{
    public async Task<IReadOnlyList<UserAccountListItem>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT ua.id, ua.user_name, ua.display_name, ua.email, ua.phone_number,
                   ua.has_all_branch_access, ua.is_active, ua.last_login_at,
                   COALESCE(array_agg(r.code ORDER BY r.code) FILTER (WHERE r.code IS NOT NULL), ARRAY[]::varchar[]) roles
            FROM auth.user_account ua
            LEFT JOIN auth.user_role ur ON ur.user_id = ua.id
            LEFT JOIN auth.role r ON r.id = ur.role_id
            GROUP BY ua.id
            ORDER BY ua.is_active DESC, lower(ua.display_name), lower(ua.user_name)
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var users = new List<UserAccountListItem>();

        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new UserAccountListItem
            {
                Id = reader.GetGuid(0),
                UserName = reader.GetString(1),
                DisplayName = reader.GetString(2),
                Email = reader.IsDBNull(3) ? null : reader.GetString(3),
                PhoneNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                HasAllBranchAccess = reader.GetBoolean(5),
                IsActive = reader.GetBoolean(6),
                LastLoginAt = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                Roles = reader.GetFieldValue<string[]>(8)
            });
        }

        return users;
    }

    public async Task<IReadOnlyList<UserRoleOption>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT code, name FROM auth.role WHERE code IN ('ADMIN', 'HR_MANAGER') ORDER BY name", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var roles = new List<UserRoleOption>();
        while (await reader.ReadAsync(cancellationToken))
            roles.Add(new UserRoleOption(reader.GetString(0), reader.GetString(1)));
        return roles;
    }

    public async Task CreateAsync(UserAccountFormModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model.Password))
            throw new InvalidOperationException("A password is required for a new user.");

        var userId = Guid.NewGuid();
        var passwordHash = AuthenticationService.HashPassword(userId, model.UserName.Trim(), model.Password);
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand("""
            INSERT INTO auth.user_account
                (id, user_name, email, password_hash, phone_number, display_name,
                 has_all_branch_access, is_active, created_by, updated_by)
            VALUES
                (@id, @user_name, @email, @password_hash, @phone_number, @display_name,
                 @all_branches, @is_active, @actor_id, @actor_id)
            """, connection, transaction))
        {
            AddUserParameters(command, userId, model, actorUserId);
            command.Parameters.AddWithValue("password_hash", passwordHash);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await ReplaceRoleAsync(connection, transaction, userId, model.RoleCode, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserAccountFormModel model, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (model.Id is null) throw new InvalidOperationException("User was not selected.");
        if (model.Id == actorUserId && (!model.IsActive || !model.RoleCode.Equals("ADMIN", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("You cannot deactivate your own account or remove your administrator role.");

        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var command = new NpgsqlCommand("""
            UPDATE auth.user_account
            SET user_name = @user_name,
                email = @email,
                phone_number = @phone_number,
                display_name = @display_name,
                has_all_branch_access = @all_branches,
                is_active = @is_active,
                updated_at = now(),
                updated_by = @actor_id
            WHERE id = @id
            """, connection, transaction))
        {
            AddUserParameters(command, model.Id.Value, model, actorUserId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                throw new InvalidOperationException("User account no longer exists.");
        }

        await ReplaceRoleAsync(connection, transaction, model.Id.Value, model.RoleCode, actorUserId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SetActiveAsync(Guid userId, bool isActive, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (userId == actorUserId && !isActive)
            throw new InvalidOperationException("You cannot deactivate your own account.");

        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE auth.user_account
            SET is_active = @is_active, updated_at = now(), updated_by = @actor_id
            WHERE id = @id
            """, connection);
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("is_active", isActive);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, string userName, string newPassword, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var passwordHash = AuthenticationService.HashPassword(userId, userName, newPassword);
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE auth.user_account
            SET password_hash = @password_hash,
                access_failed_count = 0,
                updated_at = now(),
                updated_by = @actor_id
            WHERE id = @id
            """, connection);
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("password_hash", passwordHash);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddUserParameters(NpgsqlCommand command, Guid userId, UserAccountFormModel model, Guid actorUserId)
    {
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("user_name", model.UserName.Trim());
        command.Parameters.Add("email", NpgsqlDbType.Varchar).Value = DbValue(model.Email);
        command.Parameters.Add("phone_number", NpgsqlDbType.Text).Value = DbValue(model.PhoneNumber);
        command.Parameters.AddWithValue("display_name", model.DisplayName.Trim());
        command.Parameters.AddWithValue("all_branches", model.HasAllBranchAccess);
        command.Parameters.AddWithValue("is_active", model.IsActive);
        command.Parameters.AddWithValue("actor_id", actorUserId);
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static async Task ReplaceRoleAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid userId, string roleCode,
        Guid actorUserId, CancellationToken cancellationToken)
    {
        await using (var delete = new NpgsqlCommand("""
            DELETE FROM auth.user_role ur
            USING auth.role r
            WHERE ur.role_id = r.id
              AND ur.user_id = @user_id
              AND r.code IN ('ADMIN', 'HR_MANAGER')
            """, connection, transaction))
        {
            delete.Parameters.AddWithValue("user_id", userId);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var insert = new NpgsqlCommand("""
            INSERT INTO auth.user_role (user_id, role_id, created_by, updated_by)
            SELECT @user_id, id, @actor_id, @actor_id
            FROM auth.role
            WHERE code = @role_code
            """, connection, transaction);
        insert.Parameters.AddWithValue("user_id", userId);
        insert.Parameters.AddWithValue("role_code", roleCode);
        insert.Parameters.AddWithValue("actor_id", actorUserId);
        if (await insert.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("The selected role does not exist.");
    }
}
