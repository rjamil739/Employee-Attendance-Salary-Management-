namespace Employee_Attendance_Salary_Management.Models
{
    public static class StaticConnection
    {
        //public static string conn = "Host=127.0.0.1;Port=5433;Database=AttendanceSalaryManagement;Username=postgres;Password=123456";

        public static readonly string conn = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = "213.199.35.5",
            Port = 5432,
            Database = "AttendanceSalaryManagement ",
            Username = "mery_user_ka_name",
            Password = "ranajamil123A",
            SslMode = Npgsql.SslMode.Disable,
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 50,
            Timeout = 10,
            CommandTimeout = 30,
            KeepAlive = 30
        }.ConnectionString;

    }
}
