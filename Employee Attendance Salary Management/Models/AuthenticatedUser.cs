namespace Employee_Attendance_Salary_Management.Models;

public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    string DisplayName,
    bool HasAllBranchAccess,
    IReadOnlyList<string> Roles);

public enum AuthenticationStatus
{
    Success,
    InvalidCredentials,
    AccessTerminated
}

public sealed record AuthenticationAttempt(AuthenticationStatus Status, AuthenticatedUser? User = null);
