using AuthService.BuildingBlocks.Common;
using AuthService.BuildingBlocks.Validators;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.BuildingBlocks.Middleware;
using AuthService.Models;
using AuthService.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Configure Database

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddDbContext<AuthDbContext>(options =>
     options.UseInMemoryDatabase("AuthDb"));

}
else
{
    builder.Services.AddDbContext<AuthDbContext>(options =>
     options.UseSqlServer(
         builder.Configuration.GetConnectionString("DefaultConnection"),
         sqlOptions => sqlOptions.EnableRetryOnFailure(
             maxRetryCount: 5,
             maxRetryDelay: TimeSpan.FromSeconds(30),
             errorNumbersToAdd: null
         )
     ));
}

//DbContext dbContext = builder.Services.BuildServiceProvider().GetRequiredService<AuthDbContext>();
//dbContext.Database.EnsureCreated();

// ================= LOGGING =================

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    // Replaced missing .WithThreadId() extension with a local custom enricher
    .Enrich.With<ThreadIdEnricher>()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        path: "logs/authservice-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();


// ================= JWT CONFIG =================

var jwtSettings = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT configuration missing");

if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
    throw new InvalidOperationException("JWT Secret not configured");

if (string.IsNullOrWhiteSpace(jwtSettings.Issuer))
    throw new InvalidOperationException("JWT Issuer not configured");

if (string.IsNullOrWhiteSpace(jwtSettings.Audience))
    throw new InvalidOperationException("JWT Audience not configured");

var jwtSecretKey = jwtSettings.Secret; // builder.Configuration["Jwt:Secret"];
var issuer = jwtSettings.Issuer; // builder.Configuration["Jwt:Issuer"];
var audience = jwtSettings.Audience; // builder.Configuration["Jwt:Audience"];

// Fix: replace an invalid standalone comparison expression with an explicit null/empty check.
// The original line `builder.Configuration[secret] == null;` produces CS0201 because it's a bare comparison.
// Here we validate the value read from the "Jwt:Secret" section and throw if missing.
if (string.IsNullOrEmpty(jwtSecretKey))
{
    throw new Exception("JWT Secret missing not configured in configuration (Jwt:Secret)");
}

// ================= AUTHENTICATION =================

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = false, // dev mode
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey =
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey))

    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<JwtService>>();
            logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<JwtService>>();
            var username = context.Principal?.Identity?.Name;
            logger.LogDebug("Token validated for user: {Username}", username);
            return Task.CompletedTask;
        }
    };
});



builder.Services.AddAuthorization();


// Add services to the container
// ================= CONTROLLERS =================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AuthService API",
        Version = "v1",
        Description = "Centralized Authentication Service for Smart Delivery System"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        //Type = SecuritySchemeType.ApiKey,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token"


    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "Bearer",
                    Type = ReferenceType.SecurityScheme

                }
            },
            Array.Empty<string>()
        }
    });
});

/*builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5001);
});*/

builder.Services.AddSwaggerGen(c =>
{
    c.AddServer(new OpenApiServer
    {
        Url = "/"
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// ================= APP =================


//Register FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<JwtService>();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<LoginDto>();
builder.Services.AddValidatorsFromAssemblyContaining<UpdateUserDto>();
builder.Services.AddValidatorsFromAssemblyContaining<UserValidator>();


// Register application services
builder.Services.AddScoped<IAuthService, AuthServiceImpl>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtService, JwtService>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});


var app = builder.Build();

app.UseCors();

// Health endpoint for Docker compose healthcheck
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

// ORDER IS IMPORTANT
app.UseAuthentication();
app.UseAuthorization();


// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    context.Database.EnsureCreated();
    Log.Information("AuthService database initialized");

    context.Database.EnsureDeleted();   // deletes all data
    context.Database.EnsureCreated();   // recreate schema
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService API v1");
        c.RoutePrefix = "swagger";
    });
}


// ================= AUTO DEV TOKEN by API=================

// 🔥 This creates a token automatically
app.MapGet("/dev/token", () =>
{
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: new[]
        {
            new Claim(ClaimTypes.Name, "dev-user"),
            new Claim(ClaimTypes.Role, "Admin")
        },
        expires: DateTime.UtcNow.AddYears(1),
        signingCredentials: creds
    );

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    Console.WriteLine("========== DEV Auto Generated JWT TOKEN ==========");
    Console.WriteLine($"Bearer {tokenString}");
    Console.WriteLine("===================================");

    return Results.Ok(new
    {
        token = tokenString,
        authorizationHeader = $"Bearer {tokenString}"
    });


});

// Configure JWT Authentication


//builder.WebHost.ConfigureKestrel(options =>
///{
//options.ListenAnyIP(80); // HTTP
//});


// DEBUGGING PIPELINE START
app.Use(async (context, next) =>
    {
        Console.WriteLine($"🔥 Request: {context.Request.Method} {context.Request.Path}");
        await next();
    });

    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    app.UseRouting();

    app.UseCors("AllowAll");

    //app.UseHttpsRedirection();

    app.UseAuthentication();   // MUST before Authorization
    app.UseAuthorization();

    app.MapControllers();

    try
    {
        Log.Information("Starting AuthService on {Url}", builder.Configuration["Urls"]);
        app.Run();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "AuthService terminated unexpectedly");
    }
    finally
    {
        Log.CloseAndFlush();
    }

// Local custom enricher to supply a per-log-event ThreadId property.
// This avoids the need to add the external Serilog.Enrichers.Thread package.
public class ThreadIdEnricher : ILogEventEnricher
{
    private const string PropertyName = "ThreadId";
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var threadId = Thread.CurrentThread.ManagedThreadId;
        var prop = propertyFactory.CreateProperty(PropertyName, threadId);
        logEvent.AddOrUpdateProperty(prop);
    }
}
