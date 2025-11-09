using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Infrastructure;
using Data;
using Data.Seeding;
using Models;

Env.Load(); // load .env into environment variables (project root .env); do this before reading config

var builder = WebApplication.CreateBuilder(args);

// Configuration / connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=logitrack.db";

// Add DbContext BEFORE Identity
builder.Services.AddDbContext<LogiTrackContext>(options =>
    options.UseSqlite(connectionString));

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<LogiTrackContext>()
    .AddDefaultTokenProviders();

// JWT configuration
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LogiTrack";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LogiTrackClients";

if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("JWT Key is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});

// Register memory cache, controllers, swagger, etc.
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "LogiTrack API", Version = "v1" });

    // Define the Bearer auth scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT as: Bearer {token}"
    });

    // Require Bearer auth for all operations (you can scope this per-action with attributes instead)
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });
});

builder.Services.AddTransient<ISeeder, RoleAndUserSeeder>();
builder.Services.AddSingleton<Services.Mappers.IInventoryMapper, Services.Mappers.InventoryMapper>();
builder.Services.AddSingleton<Services.Mappers.IOrderMapper, Services.Mappers.OrderMapper>();

var app = builder.Build();

await app.SeedDatabaseAsync();


// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
