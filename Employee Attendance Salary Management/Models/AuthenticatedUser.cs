namespace Employee_Attendance_Salary_Management.Models;

public sealed record AuthenticatedUser(
    Guid Id,
    string UserName,
    string DisplayName,
    bool HasAllBranchAccess,
    IReadOnlyList<string> Roles);
