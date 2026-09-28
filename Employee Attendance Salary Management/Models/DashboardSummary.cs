namespace Employee_Attendance_Salary_Management.Models;

public sealed class DashboardSummary
{
    public int Branches { get; init; }
    public int Employees { get; init; }
    public int PresentToday { get; init; }
    public int PendingLeaves { get; init; }
    public int PendingAttendanceRequests { get; init; }
    public int ActivePayrollRuns { get; init; }
    public int ActiveLoans { get; init; }
    public int ActiveDevices { get; init; }
    public int PendingApprovals { get; init; }
    public int ActiveUsers { get; init; }
    public decimal PaidThisMonth { get; init; }
    public string BranchLabel { get; init; } = "All branches";
}
