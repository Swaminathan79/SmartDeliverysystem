using ApiGateway.Models;
using ApiGateway.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Tokens;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace ApiGateway
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Debug);

            builder.Services.AddHttpClient();



            // =============================
            // REVERSE PROXY (YARP)
            // =============================

            builder.Services
            .AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .ConfigureHttpClient((context, handler) =>
            {
                handler.SslOptions.RemoteCertificateValidationCallback =
                    (_, _, _, _) => true;
            });


            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();



            // =============================
            // LOAD SERVICE MAPPING
            // =============================

            //var services = builder.Configuration
            //  .GetSection("Services")
            //.Get<Dictionary<string, string>>() ?? new(StringComparer.OrdinalIgnoreCase);

            var services = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var clusters = builder.Configuration
                .GetSection("ReverseProxy:Clusters")
                .GetChildren();

            foreach (var cluster in clusters)
            {
                var destinations = cluster
                    .GetSection("Destinations")
                    .GetChildren();

                // Prefer HTTPS destination if exists
                var httpsDest = destinations
                    .FirstOrDefault(d => d.Key.Contains("https", StringComparison.OrdinalIgnoreCase));

                var selectedDest = httpsDest ?? destinations.FirstOrDefault();

                var address = selectedDest?.GetValue<string>("Address");

                if (!string.IsNullOrEmpty(address))
                {
                    var serviceName = cluster.Key
                        .Replace("Cluster", "", StringComparison.OrdinalIgnoreCase)
                        .ToLower();

                    services[serviceName] = address.TrimEnd('/');
                }
            }

            var swaggerTargets = services.Keys
                .Select(x => x.ToLowerInvariant())
                .OrderBy(x => x)    // ADD THIS
                .Distinct()
                .ToList();

            


            // =============================
            // JWT AUTH
            // =============================

            /*var jwt = builder.Configuration.GetSection("Jwt");

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt["Issuer"],
                    ValidAudience = jwt["Audience"],
                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwt["Secret"]))
                };
            });
            */

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


            // =============================
            // RATE LIMITING
            // =============================

            builder.Services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("api", opt =>
                {
                    opt.PermitLimit = 50;
                    opt.Window = TimeSpan.FromSeconds(10);
                    opt.QueueLimit = 2;
                });
            });


            // NOTE: Requires NuGet package: Microsoft.Extensions.Http.Polly
            builder.Services.AddHttpClient("proxy")
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(3, _ => TimeSpan.FromSeconds(2)))
                .AddPolicyHandler(HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));


            builder.Services.AddHealthChecks();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod());
            });

            

            var app = builder.Build();

            app.UseCors();

            // ================= GLOBAL EXCEPTION PIPELINE =================

            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Gateway internal error");
                });
            });

            // =============================
            // CORRELATION ID
            // =============================

            app.Use(async (context, next) =>
            {
                var correlationId = Guid.NewGuid().ToString();

                context.Request.Headers["X-Correlation-ID"] = correlationId;
                context.Response.Headers["X-Correlation-ID"] = correlationId;

                app.Logger.LogInformation(
                    "Request {CorrelationId} {Path}",
                    correlationId,
                    context.Request.Path);

                await next();
            });

            var logger = app.Logger;

            // =============================
            // MIDDLEWARE ORDER (VERY IMPORTANT)
            // =============================

            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();

            // =============================
            // REVERSE PROXY (ONLY ONCE !!)
            // =============================

            //  app.MapReverseProxy()
            //.RequireRateLimiting("api");

            //app.MapReverseProxy().RequireRateLimiting("api");

            // =============================
            // SWAGGER UI
            // =============================


            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                /*foreach (var key in swaggerTargets)
                {
                    var name = char.ToUpper(key[0]) + key.Substring(1);

                    var apiGatewayPath = $"/swagger/v1/swagger.json";

                    c.SwaggerEndpoint(apiGatewayPath, $"{name.ToUpper()} Service");


                }*/

                foreach (var service in services)
                {
                    var name = char.ToUpper(service.Key[0]) + service.Key.Substring(1);

                    c.SwaggerEndpoint($"{service.Value}/swagger/v1/swagger.json", $"{name} Service");

                    c.RoutePrefix = "swagger";
                }

            });

            app.MapGet("{service.Value}/swagger-json/{service}", async (
    string service,
    HttpContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<Program> logger) =>
            {
                try
                {
                    if (!services.TryGetValue(service.ToLower(), out var targetBase))
                    {
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsync("Service not found");
                        return;
                    }

                    var httpClient = httpClientFactory.CreateClient();

                    var swaggerUrl = $"{targetBase}/swagger/v1/swagger.json";

                    logger.LogInformation("Fetching swagger from {SwaggerUrl}", swaggerUrl);

                    using var response = await httpClient.GetAsync(swaggerUrl);

                    context.Response.StatusCode = (int)response.StatusCode;

                    foreach (var header in response.Content.Headers)
                        context.Response.Headers[header.Key] = header.Value.ToArray();

                    await response.Content.CopyToAsync(context.Response.Body);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error proxying swagger for {Service}", service);
                    context.Response.StatusCode = 500;
                    await context.Response.WriteAsync("Error fetching swagger");
                }
            });

            app.UseRouting();
            /* app.UseSwaggerUI(c =>
             {
                 c.RoutePrefix = "swagger";

                 c.SwaggerEndpoint("/auth/swagger/v1/swagger.json", "Auth Service");
                 c.SwaggerEndpoint("/package/swagger/v1/swagger.json", "Package Service");
                 c.SwaggerEndpoint("/route/swagger/v1/swagger.json", "Route Service");
             });
            */
            app.UseHttpsRedirection();

           

            // =============================
            // HEALTH CHECK
            // =============================

            app.MapHealthChecks("/health");


           
            await app.RunAsync();
        }
    }
}
