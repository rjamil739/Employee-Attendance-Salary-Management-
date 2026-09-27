using Employee_Attendance_Salary_Management.Models;
using Npgsql;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class CompanyProfileService
{
    public async Task<CompanyProfile?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(
            "SELECT company_name, logo FROM core.company_profile WHERE id = 1 LIMIT 1", connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CompanyProfile
        {
            CompanyName = reader.GetString(0),
            Logo = reader.IsDBNull(1) ? null : reader.GetFieldValue<byte[]>(1)
        };
    }

    public async Task<CompanyProfile> CreateAsync(
        string companyName,
        byte[]? logo,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            INSERT INTO core.company_profile (id, company_name, logo)
            VALUES (1, @company_name, @logo)
            RETURNING company_name, logo
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("company_name", companyName.Trim());
        command.Parameters.AddWithValue("logo", logo is null ? DBNull.Value : logo);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new CompanyProfile
        {
            CompanyName = reader.GetString(0),
            Logo = reader.IsDBNull(1) ? null : reader.GetFieldValue<byte[]>(1)
        };
    }
}
