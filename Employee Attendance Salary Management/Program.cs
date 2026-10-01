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
        options.Events.OnValidatePrincipal = async context =>
        {
            // Do not open the remote database for every css/js/image request.
            // Revalidate active access periodically; login itself is still checked immediately.
            var path = context.HttpContext.Request.Path;
            if (path.StartsWithSegments("/_framework") || path.StartsWithSegments("/_content") ||
                path.StartsWithSegments("/css") || path.StartsWithSegments("/js") ||
                path.Value?.EndsWith(".css", StringComparison.OrdinalIgnoreCase) == true ||
                path.Value?.EndsWith(".js", StringComparison.OrdinalIgnoreCase) == true ||
                path.Value?.EndsWith(".map", StringComparison.OrdinalIgnoreCase) == true ||
                path.Value?.EndsWith(".ico", StringComparison.OrdinalIgnoreCase) == true)
                return;

            if (context.Properties.IssuedUtc is { } issued &&
                DateTimeOffset.UtcNow - issued < TimeSpan.FromMinutes(2))
                return;

            var principal = context.Principal;
            var idValue = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idValue, out var userId))
            {
                context.RejectPrincipal();
                return;
            }

            var authenticationService = context.HttpContext.RequestServices
                .GetRequiredService<AppAuthenticationService>();
            if (await authenticationService.IsActiveAsync(userId, context.HttpContext.RequestAborted))
            {
                context.Properties.IssuedUtc = DateTimeOffset.UtcNow;
                context.ShouldRenew = true;
                return;
            }

            var portal = principal?.IsInRole("HR_MANAGER") == true && principal.IsInRole("ADMIN") == false
                ? "hr"
                : "admin";
            context.HttpContext.Items["terminated_portal"] = portal;
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        };
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.HttpContext.Items.TryGetValue("terminated_portal", out var portal))
                context.Response.Redirect($"/login/{portal}?error=terminated");
            else
                context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    // A dedicated name prevents localhost apps from trying to decrypt each
    // other's antiforgery cookies after a restart or port change.
    options.Cookie.Name = "hr_payroll_antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddScoped<CompanyProfileService>();
builder.Services.AddScoped<AppAuthenticationService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<UserAccountService>();
builder.Services.AddScoped<BranchService>();
builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<ContractorService>();
builder.Services.AddScoped<EmployeeService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<PayrollService>();

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
    var form = await context.Request.ReadFormAsync();
    var portal = form["portal"].ToString().Equals("hr", StringComparison.OrdinalIgnoreCase) ? "hr" : "admin";
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        return Results.LocalRedirect($"/login/{portal}?error=session");
    }

    var userName = form["userName"].ToString().Trim();
    var password = form["password"].ToString();
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
    try
    {
        await antiforgery.ValidateRequestAsync(context);
    }
    catch (AntiforgeryValidationException)
    {
        // The session may still be valid, but the page contains a stale form
        // token. A fresh navigation issues a matching token without crashing.
        return Results.LocalRedirect("/");
    }
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
