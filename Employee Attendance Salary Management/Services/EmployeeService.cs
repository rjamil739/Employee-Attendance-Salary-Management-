using System.Security.Cryptography;
using System.Text;
using Employee_Attendance_Salary_Management.Models;
using Microsoft.AspNetCore.DataProtection;
using Npgsql;
using NpgsqlTypes;

namespace Employee_Attendance_Salary_Management.Services;

public sealed class EmployeeService(IDataProtectionProvider dataProtection, IWebHostEnvironment environment)
{
    private readonly IDataProtector protector = dataProtection.CreateProtector("hr-payroll.employee-sensitive-data.v1");

    public async Task<IReadOnlyList<EmployeeListItem>> GetAllAsync(Guid userId, bool hasAllBranchAccess, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT e.id,e.employee_no,e.payroll_no,concat_ws(' ',e.first_name,e.last_name),e.photo,
                   e.mobile_phone,e.email,e.employment_status,e.employment_type,e.hire_date,
                   a.branch_id,b.name,d.name,g.name,c.base_salary
            FROM hr.employee e
            LEFT JOIN LATERAL (
                SELECT ea.* FROM hr.employee_assignment ea
                WHERE ea.employee_id=e.id AND ea.effective_from<=current_date
                  AND (ea.effective_to IS NULL OR ea.effective_to>=current_date)
                ORDER BY ea.effective_from DESC LIMIT 1
            ) a ON true
            LEFT JOIN core.branch b ON b.id=a.branch_id
            LEFT JOIN core.department d ON d.id=a.department_id
            LEFT JOIN core.designation g ON g.id=a.designation_id
            LEFT JOIN LATERAL (
                SELECT ec.base_salary FROM hr.employee_compensation ec
                WHERE ec.employee_id=e.id AND ec.effective_from<=current_date
                  AND (ec.effective_to IS NULL OR ec.effective_to>=current_date)
                ORDER BY ec.effective_from DESC LIMIT 1
            ) c ON true
            WHERE @all_access OR a.branch_id IS NULL OR EXISTS (
                SELECT 1 FROM auth.user_branch ub WHERE ub.user_id=@user_id AND ub.branch_id=a.branch_id)
            ORDER BY CASE e.employment_status WHEN 'active' THEN 0 WHEN 'draft' THEN 1 ELSE 2 END,
                     lower(e.first_name),lower(coalesce(e.last_name,''))
            """, connection);
        command.Parameters.AddWithValue("all_access", hasAllBranchAccess);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<EmployeeListItem>();
        while (await reader.ReadAsync(ct))
        {
            result.Add(new EmployeeListItem
            {
                Id=reader.GetGuid(0), EmployeeNo=reader.GetString(1), PayrollNo=Text(reader,2), FullName=reader.GetString(3),
                Photo=Bytes(reader,4), MobilePhone=Text(reader,5), Email=Text(reader,6), EmploymentStatus=reader.GetString(7),
                EmploymentType=reader.GetString(8), HireDate=reader.GetFieldValue<DateOnly>(9),
                BranchId=reader.IsDBNull(10)?null:reader.GetGuid(10), BranchName=Text(reader,11), DepartmentName=Text(reader,12),
                DesignationName=Text(reader,13), BaseSalary=reader.IsDBNull(14)?null:reader.GetDecimal(14)
            });
        }
        return result;
    }

    public async Task<EmployeeWorkspaceData> GetWorkspaceAsync(Guid employeeId, CancellationToken ct = default)
    {
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(ct);
        var employee = await GetEmployeeAsync(connection, employeeId, ct);
        if (employee is null) throw new InvalidOperationException("Employee no longer exists.");
        return new EmployeeWorkspaceData
        {
            Employee=employee,
            Assignments=await GetAssignmentsAsync(connection,employeeId,ct),
            Compensations=await GetCompensationsAsync(connection,employeeId,ct),
            BankAccounts=await GetBanksAsync(connection,employeeId,ct),
            Documents=await GetDocumentsAsync(connection,employeeId,ct),
            Fingerprints=await GetFingerprintsAsync(connection,employeeId,ct)
        };
    }

    public async Task<Guid> SaveOnboardingAsync(EmployeeOnboardingModel model, byte[]? photo, Guid actor, CancellationToken ct = default)
    {
        ValidateProfile(model.Profile);
        if (model.Assignment.BranchId is null || model.Assignment.DepartmentId is null || model.Assignment.DesignationId is null)
            throw new InvalidOperationException("Complete the employee assignment layer.");
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        try
        {
            var employeeId = await InsertEmployeeAsync(connection,tx,model.Profile,photo,actor,ct);
            await InsertAssignmentAsync(connection,tx,employeeId,model.Assignment,actor,ct);
            if (model.AddCompensation) await InsertCompensationAsync(connection,tx,employeeId,model.Compensation,actor,ct);
            if (model.AddBankAccount) await InsertBankAsync(connection,tx,employeeId,model.Bank,actor,ct);
            await tx.CommitAsync(ct);
            return employeeId;
        }
        catch { await tx.RollbackAsync(ct); throw; }
    }

    public async Task SaveProfileAsync(EmployeeEditorModel model, byte[]? photo, bool updatePhoto, bool updateNationalId, Guid actor, CancellationToken ct = default)
    {
        if (model.Id is null) throw new InvalidOperationException("Employee was not selected.");
        ValidateProfile(model);
        await using var connection = new NpgsqlConnection(StaticConnection.conn);
        await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            UPDATE hr.employee SET user_account_id=@user_account_id,first_name=@first_name,last_name=@last_name,
                father_name=@father_name,photo=CASE WHEN @update_photo THEN @photo ELSE photo END,
                national_id_encrypted=CASE WHEN @update_national_id THEN @national_id ELSE national_id_encrypted END,
                national_id_hash=CASE WHEN @update_national_id THEN @national_hash ELSE national_id_hash END,
                date_of_birth=@dob,gender=@gender,email=@email,mobile_phone=@mobile,address=@address,city=@city,
                district=@district,province=@province,postal_code=@postal,hire_date=@hire_date,
                confirmation_date=@confirmation_date,termination_date=@termination_date,
                employment_status=@status,employment_type=@type,preferred_payment_channel=@payment_channel,
                preferred_bank_payment_type=@bank_payment_type,updated_at=now(),updated_by=@actor
            WHERE id=@id
            """,connection);
        AddProfileParameters(command,model,photo,actor);
        command.Parameters.AddWithValue("id",model.Id.Value);
        command.Parameters.AddWithValue("update_photo",updatePhoto);
        command.Parameters.AddWithValue("update_national_id",updateNationalId);
        if (await command.ExecuteNonQueryAsync(ct)==0) throw new InvalidOperationException("Employee no longer exists.");
    }

    public async Task SaveAssignmentAsync(Guid employeeId, EmployeeAssignmentForm model, Guid actor, CancellationToken ct=default)
    {
        if(model.BranchId is null||model.DepartmentId is null||model.DesignationId is null) throw new InvalidOperationException("Select branch, department and designation.");
        ValidatePeriod(model.EffectiveFrom,model.EffectiveTo);
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);
        if(model.Id is null){await InsertAssignmentAsync(c,null,employeeId,model,actor,ct);return;}
        await using var cmd=new NpgsqlCommand("""
            UPDATE hr.employee_assignment SET branch_id=@branch,department_id=@department,designation_id=@designation,
            manager_employee_id=@manager,effective_from=@from,effective_to=@to,updated_at=now(),updated_by=@actor
            WHERE id=@id AND employee_id=@employee
            """,c);AddAssignmentParameters(cmd,model,actor);cmd.Parameters.AddWithValue("id",model.Id.Value);cmd.Parameters.AddWithValue("employee",employeeId);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveCompensationAsync(Guid employeeId,EmployeeCompensationForm model,Guid actor,CancellationToken ct=default)
    {
        ValidatePeriod(model.EffectiveFrom,model.EffectiveTo);
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);
        if(model.Id is null){await InsertCompensationAsync(c,null,employeeId,model,actor,ct);return;}
        await using var cmd=new NpgsqlCommand("""
            UPDATE hr.employee_compensation SET base_salary=@salary,pay_frequency=@frequency,effective_from=@from,
            effective_to=@to,reason=@reason,approved_by=@actor,updated_by=@actor WHERE id=@id AND employee_id=@employee
            """,c);AddCompensationParameters(cmd,model,actor);cmd.Parameters.AddWithValue("id",model.Id.Value);cmd.Parameters.AddWithValue("employee",employeeId);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveBankAsync(Guid employeeId,EmployeeBankForm model,Guid actor,CancellationToken ct=default)
    {
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var tx=await c.BeginTransactionAsync(ct);
        if(model.IsPrimary){await using var clear=new NpgsqlCommand("UPDATE hr.employee_bank_account SET is_primary=false,updated_at=now(),updated_by=@actor WHERE employee_id=@employee",c,tx);clear.Parameters.AddWithValue("actor",actor);clear.Parameters.AddWithValue("employee",employeeId);await clear.ExecuteNonQueryAsync(ct);}
        if(model.Id is null)await InsertBankAsync(c,tx,employeeId,model,actor,ct);else{await using var cmd=new NpgsqlCommand("""
            UPDATE hr.employee_bank_account SET bank_name=@bank,account_title=@title,iban_encrypted=@iban,
            account_number_encrypted=@account,is_primary=@primary,updated_at=now(),updated_by=@actor
            WHERE id=@id AND employee_id=@employee
            """,c,tx);AddBankParameters(cmd,model,actor);cmd.Parameters.AddWithValue("id",model.Id.Value);cmd.Parameters.AddWithValue("employee",employeeId);await cmd.ExecuteNonQueryAsync(ct);}await tx.CommitAsync(ct);
    }

    public async Task<Guid> AddDocumentAsync(Guid employeeId,string documentType,string fileName,string contentType,byte[] content,DateOnly? expiresOn,Guid actor,CancellationToken ct=default)
    {
        if(content.Length==0)throw new InvalidOperationException("Choose a document file.");
        var id=Guid.NewGuid();var safeName=string.Concat(Path.GetFileName(fileName).Select(ch=>char.IsLetterOrDigit(ch)||ch is '.' or '-' or '_'?ch:'_'));
        var relative=Path.Combine("uploads","employees",employeeId.ToString("N"),$"{id:N}-{safeName}").Replace('\\','/');
        var absolute=Path.Combine(environment.WebRootPath,relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);await File.WriteAllBytesAsync(absolute,content,ct);
        try{await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("""
            INSERT INTO hr.employee_document(id,employee_id,document_type,object_key,original_file_name,content_type,checksum_sha256,expires_on,created_by,updated_by)
            VALUES(@id,@employee,@type,@key,@name,@content_type,@checksum,@expires,@actor,@actor)
            """,c);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("employee",employeeId);cmd.Parameters.AddWithValue("type",documentType.Trim());cmd.Parameters.AddWithValue("key","/"+relative);cmd.Parameters.AddWithValue("name",Path.GetFileName(fileName));cmd.Parameters.AddWithValue("content_type",string.IsNullOrWhiteSpace(contentType)?"application/octet-stream":contentType);cmd.Parameters.AddWithValue("checksum",Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant());cmd.Parameters.Add("expires",NpgsqlDbType.Date).Value=expiresOn is null?DBNull.Value:expiresOn.Value;cmd.Parameters.AddWithValue("actor",actor);await cmd.ExecuteNonQueryAsync(ct);return id;}catch{File.Delete(absolute);throw;}
    }

    public async Task DeleteDocumentAsync(Guid documentId,Guid employeeId,CancellationToken ct=default)
    {
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);string? key=null;await using(var find=new NpgsqlCommand("SELECT object_key FROM hr.employee_document WHERE id=@id AND employee_id=@employee",c)){find.Parameters.AddWithValue("id",documentId);find.Parameters.AddWithValue("employee",employeeId);key=await find.ExecuteScalarAsync(ct) as string;}await using(var cmd=new NpgsqlCommand("DELETE FROM hr.employee_document WHERE id=@id AND employee_id=@employee",c)){cmd.Parameters.AddWithValue("id",documentId);cmd.Parameters.AddWithValue("employee",employeeId);await cmd.ExecuteNonQueryAsync(ct);}if(!string.IsNullOrWhiteSpace(key)){var full=Path.GetFullPath(Path.Combine(environment.WebRootPath,key.TrimStart('/').Replace('/',Path.DirectorySeparatorChar)));var root=Path.GetFullPath(environment.WebRootPath)+Path.DirectorySeparatorChar;if(full.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&File.Exists(full))File.Delete(full);}
    }

    public async Task SaveFingerprintAsync(Guid employeeId,string fingerType,byte[] template,string? format,byte[]? image,Guid actor,CancellationToken ct=default)
    {
        if(template.Length==0)throw new InvalidOperationException("Fingerprint template is required.");
        await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("""
            INSERT INTO hr.employee_fingerprint(employee_id,finger_type,fingerprint_template,template_format,fingerprint_image,image_content_type,is_active,created_by,updated_by)
            VALUES(@employee,@finger,@template,@format,@image,@image_type,true,@actor,@actor)
            ON CONFLICT(employee_id,finger_type) DO UPDATE SET fingerprint_template=excluded.fingerprint_template,
            template_format=excluded.template_format,fingerprint_image=excluded.fingerprint_image,
            image_content_type=excluded.image_content_type,is_active=true,updated_at=now(),updated_by=excluded.updated_by
            """,c);cmd.Parameters.AddWithValue("employee",employeeId);cmd.Parameters.AddWithValue("finger",fingerType);cmd.Parameters.Add("template",NpgsqlDbType.Bytea).Value=template;cmd.Parameters.Add("format",NpgsqlDbType.Text).Value=Db(format);cmd.Parameters.Add("image",NpgsqlDbType.Bytea).Value=image is null?DBNull.Value:image;cmd.Parameters.Add("image_type",NpgsqlDbType.Text).Value=image is null?DBNull.Value:"image/png";cmd.Parameters.AddWithValue("actor",actor);await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SetFingerprintActiveAsync(Guid id,Guid employeeId,bool active,Guid actor,CancellationToken ct=default){await using var c=new NpgsqlConnection(StaticConnection.conn);await c.OpenAsync(ct);await using var cmd=new NpgsqlCommand("UPDATE hr.employee_fingerprint SET is_active=@active,updated_at=now(),updated_by=@actor WHERE id=@id AND employee_id=@employee",c);cmd.Parameters.AddWithValue("active",active);cmd.Parameters.AddWithValue("actor",actor);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("employee",employeeId);await cmd.ExecuteNonQueryAsync(ct);}

    private async Task<Guid> InsertEmployeeAsync(NpgsqlConnection c,NpgsqlTransaction tx,EmployeeEditorModel m,byte[]? photo,Guid actor,CancellationToken ct){var id=Guid.NewGuid();await using var cmd=new NpgsqlCommand("""
        INSERT INTO hr.employee(id,user_account_id,employee_no,payroll_no,first_name,last_name,father_name,photo,national_id_encrypted,national_id_hash,date_of_birth,gender,email,mobile_phone,address,city,district,province,postal_code,hire_date,confirmation_date,termination_date,employment_status,employment_type,preferred_payment_channel,preferred_bank_payment_type,created_by,updated_by)
        VALUES(@id,@user_account_id,@employee_no,@payroll_no,@first_name,@last_name,@father_name,@photo,@national_id,@national_hash,@dob,@gender,@email,@mobile,@address,@city,@district,@province,@postal,@hire_date,@confirmation_date,@termination_date,@status,@type,@payment_channel,@bank_payment_type,@actor,@actor)
        """,c,tx);AddProfileParameters(cmd,m,photo,actor);cmd.Parameters.AddWithValue("id",id);cmd.Parameters.AddWithValue("employee_no",BusinessCode.New("EMP"));cmd.Parameters.AddWithValue("payroll_no",BusinessCode.New("PAY"));await cmd.ExecuteNonQueryAsync(ct);return id;}

    private async Task InsertAssignmentAsync(NpgsqlConnection c,NpgsqlTransaction? tx,Guid employee,EmployeeAssignmentForm m,Guid actor,CancellationToken ct){ValidatePeriod(m.EffectiveFrom,m.EffectiveTo);await using var cmd=new NpgsqlCommand("""
        INSERT INTO hr.employee_assignment(employee_id,branch_id,department_id,designation_id,manager_employee_id,effective_from,effective_to,created_by,updated_by)
        VALUES(@employee,@branch,@department,@designation,@manager,@from,@to,@actor,@actor)
        """,c,tx);AddAssignmentParameters(cmd,m,actor);cmd.Parameters.AddWithValue("employee",employee);await cmd.ExecuteNonQueryAsync(ct);}
    private async Task InsertCompensationAsync(NpgsqlConnection c,NpgsqlTransaction? tx,Guid employee,EmployeeCompensationForm m,Guid actor,CancellationToken ct){ValidatePeriod(m.EffectiveFrom,m.EffectiveTo);await using var cmd=new NpgsqlCommand("""
        INSERT INTO hr.employee_compensation(employee_id,base_salary,pay_frequency,effective_from,effective_to,reason,approved_by,created_by,updated_by)
        VALUES(@employee,@salary,@frequency,@from,@to,@reason,@actor,@actor,@actor)
        """,c,tx);AddCompensationParameters(cmd,m,actor);cmd.Parameters.AddWithValue("employee",employee);await cmd.ExecuteNonQueryAsync(ct);}
    private async Task InsertBankAsync(NpgsqlConnection c,NpgsqlTransaction? tx,Guid employee,EmployeeBankForm m,Guid actor,CancellationToken ct){await using var cmd=new NpgsqlCommand("""
        INSERT INTO hr.employee_bank_account(employee_id,bank_name,account_title,iban_encrypted,account_number_encrypted,is_primary,created_by,updated_by)
        VALUES(@employee,@bank,@title,@iban,@account,@primary,@actor,@actor)
        """,c,tx);AddBankParameters(cmd,m,actor);cmd.Parameters.AddWithValue("employee",employee);await cmd.ExecuteNonQueryAsync(ct);}

    private void AddProfileParameters(NpgsqlCommand cmd,EmployeeEditorModel m,byte[]? photo,Guid actor){var national=NormalizeNationalId(m.NationalId);cmd.Parameters.Add("user_account_id",NpgsqlDbType.Uuid).Value=m.UserAccountId is null?DBNull.Value:m.UserAccountId.Value;cmd.Parameters.AddWithValue("first_name",m.FirstName.Trim());AddText(cmd,"last_name",m.LastName);AddText(cmd,"father_name",m.FatherName);cmd.Parameters.Add("photo",NpgsqlDbType.Bytea).Value=photo is null?DBNull.Value:photo;cmd.Parameters.Add("national_id",NpgsqlDbType.Bytea).Value=national is null?DBNull.Value:Protect(national);cmd.Parameters.Add("national_hash",NpgsqlDbType.Char).Value=national is null?DBNull.Value:Hash(national);cmd.Parameters.Add("dob",NpgsqlDbType.Date).Value=m.DateOfBirth is null?DBNull.Value:m.DateOfBirth.Value;AddText(cmd,"gender",m.Gender);AddText(cmd,"email",m.Email);AddText(cmd,"mobile",m.MobilePhone);AddText(cmd,"address",m.Address);AddText(cmd,"city",m.City);AddText(cmd,"district",m.District);AddText(cmd,"province",m.Province);AddText(cmd,"postal",m.PostalCode);cmd.Parameters.AddWithValue("hire_date",m.HireDate);cmd.Parameters.Add("confirmation_date",NpgsqlDbType.Date).Value=m.ConfirmationDate is null?DBNull.Value:m.ConfirmationDate.Value;cmd.Parameters.Add("termination_date",NpgsqlDbType.Date).Value=m.TerminationDate is null?DBNull.Value:m.TerminationDate.Value;cmd.Parameters.AddWithValue("status",m.EmploymentStatus);cmd.Parameters.AddWithValue("type",m.EmploymentType);cmd.Parameters.AddWithValue("payment_channel",m.PreferredPaymentChannel);AddText(cmd,"bank_payment_type",m.PreferredPaymentChannel=="bank"?m.PreferredBankPaymentType:null);cmd.Parameters.AddWithValue("actor",actor);}
    private static void AddAssignmentParameters(NpgsqlCommand cmd,EmployeeAssignmentForm m,Guid actor){cmd.Parameters.AddWithValue("branch",m.BranchId!.Value);cmd.Parameters.AddWithValue("department",m.DepartmentId!.Value);cmd.Parameters.AddWithValue("designation",m.DesignationId!.Value);cmd.Parameters.Add("manager",NpgsqlDbType.Uuid).Value=m.ManagerEmployeeId is null?DBNull.Value:m.ManagerEmployeeId.Value;cmd.Parameters.AddWithValue("from",m.EffectiveFrom);cmd.Parameters.Add("to",NpgsqlDbType.Date).Value=m.EffectiveTo is null?DBNull.Value:m.EffectiveTo.Value;cmd.Parameters.AddWithValue("actor",actor);}
    private static void AddCompensationParameters(NpgsqlCommand cmd,EmployeeCompensationForm m,Guid actor){cmd.Parameters.AddWithValue("salary",m.BaseSalary);cmd.Parameters.AddWithValue("frequency",m.PayFrequency);cmd.Parameters.AddWithValue("from",m.EffectiveFrom);cmd.Parameters.Add("to",NpgsqlDbType.Date).Value=m.EffectiveTo is null?DBNull.Value:m.EffectiveTo.Value;AddText(cmd,"reason",m.Reason);cmd.Parameters.AddWithValue("actor",actor);}
    private void AddBankParameters(NpgsqlCommand cmd,EmployeeBankForm m,Guid actor){cmd.Parameters.AddWithValue("bank",m.BankName.Trim());cmd.Parameters.AddWithValue("title",m.AccountTitle.Trim());cmd.Parameters.Add("iban",NpgsqlDbType.Bytea).Value=string.IsNullOrWhiteSpace(m.Iban)?DBNull.Value:Protect(m.Iban.Trim().ToUpperInvariant());cmd.Parameters.Add("account",NpgsqlDbType.Bytea).Value=string.IsNullOrWhiteSpace(m.AccountNumber)?DBNull.Value:Protect(m.AccountNumber.Trim());cmd.Parameters.AddWithValue("primary",m.IsPrimary);cmd.Parameters.AddWithValue("actor",actor);}

    private async Task<EmployeeEditorModel?> GetEmployeeAsync(NpgsqlConnection c,Guid id,CancellationToken ct){await using var cmd=new NpgsqlCommand("""
        SELECT id,user_account_id,employee_no,payroll_no,first_name,last_name,father_name,national_id_encrypted,photo,date_of_birth,gender,email,mobile_phone,address,city,district,province,postal_code,hire_date,confirmation_date,termination_date,employment_status,employment_type,preferred_payment_channel,preferred_bank_payment_type FROM hr.employee WHERE id=@id
        """,c);cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(ct);if(!await r.ReadAsync(ct))return null;return new EmployeeEditorModel{Id=r.GetGuid(0),UserAccountId=r.IsDBNull(1)?null:r.GetGuid(1),EmployeeNo=r.GetString(2),PayrollNo=Text(r,3),FirstName=r.GetString(4),LastName=Text(r,5),FatherName=Text(r,6),NationalId=Unprotect(Bytes(r,7)),ExistingPhoto=Bytes(r,8),DateOfBirth=r.IsDBNull(9)?null:r.GetFieldValue<DateOnly>(9),Gender=Text(r,10),Email=Text(r,11),MobilePhone=Text(r,12),Address=Text(r,13),City=Text(r,14),District=Text(r,15),Province=Text(r,16),PostalCode=Text(r,17),HireDate=r.GetFieldValue<DateOnly>(18),ConfirmationDate=r.IsDBNull(19)?null:r.GetFieldValue<DateOnly>(19),TerminationDate=r.IsDBNull(20)?null:r.GetFieldValue<DateOnly>(20),EmploymentStatus=r.GetString(21),EmploymentType=r.GetString(22),PreferredPaymentChannel=r.GetString(23),PreferredBankPaymentType=Text(r,24)};}
    private static async Task<IReadOnlyList<EmployeeAssignmentItem>> GetAssignmentsAsync(NpgsqlConnection c,Guid id,CancellationToken ct){await using var cmd=new NpgsqlCommand("""
        SELECT a.id,a.branch_id,a.department_id,a.designation_id,a.manager_employee_id,b.name,d.name,g.name,concat_ws(' ',m.first_name,m.last_name),a.effective_from,a.effective_to FROM hr.employee_assignment a JOIN core.branch b ON b.id=a.branch_id JOIN core.department d ON d.id=a.department_id JOIN core.designation g ON g.id=a.designation_id LEFT JOIN hr.employee m ON m.id=a.manager_employee_id WHERE a.employee_id=@id ORDER BY a.effective_from DESC
        """,c);cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(ct);var x=new List<EmployeeAssignmentItem>();while(await r.ReadAsync(ct))x.Add(new(){Id=r.GetGuid(0),BranchId=r.GetGuid(1),DepartmentId=r.GetGuid(2),DesignationId=r.GetGuid(3),ManagerEmployeeId=r.IsDBNull(4)?null:r.GetGuid(4),BranchName=r.GetString(5),DepartmentName=r.GetString(6),DesignationName=r.GetString(7),ManagerName=Text(r,8),EffectiveFrom=r.GetFieldValue<DateOnly>(9),EffectiveTo=r.IsDBNull(10)?null:r.GetFieldValue<DateOnly>(10)});return x;}
    private static async Task<IReadOnlyList<EmployeeCompensationItem>> GetCompensationsAsync(NpgsqlConnection c,Guid id,CancellationToken ct){await using var cmd=new NpgsqlCommand("SELECT id,base_salary,pay_frequency,effective_from,effective_to,reason FROM hr.employee_compensation WHERE employee_id=@id ORDER BY effective_from DESC",c);cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(ct);var x=new List<EmployeeCompensationItem>();while(await r.ReadAsync(ct))x.Add(new(){Id=r.GetGuid(0),BaseSalary=r.GetDecimal(1),PayFrequency=r.GetString(2),EffectiveFrom=r.GetFieldValue<DateOnly>(3),EffectiveTo=r.IsDBNull(4)?null:r.GetFieldValue<DateOnly>(4),Reason=Text(r,5)});return x;}
    private async Task<IReadOnlyList<EmployeeBankItem>> GetBanksAsync(NpgsqlConnection c,Guid id,CancellationToken ct){await using var cmd=new NpgsqlCommand("SELECT id,bank_name,account_title,iban_encrypted,account_number_encrypted,is_primary FROM hr.employee_bank_account WHERE employee_id=@id ORDER BY is_primary DESC,created_at DESC",c);cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(ct);var x=new List<EmployeeBankItem>();while(await r.ReadAsync(ct))x.Add(new(){Id=r.GetGuid(0),BankName=r.GetString(1),AccountTitle=r.GetString(2),Iban=Unprotect(Bytes(r,3)),AccountNumber=Unprotect(Bytes(r,4)),IsPrimary=r.GetBoolean(5)});return x;}
    private static async Task<IReadOnlyList<EmployeeDocumentItem>> GetDocumentsAsync(NpgsqlConnection c,Guid id,CancellationToken ct){await using var cmd=new NpgsqlCommand("SELECT id,document_type,object_key,original_file_name,content_type,expires_on,created_at FROM hr.employee_document WHERE employee_id=@id ORDER BY created_at DESC",c);cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(ct);var x=new List<EmployeeDocumentItem>();while(await r.ReadAsync(ct))x.Add(new(){Id=r.GetGuid(0),DocumentType=r.GetString(1),ObjectKey=r.GetString(2),OriginalFileName=r.GetString(3),ContentType=r.GetString(4),ExpiresOn=r.IsDBNull(5)?null:r.GetFieldValue<DateOnly>(5),CreatedAt=r.GetFieldValue<DateTimeOffset>(6)});return x;}
    private static async Task<IReadOnlyList<EmployeeFingerprintItem>> GetFingerprintsAsync(NpgsqlConnection c,Guid id,CancellationToken ct){await using var cmd=new NpgsqlCommand("SELECT id,finger_type,template_format,fingerprint_image,is_active FROM hr.employee_fingerprint WHERE employee_id=@id ORDER BY finger_type",c);cmd.Parameters.AddWithValue("id",id);await using var r=await cmd.ExecuteReaderAsync(ct);var x=new List<EmployeeFingerprintItem>();while(await r.ReadAsync(ct))x.Add(new(){Id=r.GetGuid(0),FingerType=r.GetString(1),TemplateFormat=Text(r,2),FingerprintImage=Bytes(r,3),IsActive=r.GetBoolean(4)});return x;}

    private byte[] Protect(string value)=>Encoding.UTF8.GetBytes(protector.Protect(value));private string? Unprotect(byte[]? value){if(value is null)return null;try{return protector.Unprotect(Encoding.UTF8.GetString(value));}catch{return "Protected value";}}
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();private static string? NormalizeNationalId(string? value)=>string.IsNullOrWhiteSpace(value)?null:new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    private static void ValidateProfile(EmployeeEditorModel m){if(m.TerminationDate<m.HireDate)throw new InvalidOperationException("Termination date cannot be before hire date.");if(m.PreferredPaymentChannel=="bank"&&string.IsNullOrWhiteSpace(m.PreferredBankPaymentType))throw new InvalidOperationException("Select a bank payment type.");if(m.DateOfBirth>DateOnly.FromDateTime(DateTime.Today))throw new InvalidOperationException("Date of birth cannot be in the future.");}
    private static void ValidatePeriod(DateOnly from,DateOnly? to){if(to<from)throw new InvalidOperationException("Effective-to date cannot be before effective-from date.");}
    private static void AddText(NpgsqlCommand cmd,string name,string? value)=>cmd.Parameters.Add(name,NpgsqlDbType.Text).Value=Db(value);private static object Db(string? value)=>string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim();private static string? Text(NpgsqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);private static byte[]? Bytes(NpgsqlDataReader r,int i)=>r.IsDBNull(i)?null:r.GetFieldValue<byte[]>(i);
}
