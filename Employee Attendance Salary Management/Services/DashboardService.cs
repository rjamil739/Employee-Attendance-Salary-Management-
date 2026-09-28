using Employee_Attendance_Salary_Management.Models;
using Npgsql;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class DashboardService
{
    public async Task<DashboardSummary> GetAsync(Guid userId, bool hasAllBranchAccess, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            WITH accessible_branches AS (
                SELECT b.id, b.name
                FROM core.branch b
                WHERE b.is_active = true
                  AND (@all_access OR EXISTS (
                      SELECT 1 FROM auth.user_branch ub
                      WHERE ub.user_id = @user_id AND ub.branch_id = b.id
                  ))
            ),
            current_assignments AS (
                SELECT DISTINCT ON (ea.employee_id) ea.employee_id, ea.branch_id
                FROM hr.employee_assignment ea
                JOIN accessible_branches ab ON ab.id = ea.branch_id
                WHERE ea.effective_from <= current_date
                  AND (ea.effective_to IS NULL OR ea.effective_to >= current_date)
                ORDER BY ea.employee_id, ea.effective_from DESC
            )
            SELECT
                (SELECT count(*) FROM accessible_branches),
                (SELECT count(*) FROM hr.employee e JOIN current_assignments ca ON ca.employee_id = e.id WHERE e.employment_status = 'active'),
                (SELECT count(*) FROM attendance.attendance_day ad JOIN accessible_branches ab ON ab.id = ad.branch_id WHERE ad.work_date = current_date AND ad.status IN ('present', 'late')),
                (SELECT count(*) FROM leave_mgmt.leave_request lr JOIN accessible_branches ab ON ab.id = lr.branch_id WHERE lr.status = 'pending'),
                (SELECT count(*) FROM attendance.adjustment_request ar JOIN accessible_branches ab ON ab.id = ar.branch_id WHERE ar.status = 'pending')
                  + (SELECT count(*) FROM attendance.overtime_request ot JOIN accessible_branches ab ON ab.id = ot.branch_id WHERE ot.status = 'pending'),
                (SELECT count(*) FROM payroll.payroll_run pr WHERE pr.status IN ('draft', 'calculating', 'calculated', 'approved') AND (@all_access OR pr.branch_id IN (SELECT id FROM accessible_branches))),
                (SELECT count(*) FROM loans.employee_loan el JOIN accessible_branches ab ON ab.id = el.branch_id WHERE el.status IN ('approved', 'active', 'paused')),
                (SELECT count(*) FROM attendance.zkt_device zd JOIN accessible_branches ab ON ab.id = zd.branch_id WHERE zd.is_active = true),
                (SELECT count(*) FROM workflow.approval_request wr WHERE wr.status = 'pending' AND (@all_access OR wr.branch_id IN (SELECT id FROM accessible_branches))),
                (SELECT count(*) FROM auth.user_account ua WHERE ua.is_active = true AND @all_access),
                (SELECT COALESCE(sum(sp.amount), 0) FROM finance.salary_payment sp JOIN accessible_branches ab ON ab.id = sp.branch_id WHERE sp.status = 'paid' AND sp.paid_at >= date_trunc('month', current_date)),
                CASE
                    WHEN @all_access THEN 'All branches'
                    ELSE COALESCE((SELECT string_agg(name, ', ' ORDER BY name) FROM accessible_branches), 'No branch assigned')
                END
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("all_access", hasAllBranchAccess);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return new DashboardSummary
        {
            Branches = checked((int)reader.GetInt64(0)),
            Employees = checked((int)reader.GetInt64(1)),
            PresentToday = checked((int)reader.GetInt64(2)),
            PendingLeaves = checked((int)reader.GetInt64(3)),
            PendingAttendanceRequests = checked((int)reader.GetInt64(4)),
            ActivePayrollRuns = checked((int)reader.GetInt64(5)),
            ActiveLoans = checked((int)reader.GetInt64(6)),
            ActiveDevices = checked((int)reader.GetInt64(7)),
            PendingApprovals = checked((int)reader.GetInt64(8)),
            ActiveUsers = checked((int)reader.GetInt64(9)),
            PaidThisMonth = reader.GetDecimal(10),
            BranchLabel = reader.GetString(11)
        };
    }
}
