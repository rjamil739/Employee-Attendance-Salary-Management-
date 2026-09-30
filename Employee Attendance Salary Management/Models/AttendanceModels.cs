using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed record HolidayCalendarItem(Guid Id, Guid? BranchId, string? BranchName, string Code, string Name, bool IsActive);
public sealed record HolidayItem(Guid Id, Guid CalendarId, string CalendarName, DateOnly HolidayDate, string Name, bool IsPaid);
public sealed record ShiftItem(Guid Id, string Code, string Name, TimeOnly StartTime, TimeOnly EndTime, int BreakMinutes, bool CrossesMidnight, int ScheduledMinutes, bool IsFlexible, bool IsActive);
public sealed record AttendancePolicyItem(Guid Id, string Code, string Name, int LateGraceMinutes, int EarlyOutGraceMinutes, int? HalfDayBelowMinutes, int? AbsentBelowMinutes, int OvertimeAfterMinutes, int OvertimeRoundingMinutes, decimal OvertimeMultiplier, decimal HolidayOvertimeMultiplier, short[] WeeklyOffDays, bool RequireOvertimeApproval, bool IsActive);
public sealed record EmployeeScheduleItem(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeNo, Guid ShiftId, string ShiftName, Guid PolicyId, string PolicyName, Guid? HolidayCalendarId, string? CalendarName, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record AttendanceDayItem(Guid Id, Guid EmployeeId, string EmployeeName, string EmployeeNo, string BranchName, string DepartmentName, DateOnly WorkDate, DateTimeOffset? FirstInAt, DateTimeOffset? LastOutAt, int ScheduledMinutes, int WorkedMinutes, int LateMinutes, int EarlyOutMinutes, int OvertimeMinutes, int BreakMinutes, string Status, string EntrySource, string? Notes);
public sealed record AttendanceRequestItem(Guid Id, string Kind, Guid EmployeeId, string EmployeeName, string EmployeeNo, string BranchName, DateOnly WorkDate, DateTimeOffset? RequestedFirstInAt, DateTimeOffset? RequestedLastOutAt, int? RequestedMinutes, int? ApprovedMinutes, decimal? Multiplier, string? Reason, string Status, string RequestedBy, DateTimeOffset CreatedAt);

public sealed class HolidayCalendarForm { public Guid? Id { get; set; } public Guid? BranchId { get; set; } public string Code { get; set; }=""; [Required] public string Name { get; set; }=""; public bool IsActive { get; set; }=true; }
public sealed class HolidayForm { public Guid? Id { get; set; } [Required] public Guid? CalendarId { get; set; } public DateOnly HolidayDate { get; set; }=DateOnly.FromDateTime(DateTime.Today); [Required] public string Name { get; set; }=""; public bool IsPaid { get; set; }=true; }
public sealed class ShiftForm { public Guid? Id { get; set; } public string Code { get; set; }=""; [Required] public string Name { get; set; }=""; public TimeOnly StartTime { get; set; }=new(9,0); public TimeOnly EndTime { get; set; }=new(17,0); [Range(0,1440)] public int BreakMinutes { get; set; }=60; public bool CrossesMidnight { get; set; } public bool IsFlexible { get; set; } public bool IsActive { get; set; }=true; }
public sealed class AttendancePolicyForm { public Guid? Id { get; set; } public string Code { get; set; }=""; [Required] public string Name { get; set; }=""; public int LateGraceMinutes { get; set; }=10; public int EarlyOutGraceMinutes { get; set; }=10; public int? HalfDayBelowMinutes { get; set; }=240; public int? AbsentBelowMinutes { get; set; }=60; public int OvertimeAfterMinutes { get; set; }=30; public int OvertimeRoundingMinutes { get; set; }=15; public decimal OvertimeMultiplier { get; set; }=1.5m; public decimal HolidayOvertimeMultiplier { get; set; }=2m; public short[] WeeklyOffDays { get; set; }=[7]; public bool RequireOvertimeApproval { get; set; }=true; public bool IsActive { get; set; }=true; }
public sealed class EmployeeScheduleForm { public Guid? Id { get; set; } [Required] public Guid? EmployeeId { get; set; } [Required] public Guid? ShiftId { get; set; } [Required] public Guid? PolicyId { get; set; } public Guid? HolidayCalendarId { get; set; } public DateOnly EffectiveFrom { get; set; }=DateOnly.FromDateTime(DateTime.Today); public DateOnly? EffectiveTo { get; set; } }
public sealed class ManualAttendanceForm { [Required] public Guid? EmployeeId { get; set; } public DateOnly WorkDate { get; set; }=DateOnly.FromDateTime(DateTime.Today); public DateTime? FirstInLocal { get; set; } public DateTime? LastOutLocal { get; set; } public int BreakMinutes { get; set; } public string Status { get; set; }="present"; [Required] public string Reason { get; set; }=""; public string? Notes { get; set; } }
public sealed class AttendanceRequestForm
{
    [Required] public Guid? EmployeeId { get; set; }
    [Required] public string Kind { get; set; } = "adjustment";
    public DateOnly WorkDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateTime? RequestedFirstInLocal { get; set; }
    public DateTime? RequestedLastOutLocal { get; set; }
    [Range(1, 1440)] public int RequestedMinutes { get; set; } = 60;
    [Range(typeof(decimal), "0", "10")] public decimal Multiplier { get; set; } = 1.5m;
    [Required] public string Reason { get; set; } = "";
}
