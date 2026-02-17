using AuthService.Data;
using AuthService.DTOs;
using AuthService.Middleware;
using AuthService.Services;
using AuthService.Validation;
using AuthService.Validators;
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

DbContext dbContext = builder.Services.BuildServiceProvider().GetRequiredService<AuthDbContext>();
dbContext.Database.EnsureCreated();

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

// Add services to the container
// ================= CONTROLLERS =================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();

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
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});


// ================= JWT AUTHENTICATION =================
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
    throw new Exception("JWT Secret missing in configuration");


// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"] ?? 
                    throw new InvalidOperationException("JWT Secret not configured"))
            )
        };
        
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                logger.LogWarning("Authentication failed: {Error}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();
                var username = context.Principal?.Identity?.Name;
                logger.LogDebug("Token validated for user: {Username}", username);
                return Task.CompletedTask;
            }
        };
    });

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80); // HTTP
});


builder.Services.AddAuthorization();

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

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    context.Database.EnsureCreated();
    Log.Information("AuthService database initialized");
}

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService API v1");
        c.RoutePrefix = "swagger";
    });
}

// DEBUGGING PIPELINE START
app.Use(async (context, next) =>
{
    Console.WriteLine($"🔥 Request: {context.Request.Method} {context.Request.Path}");
    await next();
});


app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();

// Global exception middleware
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

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
