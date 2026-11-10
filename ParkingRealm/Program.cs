using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Booking;
using ParkingRealm.Data;
using ParkingRealm.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ReservationService>();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.AddAuthorization(options =>
    options.AddPolicy("Administrator", policy => policy.RequireRole(AdministratorProvisioning.RoleName)));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // This is an API: answer 401/403 instead of redirecting to a login page.
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddProblemDetails();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy("ReactDev", policy =>
            policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
}

var app = builder.Build();

if (args is ["--grant-admin", var email])
{
    using var scope = app.Services.CreateScope();
    var result = await AdministratorProvisioning.GrantAsync(scope.ServiceProvider, email);
    if (!result.Succeeded)
    {
        foreach (var error in result.Errors)
            Console.Error.WriteLine($"{error.Code}: {error.Description}");
        Environment.ExitCode = 1;
    }
    else Console.WriteLine("Administrator role provisioned. Sign in again to refresh the session.");
    return;
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseCors("ReactDev");
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Hello World!");
app.MapAuthEndpoints();
app.MapVehicleEndpoints();
app.MapParkingEndpoints();
app.MapReservationEndpoints();
app.MapBookingSecurityEndpoints();
app.MapBookingStatisticsEndpoints();

app.Run();

public partial class Program;
