using System.Text;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Caching.Memory;
using Infrastructure;
using Data;
using Data.Seeding;
using Models;
using Dtos;

Env.Load(); // load .env into environment variables (project root .env)

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=logitrack.db";

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

    // Require Bearer auth for all operations
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

    // Enable annotations
    c.EnableAnnotations();
});

builder.Services.AddTransient<ISeeder, RoleAndUserSeeder>();
builder.Services.AddSingleton<Services.Mappers.IInventoryMapper, Services.Mappers.InventoryMapper>();
builder.Services.AddSingleton<Services.Mappers.IOrderMapper, Services.Mappers.OrderMapper>();

var app = builder.Build();

// Ensure database schema is applied and optionally rehydrate caches on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Apply any pending EF Core migrations so the DB schema is up-to-date
    try
    {
        var db = services.GetRequiredService<LogiTrackContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Database migration failed: {ex.Message}");
        throw;
    }

    // Rehydrate critical in-memory caches from the durable store
    try
    {
        var cache = services.GetService<IMemoryCache>();
        if (cache != null)
        {
            // Rehydrate inventory list cache
            const string inventoryListVersionKey = "Inventory:List:Version";
            const string inventoryListCacheKeyFormat = "Inventory:List:v={0}";
            var inventoryVersion = Guid.NewGuid().ToString();
            cache.Set(inventoryListVersionKey, inventoryVersion, TimeSpan.FromDays(1));
            var invCacheKey = string.Format(inventoryListCacheKeyFormat, inventoryVersion);

            var db = services.GetRequiredService<LogiTrackContext>();
            var inventoryItems = await db.InventoryItems
                .AsNoTracking()
                .OrderBy(i => i.ItemId)
                .Select(i => new InventoryItemDto
                {
                    ItemId = i.ItemId,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Location = i.Location
                })
                .ToListAsync();

            cache.Set(invCacheKey, inventoryItems, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) // longer-lived warm cache
            });

            // Rehydrate orders list cache
            const string ordersListVersionKey = "Orders:List:Version";
            const string ordersListCacheKeyFormat = "Orders:List:v={0}";
            var ordersVersion = Guid.NewGuid().ToString();
            cache.Set(ordersListVersionKey, ordersVersion, TimeSpan.FromDays(1));
            var ordersCacheKey = string.Format(ordersListCacheKeyFormat, ordersVersion);

            var orders = await db.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .OrderByDescending(o => o.DatePlaced)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    CustomerName = o.CustomerName,
                    DatePlaced = o.DatePlaced,
                    Items = o.Items.Select(ii => new InventoryItemDto
                    {
                        ItemId = ii.ItemId,
                        Name = ii.Name,
                        Quantity = ii.Quantity,
                        Location = ii.Location
                    }).ToList()
                })
                .ToListAsync();

            cache.Set(ordersCacheKey, orders, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6)
            });
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Cache rehydration failed: {ex.Message}");
    }
}

// Seed DB (roles/users etc.)
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
