using Employee_Attendance_Salary_Management.Models;
using Npgsql;
using NpgsqlTypes;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class AccessControlService
{
    public async Task<IReadOnlyList<RoleItem>> GetRolesAsync(CancellationToken ct=default)
    {
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("""
            SELECT r.id,r.code,r.name,r.description,r.scope_type,r.is_system,count(ur.user_id)::int
            FROM auth.role r LEFT JOIN auth.user_role ur ON ur.role_id=r.id GROUP BY r.id ORDER BY r.is_system DESC,r.name
            """,c);await using var rd=await cmd.ExecuteReaderAsync(ct);var list=new List<RoleItem>();while(await rd.ReadAsync(ct))list.Add(new RoleItem{Id=rd.GetGuid(0),Code=rd.GetString(1),Name=rd.GetString(2),Description=rd.IsDBNull(3)?null:rd.GetString(3),ScopeType=rd.GetString(4),IsSystem=rd.GetBoolean(5),UserCount=rd.GetInt32(6)});return list;
    }

    public async Task SaveRoleAsync(RoleFormModel m,Guid actor,CancellationToken ct=default)
    {
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);var sql=m.Id is null?"""
          INSERT INTO auth.role(code,name,description,scope_type,is_system,created_by,updated_by) VALUES(@code,@name,@description,@scope,false,@actor,@actor)
          """:"""
          UPDATE auth.role SET code=CASE WHEN is_system THEN code ELSE @code END,name=@name,description=@description,scope_type=@scope,updated_by=@actor WHERE id=@id
          """;await using var cmd=new NpgsqlCommand(sql,c);cmd.Parameters.AddWithValue("code",m.Id is null?BusinessCode.New("ROLE"):m.Code.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("name",m.Name.Trim());cmd.Parameters.Add("description",NpgsqlDbType.Text).Value=string.IsNullOrWhiteSpace(m.Description)?DBNull.Value:m.Description.Trim();cmd.Parameters.AddWithValue("scope",m.ScopeType);cmd.Parameters.AddWithValue("actor",actor);if(m.Id is not null)cmd.Parameters.AddWithValue("id",m.Id.Value);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<UserAccessAssignment> GetAssignmentAsync(Guid userId,CancellationToken ct=default)
    {
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);var result=new UserAccessAssignment{UserId=userId};
        await using(var cmd=new NpgsqlCommand("SELECT has_all_branch_access FROM auth.user_account WHERE id=@id",c)){cmd.Parameters.AddWithValue("id",userId);result.HasAllBranchAccess=(bool)(await cmd.ExecuteScalarAsync(ct)??false);}
        await using(var cmd=new NpgsqlCommand("SELECT role_id FROM auth.user_role WHERE user_id=@id",c)){cmd.Parameters.AddWithValue("id",userId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))result.RoleIds.Add(r.GetGuid(0));}
        await using(var cmd=new NpgsqlCommand("SELECT branch_id,is_default FROM auth.user_branch WHERE user_id=@id",c)){cmd.Parameters.AddWithValue("id",userId);await using var r=await cmd.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct)){var id=r.GetGuid(0);result.BranchIds.Add(id);if(r.GetBoolean(1))result.DefaultBranchId=id;}}
        return result;
    }

    public async Task SaveAssignmentAsync(UserAccessAssignment m,Guid actor,Guid adminRoleId,CancellationToken ct=default)
    {
        if(m.UserId==actor&&!m.RoleIds.Contains(adminRoleId))throw new InvalidOperationException("You cannot remove your own administrator role.");
        if(m.DefaultBranchId is not null&&!m.BranchIds.Contains(m.DefaultBranchId.Value))throw new InvalidOperationException("Default branch must be one of the assigned branches.");
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
        await Exec(c,tx,"DELETE FROM auth.user_role WHERE user_id=@user",m.UserId,actor,ct);foreach(var role in m.RoleIds){await using var cmd=new NpgsqlCommand("INSERT INTO auth.user_role(user_id,role_id,created_by,updated_by) VALUES(@user,@value,@actor,@actor)",c,tx);Add(cmd,m.UserId,role,actor);await cmd.ExecuteNonQueryAsync(ct);}
        await Exec(c,tx,"DELETE FROM auth.user_branch WHERE user_id=@user",m.UserId,actor,ct);foreach(var branch in m.BranchIds){await using var cmd=new NpgsqlCommand("INSERT INTO auth.user_branch(user_id,branch_id,is_default,created_by,updated_by) VALUES(@user,@value,@default,@actor,@actor)",c,tx);Add(cmd,m.UserId,branch,actor);cmd.Parameters.AddWithValue("default",m.DefaultBranchId==branch);await cmd.ExecuteNonQueryAsync(ct);}
        await using(var cmd=new NpgsqlCommand("UPDATE auth.user_account SET has_all_branch_access=@all,updated_at=now(),updated_by=@actor WHERE id=@user",c,tx)){cmd.Parameters.AddWithValue("all",m.HasAllBranchAccess);cmd.Parameters.AddWithValue("actor",actor);cmd.Parameters.AddWithValue("user",m.UserId);await cmd.ExecuteNonQueryAsync(ct);}await tx.CommitAsync(ct);
    }
    private static async Task Exec(NpgsqlConnection c,NpgsqlTransaction tx,string sql,Guid user,Guid actor,CancellationToken ct){await using var cmd=new NpgsqlCommand(sql,c,tx);cmd.Parameters.AddWithValue("user",user);cmd.Parameters.AddWithValue("actor",actor);await cmd.ExecuteNonQueryAsync(ct);}
    private static void Add(NpgsqlCommand c,Guid user,Guid value,Guid actor){c.Parameters.AddWithValue("user",user);c.Parameters.AddWithValue("value",value);c.Parameters.AddWithValue("actor",actor);}
}
