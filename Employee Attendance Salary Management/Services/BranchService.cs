using Employee_Attendance_Salary_Management.Models;
using Npgsql;
using NpgsqlTypes;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class BranchService
{
    public async Task<IReadOnlyList<BranchListItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT b.id, b.code, b.name, b.logo, b.is_head_office, b.is_active,
                   b.created_at, b.updated_at, creator.display_name, updater.display_name
            FROM core.branch b
            LEFT JOIN auth.user_account creator ON creator.id = b.created_by
            LEFT JOIN auth.user_account updater ON updater.id = b.updated_by
            ORDER BY b.is_head_office DESC, b.is_active DESC, lower(b.name)
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var branches = new List<BranchListItem>();

        while (await reader.ReadAsync(cancellationToken))
        {
            branches.Add(new BranchListItem
            {
                Id = reader.GetGuid(0),
                Code = reader.GetString(1),
                Name = reader.GetString(2),
                Logo = reader.IsDBNull(3) ? null : reader.GetFieldValue<byte[]>(3),
                IsHeadOffice = reader.GetBoolean(4),
                IsActive = reader.GetBoolean(5),
                CreatedAt = reader.GetFieldValue<DateTimeOffset>(6),
                UpdatedAt = reader.GetFieldValue<DateTimeOffset>(7),
                CreatedByName = reader.IsDBNull(8) ? null : reader.GetString(8),
                UpdatedByName = reader.IsDBNull(9) ? null : reader.GetString(9)
            });
        }

        return branches;
    }

    public async Task CreateAsync(BranchFormModel model, byte[]? logo, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO core.branch
                (code, name, logo, is_head_office, is_active, created_by, updated_by)
            VALUES
                (@code, @name, @logo, @is_head_office, @is_active, @actor_id, @actor_id)
            """, connection);
        AddParameters(command, model, logo, actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(BranchFormModel model, byte[]? logo, bool updateLogo, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        if (model.Id is null) throw new InvalidOperationException("Branch was not selected.");
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE core.branch
            SET code = @code,
                name = @name,
                logo = CASE WHEN @update_logo THEN @logo ELSE logo END,
                is_head_office = @is_head_office,
                is_active = @is_active,
                updated_at = now(),
                updated_by = @actor_id
            WHERE id = @id
            """, connection);
        AddParameters(command, model, logo, actorUserId);
        command.Parameters.AddWithValue("id", model.Id.Value);
        command.Parameters.AddWithValue("update_logo", updateLogo);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new InvalidOperationException("Branch no longer exists.");
    }

    public async Task SetActiveAsync(Guid branchId, bool isActive, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE core.branch
            SET is_active = @is_active, updated_at = now(), updated_by = @actor_id
            WHERE id = @id
            """, connection);
        command.Parameters.AddWithValue("id", branchId);
        command.Parameters.AddWithValue("is_active", isActive);
        command.Parameters.AddWithValue("actor_id", actorUserId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(NpgsqlCommand command, BranchFormModel model, byte[]? logo, Guid actorUserId)
    {
        command.Parameters.AddWithValue("code", model.Id is null ? BusinessCode.New("BR") : model.Code.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("name", model.Name.Trim());
        command.Parameters.Add("logo", NpgsqlDbType.Bytea).Value = logo is null ? DBNull.Value : logo;
        command.Parameters.AddWithValue("is_head_office", model.IsHeadOffice);
        command.Parameters.AddWithValue("is_active", model.IsActive);
        command.Parameters.AddWithValue("actor_id", actorUserId);
    }
}
