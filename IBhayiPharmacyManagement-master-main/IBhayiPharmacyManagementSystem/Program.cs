using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

// Enable Npgsql legacy timestamp behavior (DateTime.Now mapped to timestamp without time zone)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
 
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

// Add session configuration for auto-logout
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // 30 minutes session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddIdentity<Users, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;
})
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Register email service
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<SmtpEmailSender>>();
    return new SmtpEmailSender(configuration, logger);
});

// Register PDF import service
builder.Services.AddScoped<PdfImportService>();

// Register XLSX import service
builder.Services.AddScoped<XlsxImportService>();

// Register customer activity service
builder.Services.AddScoped<ICustomerActivityService, CustomerActivityService>();
builder.Services.AddHttpContextAccessor();

// Configure authentication cookie options for auto-logout
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

// Configure path base from environment variable for IIS subdirectory deployment
var pathBase = app.Configuration["ASPNETCORE_PATHBASE"];
if (!string.IsNullOrEmpty(pathBase))
{
    app.UsePathBase(pathBase);
}

await SeedService.SeedDatabase(app.Services);

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

// Add session middleware
app.UseSession();

app.UseAuthentication();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
