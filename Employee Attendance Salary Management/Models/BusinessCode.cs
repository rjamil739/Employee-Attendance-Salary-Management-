namespace Employee_Attendance_Salary_Management.Models;

public static class BusinessCode
{
    public static string New(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..(prefix.Length + 9)].ToUpperInvariant();
}
