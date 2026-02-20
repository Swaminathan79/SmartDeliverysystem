using AuthService.BuildingBlocks.Common;
using AuthService.BuildingBlocks.Infrastructure;
using AuthService.BuildingBlocks.Middleware;
using AuthService.BuildingBlocks.Validators;
using AuthService.Data;
using AuthService.DTOs;
using AuthService.Models;
using AuthService.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Win32;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

Log.Information("Starting AuthService on {Url}", builder.Configuration["Urls"]);

try
{

        //////////////////////////////////////////////////////////////
        // 1️⃣ LOGGING (FIRST)
        //////////////////////////////////////////////////////////////

        Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
        .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
        .MinimumLevel.Override("System", LogEventLevel.Warning)
        .Enrich.FromLogContext()
        .Enrich.With<ThreadIdEnricher>()
        .WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
        )
        .WriteTo.File(
            path: "logs/authservice-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30
        )
        .CreateLogger();

    builder.Host.UseSerilog();

    Log.Information("Starting AuthService...");



    //////////////////////////////////////////////////////////////
    // 2️⃣ DATABASE 
    //////////////////////////////////////////////////////////////

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
                sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)
            ));
    }

    //////////////////////////////////////////////////////////////
    // 3️⃣ JWT CONFIGURATION
    //////////////////////////////////////////////////////////////

    var jwtSettings = builder.Configuration
        .GetSection("Jwt")
        .Get<JwtSettings>()
        ?? throw new InvalidOperationException("JWT configuration missing");

    if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
        throw new Exception("JWT Secret missing");

    var jwtSecretKey = jwtSettings.Secret;
    var issuer = jwtSettings.Issuer;
    var audience = jwtSettings.Audience;

    //////////////////////////////////////////////////////////////
    // 4️⃣ AUTHENTICATION
    //////////////////////////////////////////////////////////////

    builder.Services
        .AddAuthentication(options =>
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
                ValidIssuer = issuer,
                ValidAudience = audience,
                ClockSkew = TimeSpan.Zero,
                IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                RoleClaimType = ClaimTypes.Role
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
                },

                OnChallenge = context => //Unauthorized Exception
                {
                    context.HandleResponse(); // suppress default behavior

                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";

                    var result = JsonSerializer.Serialize(new ErrorResponse
                    {
                        Error = "Unauthorized",
                        Message = "Bearer token is missing or invalid.",
                        StatusCode = 401,
                        Timestamp = DateTime.UtcNow,
                        Path = context.Request.Path,
                        Method = context.Request.Method,
                        TraceId = context.HttpContext.TraceIdentifier
                    });

                    return context.Response.WriteAsync(result);
                },

                OnForbidden = context => //Handle 403 Forbidden
                {
                    context.Response.StatusCode = 403;
                    context.Response.ContentType = "application/json";

                    var result = JsonSerializer.Serialize(new ErrorResponse
                    {
                        Error = "Forbidden",
                        Message = "You do not have permission to access this resource.",
                        StatusCode = 403,
                        Timestamp = DateTime.UtcNow,
                        Path = context.Request.Path,
                        Method = context.Request.Method,
                        TraceId = context.HttpContext.TraceIdentifier
                    });

                    return context.Response.WriteAsync(result);
                }

            };

        });

    builder.Services.AddAuthorization();

    //////////////////////////////////////////////////////////////
    // 5️⃣ CONTROLLERS + JSON
    //////////////////////////////////////////////////////////////

    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter());
        });

    builder.Services.AddEndpointsApiExplorer();

    //////////////////////////////////////////////////////////////
    // 6️⃣ CORS (REGISTER ONCE ONLY)
    //////////////////////////////////////////////////////////////

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });

    //////////////////////////////////////////////////////////////
    // 7️⃣ SWAGGER (REGISTER ONCE ONLY)
    //////////////////////////////////////////////////////////////

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "AuthService API",
            Version = "v1",
            Description = "Centralized Authentication Service"
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description = "Enter 'Bearer' [space] and your token"
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

    //////////////////////////////////////////////////////////////
    // 8️⃣ FLUENT VALIDATION
    //////////////////////////////////////////////////////////////

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<RegisterDtoValidator>();

    //////////////////////////////////////////////////////////////
    // 9️⃣ APPLICATION SERVICES
    //////////////////////////////////////////////////////////////

    builder.Services.AddScoped<IAuthService, AuthServiceImpl>();
    builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
    builder.Services.AddScoped<IJwtService, JwtService>();

    //////////////////////////////////////////////////////////////
    // 🔟 KESTREL
    //////////////////////////////////////////////////////////////

    builder.WebHost.ConfigureKestrel(options =>
    {
        options.ListenAnyIP(5081);
    });

    var app = builder.Build();

    // ======================================================
    // 1️⃣ GLOBAL MIDDLEWARE (ORDER MATTERS)
    // ======================================================

    // Logging first
    app.UseSerilogRequestLogging();

    //////////////////////////////////////////////////////////////
    // MIDDLEWARE PIPELINE
    //////////////////////////////////////////////////////////////

    // Global exception handler (must be early)
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();


    //REGISTER SERVICES(Database, Auth, etc.)

    // ======================================================
    // 2️⃣ RESET DATABASE (ONLY DEV)
    // ======================================================
    if (app.Environment.IsDevelopment())
    {
        await DatabaseInitializer.ClearUsersAsync(app.Services);
        await DatabaseInitializer.ResetDatabaseAsync(app.Services);
    }


    
    // Developer exception page (dev only)
    if (app.Environment.IsDevelopment())
    {
       // app.UseDeveloperExceptionPage();
    }

    
    // HTTPS redirection
    app.UseHttpsRedirection();

    // Routing
    app.UseRouting();

    // CORS
    app.UseCors("AllowAll");

    // Authentication & Authorization
    app.UseAuthentication();
    app.UseAuthorization();


    // ======================================================
    // 3️⃣ ENDPOINT MAPPING
    // ======================================================

    app.MapControllers();

    // Health endpoint
    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }));

    // Dev token endpoint
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

    return Results.Ok(new
    {
        token = tokenString,
        authorizationHeader = $"Bearer {tokenString}"
        });
    });


    // ======================================================
    // 4️⃣ SWAGGER (LAST MIDDLEWARE BEFORE RUN)
    // ======================================================
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AuthService API v1");
        c.RoutePrefix = "swagger";
    });


    // ======================================================
    // 5️⃣ RUN APPLICATION (ALWAYS LAST)
    // ======================================================
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