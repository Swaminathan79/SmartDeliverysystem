using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddAuthentication();

builder.Services.AddRateLimiter();

builder.Services.AddHttpClient(); // REQUIRED for IHttpClientFactory

builder.Logging.AddConsole();

// Add reverse proxy
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger
builder.Services.AddSwaggerGen();
/*builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "API Gateway",
        Version = "v1"
    }); 
});*/

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("https://localhost:5001/swagger/v1/swagger.json", "Auth Service");
    c.SwaggerEndpoint("https://localhost:5002/swagger/v1/swagger.json", "Package Service");
    c.SwaggerEndpoint("https://localhost:5003/swagger/v1/swagger.json", "Route Service");
});



// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
   //app.MapOpenApi();
}



app.MapReverseProxy();

app.UseHttpsRedirection();

var logger = app.Logger;

// Dynamic service mapping from appsettings.json
var services = builder.Configuration
    .GetSection("Services")
    .Get<Dictionary<string, string>>() ?? new();

app.Map("/{service}/{**path}", async (
    string service,
    string path,
    HttpContext context,
    IHttpClientFactory httpClientFactory) =>
{
   /* string? baseUrl = service.ToLower() switch
    {
        "auth" => "https://localhost:5001",
        "package" => "https://localhost:5002",
        "route" => "https://localhost:5003",
        _ => null
    };*/


    try
    {
        if (!services.TryGetValue(service.ToLower(), out var baseUrl))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync("Service not found");
            return;
        }

        var targetUrl = $"{baseUrl}/{path}{context.Request.QueryString}";

        logger.LogInformation("Proxying request to {TargetUrl}", targetUrl);

        var requestMessage = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            targetUrl);

        // Forward body
        if (context.Request.ContentLength > 0)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        // Forward headers
        foreach (var header in context.Request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        var httpClient = httpClientFactory.CreateClient();

        var responseMessage = await httpClient.SendAsync(
            requestMessage,
            HttpCompletionOption.ResponseHeadersRead);

        context.Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
            context.Response.Headers[header.Key] = header.Value.ToArray();

        await responseMessage.Content.CopyToAsync(context.Response.Body);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Gateway error");

        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Gateway internal error");
    }
});

app.Run();
