using Employee_Attendance_Salary_Management.Models;
using Npgsql;

try
{
    var connectionSettings = new NpgsqlConnectionStringBuilder(StaticConnection.conn);
    Console.WriteLine($"Configured database: [{connectionSettings.Database}]");
    Console.WriteLine($"Configured SSL mode: {connectionSettings.SslMode}");
    await using var connection = new NpgsqlConnection(connectionSettings.ConnectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand("""
        SELECT current_database(), current_user,
               to_regclass('core.company_profile')::text,
               (SELECT count(*) FROM core.company_profile),
               (SELECT company_name FROM core.company_profile ORDER BY id LIMIT 1),
               (SELECT count(*) FROM auth.user_account),
               (SELECT count(*) FROM auth.user_account WHERE is_active),
               (SELECT count(*) FROM auth.user_role),
               (SELECT string_agg(ua.user_name || '=' || COALESCE(r.roles, 'NO_ROLE'), ', ' ORDER BY ua.user_name)
                FROM auth.user_account ua
                LEFT JOIN LATERAL (
                    SELECT string_agg(ro.code, '+') roles
                    FROM auth.user_role ur JOIN auth.role ro ON ro.id = ur.role_id
                    WHERE ur.user_id = ua.id
                ) r ON true)
        """, connection);
    await using var reader = await command.ExecuteReaderAsync();
    await reader.ReadAsync();
    Console.WriteLine($"Database: [{reader.GetString(0)}]");
    Console.WriteLine($"User: {reader.GetString(1)}");
    Console.WriteLine($"Table: {(reader.IsDBNull(2) ? "missing" : reader.GetString(2))}");
    Console.WriteLine($"Rows: {reader.GetInt64(3)}");
    Console.WriteLine($"Company: {(reader.IsDBNull(4) ? "empty" : reader.GetString(4))}");
    Console.WriteLine($"Users: {reader.GetInt64(5)}; Active: {reader.GetInt64(6)}; Role assignments: {reader.GetInt64(7)}");
    Console.WriteLine($"Accounts: {(reader.IsDBNull(8) ? "none" : reader.GetString(8))}");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    if (exception is PostgresException postgres)
        Console.Error.WriteLine($"SQLSTATE: {postgres.SqlState}; Detail: {postgres.Detail}");
    Environment.ExitCode = 1;
}
