using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed record LeaveTypeItem(Guid Id,string Code,string Name,bool IsPaid,bool RequiresAttachment,bool AllowHalfDay,decimal? MaxConsecutiveDays,bool IsActive);
public sealed record LeavePolicyItem(Guid Id,Guid LeaveTypeId,string LeaveTypeName,string Name,decimal AnnualEntitlement,string AccrualFrequency,decimal CarryForwardLimit,bool AllowNegativeBalance,DateOnly EffectiveFrom,DateOnly? EffectiveTo)
{ public bool IsActive => EffectiveFrom <= DateOnly.FromDateTime(DateTime.Today) && (EffectiveTo is null || EffectiveTo > DateOnly.FromDateTime(DateTime.Today)); }
public sealed record EmployeeLeavePolicyItem(Guid Id,Guid EmployeeId,string EmployeeName,string EmployeeNo,Guid PolicyId,string PolicyName,string LeaveTypeName,DateOnly EffectiveFrom,DateOnly? EffectiveTo,bool PolicyActive)
{ public bool IsActive => PolicyActive && (EffectiveTo is null || EffectiveTo > DateOnly.FromDateTime(DateTime.Today)); }
public sealed record LeaveBalanceItem(Guid EmployeeId,string EmployeeName,string EmployeeNo,Guid LeaveTypeId,string LeaveTypeName,decimal Balance,bool PolicyActive);
public sealed record LeaveRequestItem(Guid Id,Guid EmployeeId,string EmployeeName,string EmployeeNo,string BranchName,Guid LeaveTypeId,string LeaveTypeName,DateOnly StartDate,DateOnly EndDate,decimal RequestedDays,string? Reason,string Status,string? DecisionNotes,DateTimeOffset? RequestedAt,bool PolicyActive,string? AttachmentKey);
public sealed record LeaveLedgerItem(long Id,Guid EmployeeId,string EmployeeName,string EmployeeNo,string LeaveTypeName,DateOnly TransactionDate,decimal Quantity,string EntryType,string? Reference);

public sealed class LeaveTypeForm { public Guid? Id{get;set;} public string Code{get;set;}=""; [Required]public string Name{get;set;}=""; public bool IsPaid{get;set;}=true; public bool RequiresAttachment{get;set;} public bool AllowHalfDay{get;set;}=true; public decimal? MaxConsecutiveDays{get;set;} public bool IsActive{get;set;}=true; }
public sealed class LeavePolicyForm { public Guid? Id{get;set;} [Required]public Guid? LeaveTypeId{get;set;} [Required]public string Name{get;set;}=""; [Range(0,999)]public decimal AnnualEntitlement{get;set;} [Required]public string AccrualFrequency{get;set;}="annual"; [Range(0,999)]public decimal CarryForwardLimit{get;set;} public bool AllowNegativeBalance{get;set;} public DateOnly EffectiveFrom{get;set;}=new(DateTime.Today.Year,1,1); public DateOnly? EffectiveTo{get;set;} }
public sealed class EmployeeLeavePolicyForm { public Guid? Id{get;set;} [Required]public Guid? EmployeeId{get;set;} [Required]public Guid? PolicyId{get;set;} public DateOnly EffectiveFrom{get;set;}=DateOnly.FromDateTime(DateTime.Today); public DateOnly? EffectiveTo{get;set;} public bool AddOpeningBalance{get;set;}=true; }
public sealed class LeaveBalanceForm { [Required]public Guid? EmployeeId{get;set;} [Required]public Guid? LeaveTypeId{get;set;} public decimal Quantity{get;set;} [Required]public string Reference{get;set;}="Opening balance"; }
public sealed class LeaveRequestForm { [Required]public Guid? EmployeeId{get;set;} [Required]public Guid? LeaveTypeId{get;set;} public DateOnly StartDate{get;set;}=DateOnly.FromDateTime(DateTime.Today); public DateOnly EndDate{get;set;}=DateOnly.FromDateTime(DateTime.Today); public bool IsHalfDay{get;set;} [Required]public string Reason{get;set;}=""; }
