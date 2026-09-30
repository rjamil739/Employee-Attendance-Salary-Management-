using Employee_Attendance_Salary_Management.Models;
using Npgsql;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class CompanyProfileService
{
    private readonly SemaphoreSlim cacheLock = new(1, 1);
    private CompanyProfile? cachedProfile;
    private bool cacheLoaded;

    public async Task<CompanyProfile?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (cacheLoaded) return cachedProfile;
        await cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (cacheLoaded) return cachedProfile;
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand("""
            SELECT company_name, logo
            FROM core.company_profile
            WHERE NULLIF(BTRIM(company_name), '') IS NOT NULL
            ORDER BY id
            LIMIT 1
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            cacheLoaded = true;
            return null;
        }

        cachedProfile = new CompanyProfile
        {
            CompanyName = reader.GetString(0),
            Logo = reader.IsDBNull(1) ? null : reader.GetFieldValue<byte[]>(1)
        };
        cacheLoaded = true;
        return cachedProfile;
        }
        finally
        {
            cacheLock.Release();
        }
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
        command.Parameters.Add("logo", NpgsqlTypes.NpgsqlDbType.Bytea).Value = logo is null ? DBNull.Value : logo;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        cachedProfile = new CompanyProfile
        {
            CompanyName = reader.GetString(0),
            Logo = reader.IsDBNull(1) ? null : reader.GetFieldValue<byte[]>(1)
        };
        cacheLoaded = true;
        return cachedProfile;
    }
}
