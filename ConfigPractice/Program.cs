using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.Sources.Clear();
builder.Configuration.AddJsonFile("sharedsettings.json", optional: true,
    reloadOnChange: true);
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

// User Secrets; We should use this provider method only in development.
// In production, environment variables or key vaults such as Azure Key Vault are strongly recommended.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}
else
{
    builder.Configuration.AddEnvironmentVariables();
}

// Bind a section to proper POCO option class.
builder.Services.Configure<AppDisplaySettings>(builder.Configuration.GetSection("AppDisplaySettings"));
// Registers the MapSettings object in DI by delegating to the IOptions registration
builder.Services.AddSingleton(provider => provider.GetRequiredService<IOptions<AppDisplaySettings>>().Value);

// Bind strongly typed settings without IOptions
var settings = new MapSettings();
// Almost the same above, but it is getting the section and binds MapSettings to the settings object.
builder.Configuration.GetSection("MapSettings").Bind(settings);
// Register 
builder.Services.AddSingleton(settings);

// Routing configuration
builder.Services.Configure<RouteOptions>(o =>
{
    o.LowercaseUrls = true;
    o.AppendTrailingSlash = true;
    o.LowercaseQueryStrings = false;
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
// It is safe to use only dependencies that have longer than or equal to the service's lifetime.

builder.Services.AddAntiforgery();
builder.Services.AddHttpLogging(opts =>
   opts.LoggingFields = HttpLoggingFields.RequestProperties);
builder.Logging.AddFilter("Microsoft.AspNetCore.HttpLogging", LogLevel.Debug);
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseHttpLogging();
app.UseAntiforgery();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
}
app.UseHsts();

app.MapRazorPages();

app.MapGet("/", () => 
@"Let's learn configuration and generic host!");
// Configurations are registered in the DI container
app.MapGet("/config", (IConfiguration config) => config.AsEnumerable());

/*
app.MapGet("/display-settings", (IOptionsSnapshot<AppDisplaySettings> options) =>
{

    AppDisplaySettings settings = options.Value;

    return new { title = settings.Title, showCopyright = settings.ShowCopyright };
});
*/
// Without using IOptions at routing
app.MapGet("/no-ioptions/mapsettings", (MapSettings mapSettings) => mapSettings);
app.MapGet("/no-ioptions/appdisplaysettings", (AppDisplaySettings appDisplaySettings) => appDisplaySettings);

app.Run();

public class AppDisplaySettings
{
    public string Title { get; set; }
    public bool ShowCopyright { get; set; }
}

public class MapSettings
{
    public int DefaultZoomLevel { get; set; }
    // Remember that we must match subclass's name to the JSON property name.
    public DefaultLocationInfo DefaultLocation { get; set; } = new DefaultLocationInfo();

    public class DefaultLocationInfo
    {
        public string Latitude { get; set; }
        public string Longitude { get; set; }
    }
}
