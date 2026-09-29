using Employee_Attendance_Salary_Management.Models;
using Npgsql;
using NpgsqlTypes;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class ContractorService
{
    public async Task<IReadOnlyList<ContractorItem>> GetAllAsync(CancellationToken ct=default)
    {
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);
        await using var cmd=new NpgsqlCommand("""
            SELECT c.id,c.code,c.name,c.contact_person,c.phone_number,c.email,c.address,c.city,c.is_active,
              COALESCE(array_agg(b.id ORDER BY b.name) FILTER(WHERE cb.is_active=true),ARRAY[]::uuid[]),
              COALESCE(array_agg(b.name ORDER BY b.name) FILTER(WHERE cb.is_active=true),ARRAY[]::text[])
            FROM core.contractor c LEFT JOIN core.contractor_branch cb ON cb.contractor_id=c.id
            LEFT JOIN core.branch b ON b.id=cb.branch_id GROUP BY c.id ORDER BY c.is_active DESC,c.name
            """,c);
        await using var r=await cmd.ExecuteReaderAsync(ct);var list=new List<ContractorItem>();
        while(await r.ReadAsync(ct))list.Add(new ContractorItem{Id=r.GetGuid(0),Code=r.GetString(1),Name=r.GetString(2),ContactPerson=r.IsDBNull(3)?null:r.GetString(3),PhoneNumber=r.IsDBNull(4)?null:r.GetString(4),Email=r.IsDBNull(5)?null:r.GetString(5),Address=r.IsDBNull(6)?null:r.GetString(6),City=r.IsDBNull(7)?null:r.GetString(7),IsActive=r.GetBoolean(8),ActiveBranchIds=r.GetFieldValue<Guid[]>(9),ActiveBranchNames=r.GetFieldValue<string[]>(10)});
        return list;
    }

    public async Task SaveAsync(ContractorFormModel m,Guid actor,CancellationToken ct=default)
    {
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);var id=m.Id??Guid.NewGuid();
        var sql=m.Id is null?"""
            INSERT INTO core.contractor(id,code,name,contact_person,phone_number,email,address,city,is_active,created_by,updated_by)
            VALUES(@id,@code,@name,@contact,@phone,@email,@address,@city,@active,@actor,@actor)
            """:"""
            UPDATE core.contractor SET code=@code,name=@name,contact_person=@contact,phone_number=@phone,email=@email,address=@address,city=@city,is_active=@active,updated_at=now(),updated_by=@actor WHERE id=@id
            """;
        await using(var cmd=new NpgsqlCommand(sql,c,tx)){cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("code",m.Id is null?BusinessCode.New("CTR"):m.Code.Trim().ToUpperInvariant());cmd.Parameters.AddWithValue("name",m.Name.Trim());AddText(cmd,"contact",m.ContactPerson);AddText(cmd,"phone",m.PhoneNumber);AddText(cmd,"email",m.Email,NpgsqlDbType.Varchar);AddText(cmd,"address",m.Address);AddText(cmd,"city",m.City,NpgsqlDbType.Varchar);cmd.Parameters.AddWithValue("active",m.IsActive);cmd.Parameters.AddWithValue("actor",actor);await cmd.ExecuteNonQueryAsync(ct);}
        await using(var off=new NpgsqlCommand("UPDATE core.contractor_branch SET is_active=false,updated_by=@actor WHERE contractor_id=@id",c,tx)){off.Parameters.AddWithValue("id",id);off.Parameters.AddWithValue("actor",actor);await off.ExecuteNonQueryAsync(ct);}
        foreach(var branch in m.BranchIds){await using var map=new NpgsqlCommand("""
            INSERT INTO core.contractor_branch(contractor_id,branch_id,is_active,created_by,updated_by) VALUES(@id,@branch,true,@actor,@actor)
            ON CONFLICT(contractor_id,branch_id) DO UPDATE SET is_active=true,updated_by=@actor
            """,c,tx);map.Parameters.AddWithValue("id",id);map.Parameters.AddWithValue("branch",branch);map.Parameters.AddWithValue("actor",actor);await map.ExecuteNonQueryAsync(ct);}
        await tx.CommitAsync(ct);
    }

    public async Task SetActiveAsync(Guid id,bool active,Guid actor,CancellationToken ct=default){await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("UPDATE core.contractor SET is_active=@active,updated_at=now(),updated_by=@actor WHERE id=@id",c);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("active",active);cmd.Parameters.AddWithValue("actor",actor);await cmd.ExecuteNonQueryAsync(ct);}
    private static void AddText(NpgsqlCommand c,string name,string? value,NpgsqlDbType type=NpgsqlDbType.Text)=>c.Parameters.Add(name,type).Value=string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim();
}
