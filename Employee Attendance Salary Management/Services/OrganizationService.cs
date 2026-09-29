using Employee_Attendance_Salary_Management.Models;
using Npgsql;
using NpgsqlTypes;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class OrganizationService
{
    public async Task<IReadOnlyList<DepartmentItem>> GetDepartmentsAsync(CancellationToken ct = default)
    {
        await using var c = new NpgsqlConnection(StaticConnection.conn); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT d.id,d.branch_id,b.name,d.parent_department_id,p.name,d.code,d.name,d.is_active
            FROM core.department d JOIN core.branch b ON b.id=d.branch_id
            LEFT JOIN core.department p ON p.id=d.parent_department_id
            ORDER BY b.name,d.name
            """, c);
        await using var r = await cmd.ExecuteReaderAsync(ct); var items = new List<DepartmentItem>();
        while (await r.ReadAsync(ct)) items.Add(new DepartmentItem { Id=r.GetGuid(0),BranchId=r.GetGuid(1),BranchName=r.GetString(2),ParentDepartmentId=r.IsDBNull(3)?null:r.GetGuid(3),ParentDepartmentName=r.IsDBNull(4)?null:r.GetString(4),Code=r.GetString(5),Name=r.GetString(6),IsActive=r.GetBoolean(7) });
        return items;
    }

    public async Task<IReadOnlyList<DesignationItem>> GetDesignationsAsync(CancellationToken ct = default)
    {
        await using var c = new NpgsqlConnection(StaticConnection.conn); await c.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand("""
            SELECT x.id,x.branch_id,b.name,x.department_id,d.name,x.code,x.name,x.grade,x.is_active
            FROM core.designation x JOIN core.branch b ON b.id=x.branch_id
            JOIN core.department d ON d.id=x.department_id ORDER BY b.name,d.name,x.name
            """, c);
        await using var r = await cmd.ExecuteReaderAsync(ct); var items = new List<DesignationItem>();
        while (await r.ReadAsync(ct)) items.Add(new DesignationItem { Id=r.GetGuid(0),BranchId=r.GetGuid(1),BranchName=r.GetString(2),DepartmentId=r.GetGuid(3),DepartmentName=r.GetString(4),Code=r.GetString(5),Name=r.GetString(6),Grade=r.IsDBNull(7)?null:r.GetString(7),IsActive=r.GetBoolean(8) });
        return items;
    }

    public async Task SaveDepartmentAsync(DepartmentFormModel m, Guid actor, CancellationToken ct = default)
    {
        if (m.BranchId is null) throw new InvalidOperationException("Select a branch.");
        if (m.Id is not null && m.ParentDepartmentId == m.Id)
            throw new InvalidOperationException("A department cannot be its own parent.");
        await using var c = new NpgsqlConnection(StaticConnection.conn); await c.OpenAsync(ct);
        var sql = m.Id is null ? """
            INSERT INTO core.department(branch_id,parent_department_id,code,name,is_active,created_by,updated_by)
            VALUES(@branch,@parent,@code,@name,@active,@actor,@actor)
            """ : """
            UPDATE core.department SET branch_id=@branch,parent_department_id=@parent,code=@code,name=@name,is_active=@active,updated_at=now(),updated_by=@actor WHERE id=@id
            """;
        await using var cmd = new NpgsqlCommand(sql,c); AddCommon(cmd,m.BranchId.Value,m.ParentDepartmentId,m.Id is null ? BusinessCode.New("DEP") : m.Code,m.Name,m.IsActive,actor); if(m.Id is not null)cmd.Parameters.AddWithValue("id",m.Id.Value); await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveDesignationAsync(DesignationFormModel m, Guid actor, CancellationToken ct = default)
    {
        if(m.BranchId is null||m.DepartmentId is null) throw new InvalidOperationException("Select branch and department.");
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);
        var sql=m.Id is null?"""
            INSERT INTO core.designation(branch_id,department_id,code,name,grade,is_active,created_by,updated_by)
            VALUES(@branch,@department,@code,@name,@grade,@active,@actor,@actor)
            """:"""
            UPDATE core.designation SET branch_id=@branch,department_id=@department,code=@code,name=@name,grade=@grade,is_active=@active,updated_at=now(),updated_by=@actor WHERE id=@id
            """;
        await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("branch",m.BranchId.Value);cmd.Parameters.AddWithValue("department",m.DepartmentId.Value);cmd.Parameters.AddWithValue("code",m.Id is null?BusinessCode.New("DSG"):m.Code.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("name",m.Name.Trim());cmd.Parameters.Add("grade",NpgsqlDbType.Text).Value=Db(m.Grade);cmd.Parameters.AddWithValue("active",m.IsActive);cmd.Parameters.AddWithValue("actor",actor);if(m.Id is not null)cmd.Parameters.AddWithValue("id",m.Id.Value);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetActiveAsync(string entity,Guid id,bool active,Guid actor,CancellationToken ct=default)
    {
        var table=entity=="department"?"core.department":entity=="designation"?"core.designation":throw new ArgumentException("Invalid entity.");
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand($"UPDATE {table} SET is_active=@active,updated_at=now(),updated_by=@actor WHERE id=@id",c);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("active",active);cmd.Parameters.AddWithValue("actor",actor);await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AddCommon(NpgsqlCommand cmd,Guid branch,Guid? parent,string code,string name,bool active,Guid actor){cmd.Parameters.AddWithValue("branch",branch);cmd.Parameters.Add("parent",NpgsqlDbType.Uuid).Value=parent is null?DBNull.Value:parent.Value;cmd.Parameters.AddWithValue("code",code.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("name",name.Trim());cmd.Parameters.AddWithValue("active",active);cmd.Parameters.AddWithValue("actor",actor);}
    private static object Db(string? value)=>string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim();
}
