using System.ComponentModel.DataAnnotations;

namespace Employee_Attendance_Salary_Management.Models;

public sealed record PayComponentItem(Guid Id,string Code,string Name,string ComponentType,string CalculationMethod,bool Taxable,bool Pensionable,int DisplayOrder,bool IsActive);
public sealed record PayrollPeriodItem(Guid Id,string PeriodCode,DateOnly StartDate,DateOnly EndDate,DateOnly PaymentDate,string Status);
public sealed record EmployeeComponentItem(Guid Id,Guid EmployeeId,string EmployeeName,string EmployeeNo,Guid ComponentId,string ComponentName,string ComponentType,decimal? FixedAmount,decimal? Percentage,DateOnly EffectiveFrom,DateOnly? EffectiveTo,bool ComponentActive);
public sealed record PayrollRunItem(Guid Id,Guid PeriodId,string PeriodCode,Guid? BranchId,string? BranchName,int RunNo,string Status,string CalculationVersion,DateTimeOffset? StartedAt,DateTimeOffset? CompletedAt,string? Notes);
public sealed record PayrollResultItem(Guid Id,Guid RunId,Guid EmployeeId,string EmployeeName,string EmployeeNo,string BranchName,string DepartmentName,decimal BaseSalary,decimal GrossEarnings,decimal TotalDeductions,decimal EmployerContributions,decimal NetPay,decimal PayableDays,int WorkedMinutes,int OvertimeMinutes,decimal UnpaidLeaveDays,string Status);

public sealed class PayComponentForm { public Guid? Id{get;set;} public string Code{get;set;}=""; [Required]public string Name{get;set;}=""; [Required]public string ComponentType{get;set;}="earning"; [Required]public string CalculationMethod{get;set;}="fixed"; public bool Taxable{get;set;}=true; public bool Pensionable{get;set;} public int DisplayOrder{get;set;} public bool IsActive{get;set;}=true; }
public sealed class PayrollPeriodForm { public Guid? Id{get;set;} public string PeriodCode{get;set;}=""; public DateOnly StartDate{get;set;}=new(DateTime.Today.Year,DateTime.Today.Month,1); public DateOnly EndDate{get;set;}=new(DateTime.Today.Year,DateTime.Today.Month,DateTime.DaysInMonth(DateTime.Today.Year,DateTime.Today.Month)); public DateOnly PaymentDate{get;set;}=new(DateTime.Today.Year,DateTime.Today.Month,DateTime.DaysInMonth(DateTime.Today.Year,DateTime.Today.Month)); }
public sealed class EmployeeComponentForm { public Guid? Id{get;set;} [Required]public Guid? EmployeeId{get;set;} [Required]public Guid? ComponentId{get;set;} public decimal? FixedAmount{get;set;} public decimal? Percentage{get;set;} public DateOnly EffectiveFrom{get;set;}=DateOnly.FromDateTime(DateTime.Today); public DateOnly? EffectiveTo{get;set;} }
public sealed class PayrollRunForm { [Required]public Guid? PeriodId{get;set;} public Guid? BranchId{get;set;} public string Notes{get;set;}=""; }
