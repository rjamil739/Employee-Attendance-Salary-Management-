using Employee_Attendance_Salary_Management.Components;
using Employee_Attendance_Salary_Management.Models;
using Employee_Attendance_Salary_Management.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using AppAuthenticationService = Employee_Attendance_Salary_Management.Services.AuthenticationService;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "hr_payroll_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
    });
builder.Services.AddAuthorization();
builder.Services.AddScoped<CompanyProfileService>();
builder.Services.AddScoped<AppAuthenticationService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<BranchService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<ContractorService>();
builder.Services.AddScoped<AccessControlService>();
builder.Services.AddScoped<EmployeeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/auth/login", async (HttpContext context, IAntiforgery antiforgery, AppAuthenticationService authenticationService) =>
{
    await antiforgery.ValidateRequestAsync(context);
    var form = await context.Request.ReadFormAsync();
    var userName = form["userName"].ToString().Trim();
    var password = form["password"].ToString();
    var portal = form["portal"].ToString().Equals("hr", StringComparison.OrdinalIgnoreCase) ? "hr" : "admin";
    var requiredRole = portal == "hr" ? "HR_MANAGER" : "ADMIN";

    var attempt = await authenticationService.ValidateAsync(userName, password, requiredRole);
    if (attempt.Status == AuthenticationStatus.AccessTerminated)
        return Results.LocalRedirect($"/login/{portal}?error=terminated");
    if (attempt.Status != AuthenticationStatus.Success || attempt.User is null)
        return Results.LocalRedirect($"/login/{portal}?error=invalid");
    var user = attempt.User;

    var isPersistent = form["rememberMe"].ToString().Equals("on", StringComparison.OrdinalIgnoreCase);
    await context.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        authenticationService.CreatePrincipal(user),
        new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(isPersistent ? 24 : 8)
        });

    return Results.LocalRedirect(portal == "hr" ? "/dashboard/hr" : "/dashboard/admin");
});

app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
