using System.Text;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PackageService.Data;
using PackageService.Repositories;
using PackageService.Services;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<PackageDbContext>(options =>
     options.UseInMemoryDatabase("PackageDb"));
// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
   // .Enrich.WithMachineName()
   // .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
    )
    .WriteTo.File(
        path: "logs/packageservice-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PackageService API",
        Version = "v1",
        Description = "Package Tracking and Management Service"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

});

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80); // HTTP
});

// Configure Database
builder.Services.AddDbContext<PackageDbContext>(options =>
        options.UseInMemoryDatabase("PackageDb"));

    // Configure HttpClient for RouteService
    builder.Services.AddHttpClient("RouteService", client =>
    {
        client.BaseAddress = new Uri(
            builder.Configuration["RouteService:BaseUrl"] ?? "http://localhost:5002"
        );
        client.DefaultRequestHeaders.Add("Accept", "application/json");
    });

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

    builder.Services.AddAuthorization();

    // Register application services
    builder.Services.AddScoped<IPackageRepository, PackageRepository>();
    builder.Services.AddScoped<IPackageService, PackageServiceImpl>();
    builder.Services.AddScoped<IRouteValidationService, RouteValidationService>();

    var app = builder.Build();

    // Initialize database
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<PackageDbContext>();
        context.Database.EnsureCreated();
        Log.Information("PackageService database initialized");
    }

    // Configure middleware
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "PackageService API v1");
            c.RoutePrefix = "swagger";
        });
    }

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    try
    {
        Log.Information("Starting PackageService on {Url}", builder.Configuration["Urls"]);
        app.Run();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "PackageService terminated unexpectedly");
    }
    finally
    {
        Log.CloseAndFlush();
    }
