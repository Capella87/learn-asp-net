using Microsoft.AspNetCore.Builder;
using ControllerWebAPI;
using System.Text.Json;
using Serilog;
using Serilog.Events;
using ControllerWebAPI.Controllers;
using ControllerWebAPI.Services;
using System.Diagnostics;
using Microsoft.Net.Http.Headers;
using ControllerWebAPI.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddEnvironmentVariables()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json",
        optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

try
{
    Log.Information("Starting the host...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Configuration.AddConfiguration(configuration);
    builder.Services.AddSerilog();

    // Routing configuration
    builder.Services.Configure<RouteOptions>(o =>
    {
        o.LowercaseUrls = true;
        o.AppendTrailingSlash = true;
        o.LowercaseQueryStrings = true;
    });

    // JSON configuration
    builder.Services.ConfigureHttpJsonOptions(o =>
    {
        o.SerializerOptions.AllowTrailingCommas = false;
        o.SerializerOptions.ReadCommentHandling = JsonCommentHandling.Skip;
        o.SerializerOptions.PropertyNameCaseInsensitive = true;
    });

    // Enable Scope Validation always (By default, it is only enabled in development)
    builder.Host.UseDefaultServiceProvider(o =>
    {
        o.ValidateScopes = true;
        o.ValidateOnBuild = true;
    });

    builder.Services.AddProblemDetails();
    builder.Services.AddAntiforgery();
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddHealthChecks();
    }


    // Add services to the container.
    builder.Services.AddScoped<GameService, GameService>();
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

    // AddControllers and MapControllers for MVC Web API.
    builder.Services.AddControllers();
    // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi(options =>
    {
        options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    });

    var app = builder.Build();
    app.UseRouting();
    app.UseSerilogRequestLogging((opts) =>
    {
        opts.MessageTemplate = "{Protocol} {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        opts.GetMessageTemplateProperties = (HttpContext httpContext, string requestPath, double elapsedMs, int statusCode) =>
        [
            new LogEventProperty("Protocol", new ScalarValue(httpContext.Request.Protocol)),
            new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
            new LogEventProperty("RequestPath", new ScalarValue(requestPath)),
            new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
            new LogEventProperty("Elapsed", new ScalarValue(elapsedMs)),
            new LogEventProperty("UserAgent", new ScalarValue(httpContext.Request.Headers[HeaderNames.UserAgent].ToString())),
            new LogEventProperty("ContentType", new ScalarValue(httpContext.Request.ContentType)),
        ];
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi("/openapi/v1api.json");
        app.MapScalarApiReference("openapi/scalar", config =>
        {
            config.OpenApiRoutePattern = "/openapi/v1api.json";
            config.Theme = ScalarTheme.BluePlanet;
            config.HideModels = true;
            config.HideDarkModeToggle = false;
        });
        app.UseDeveloperExceptionPage();
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAntiforgery();
    app.UseStatusCodePages();
    app.UseAuthorization();
    app.MapControllers();

    app.MapGet("/", () => "Hello World!");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException && ex.Source != "Microsoft.EntityFrameworkCore.Design")
{
    Log.Fatal(ex, "The WebApplication host terminated unexpectedly...");
}
catch (HostAbortedException ex) when (ex.Source != "Microsoft.EntityFrameworkCore.Design")
{
    Log.Fatal(ex, "The WebApplication host is aborted...");
}
finally
{
    Log.Information("Closing the logger...");
    Log.CloseAndFlush();
}
