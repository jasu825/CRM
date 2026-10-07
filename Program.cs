using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Support optional local override files (ignored by git) in addition to User Secrets
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

// 1. Configure EF Core to use SQL Server (resolves from User Secrets, appsettings, or Environment Variables)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found. Configure via User Secrets in development or Environment Variables in production.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 2. Configure ASP.NET Core Identity with Security Policies
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password policy per specification (Min 8 chars, uppercase, lowercase, digit, special char)
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;

    // Account lockout policy per specification (Lock after 5 failed attempts for 15 minutes)
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Application Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<ILeadService, LeadService>();
builder.Services.AddScoped<IActivityService, ActivityService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Health verification endpoint for SQL Server connectivity
app.MapGet("/test-db", async (ApplicationDbContext db) =>
{
    try
    {
        bool canConnect = await db.Database.CanConnectAsync();
        return Results.Ok(new
        {
            Status = "Connected",
            CanConnect = canConnect,
            Database = db.Database.GetDbConnection().Database,
            DataSource = db.Database.GetDbConnection().DataSource
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database Connection Failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

// Automatically seed Roles (Admin, Manager, SalesExecutive) and initial Development Admin account
try
{
    await DbInitializer.SeedRolesAndAdminAsync(app.Services);
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "DbInitializer was unable to complete seeding. Ensure SQL Server is running and migrations are applied.");
}

app.Run();
