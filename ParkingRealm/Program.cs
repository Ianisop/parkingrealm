using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ParkingRealm.Data;
using ParkingRealm.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddProblemDetails();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
        options.AddPolicy("ReactDev", policy =>
            policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseCors("ReactDev");
}

app.MapGet("/", () => "Hello World!");
app.MapAuthEndpoints();

app.Run();

public partial class Program;
