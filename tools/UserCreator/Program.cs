using Employee_Attendance_Salary_Management.Models;
using Microsoft.AspNetCore.Identity;
using Npgsql;

var arguments = ParseArguments(args);
if (!arguments.TryGetValue("username", out var userName)
    || !arguments.TryGetValue("display-name", out var displayName)
    || !arguments.TryGetValue("role", out var roleInput))
{
    PrintUsage();
    return 1;
}

var safeUserName = userName!;
var safeDisplayName = displayName!;
var safeRoleInput = roleInput!;

var role = safeRoleInput.Trim().ToUpperInvariant() switch
{
    "ADMIN" => "ADMIN",
    "HR" or "HR_MANAGER" => "HR_MANAGER",
    _ => string.Empty
};

if (role.Length == 0)
{
    Console.Error.WriteLine("Role must be ADMIN or HR_MANAGER.");
    return 1;
}

var password = arguments.GetValueOrDefault("password") ?? ReadPassword();
if (password.Length < 8)
{
    Console.Error.WriteLine("Password must contain at least 8 characters.");
    return 1;
}

var email = arguments.GetValueOrDefault("email");
var branchCode = arguments.GetValueOrDefault("branch-code");
var hasAllBranchAccess = role == "ADMIN" || arguments.ContainsKey("all-branches");
var userId = Guid.NewGuid();
var passwordIdentity = new PasswordIdentity(userId, safeUserName.Trim());
var passwordHash = new PasswordHasher<PasswordIdentity>().HashPassword(passwordIdentity, password);

await using var connection = new NpgsqlConnection(StaticConnection.conn);
await connection.OpenAsync();
await using var transaction = await connection.BeginTransactionAsync();

try
{
    await using (var duplicateCommand = new NpgsqlCommand(
        "SELECT EXISTS (SELECT 1 FROM auth.user_account WHERE lower(user_name) = lower(@user_name))",
        connection,
        transaction))
    {
        duplicateCommand.Parameters.AddWithValue("user_name", safeUserName.Trim());
        if ((bool)(await duplicateCommand.ExecuteScalarAsync())!)
            throw new InvalidOperationException($"User '{safeUserName}' already exists.");
    }

    const string insertUserSql = """
        INSERT INTO auth.user_account
            (id, user_name, email, password_hash, display_name, has_all_branch_access, is_active)
        VALUES
            (@id, @user_name, @email, @password_hash, @display_name, @all_branches, true)
        """;
    await using (var userCommand = new NpgsqlCommand(insertUserSql, connection, transaction))
    {
        userCommand.Parameters.AddWithValue("id", userId);
        userCommand.Parameters.AddWithValue("user_name", safeUserName.Trim());
        userCommand.Parameters.AddWithValue("email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email.Trim());
        userCommand.Parameters.AddWithValue("password_hash", passwordHash);
        userCommand.Parameters.AddWithValue("display_name", safeDisplayName.Trim());
        userCommand.Parameters.AddWithValue("all_branches", hasAllBranchAccess);
        await userCommand.ExecuteNonQueryAsync();
    }

    await using (var roleCommand = new NpgsqlCommand("""
        INSERT INTO auth.user_role (user_id, role_id)
        SELECT @user_id, id FROM auth.role WHERE code = @role
        """, connection, transaction))
    {
        roleCommand.Parameters.AddWithValue("user_id", userId);
        roleCommand.Parameters.AddWithValue("role", role);
        if (await roleCommand.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException($"Database role '{role}' was not found. Run the database setup script first.");
    }

    if (!string.IsNullOrWhiteSpace(branchCode))
    {
        await using var branchCommand = new NpgsqlCommand("""
            INSERT INTO auth.user_branch (user_id, branch_id, is_default)
            SELECT @user_id, id, true
            FROM core.branch
            WHERE upper(code) = upper(@branch_code)
            """, connection, transaction);
        branchCommand.Parameters.AddWithValue("user_id", userId);
        branchCommand.Parameters.AddWithValue("branch_code", branchCode.Trim());
        if (await branchCommand.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException($"Branch code '{branchCode}' was not found.");
    }

    await transaction.CommitAsync();
    Console.WriteLine($"User created successfully: {safeUserName.Trim()} ({role})");
    Console.WriteLine(hasAllBranchAccess ? "Branch access: all branches" : $"Branch access: {branchCode ?? "no branch assigned"}");
    return 0;
}
catch (Exception exception)
{
    await transaction.RollbackAsync();
    Console.Error.WriteLine($"User was not created: {exception.Message}");
    return 1;
}

static Dictionary<string, string?> ParseArguments(string[] values)
{
    var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    for (var index = 0; index < values.Length; index++)
    {
        var value = values[index];
        if (!value.StartsWith("--")) continue;

        var key = value[2..];
        if (key == "all-branches")
        {
            result[key] = "true";
            continue;
        }

        result[key] = index + 1 < values.Length && !values[index + 1].StartsWith("--")
            ? values[++index]
            : null;
    }
    return result;
}

static string ReadPassword()
{
    Console.Write("Password: ");
    var password = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace && password.Count > 0)
        {
            password.RemoveAt(password.Count - 1);
            continue;
        }
        if (!char.IsControl(key.KeyChar)) password.Add(key.KeyChar);
    }
    Console.WriteLine();
    return new string(password.ToArray());
}

static void PrintUsage()
{
    Console.WriteLine("Create an application login user");
    Console.WriteLine();
    Console.WriteLine("Required:");
    Console.WriteLine("  --username <name> --display-name <name> --role <ADMIN|HR_MANAGER>");
    Console.WriteLine();
    Console.WriteLine("Optional:");
    Console.WriteLine("  --email <address> --branch-code <code> --all-branches --password <password>");
    Console.WriteLine();
    Console.WriteLine("For security, omit --password and enter it at the hidden prompt.");
}

file sealed record PasswordIdentity(Guid Id, string UserName);
