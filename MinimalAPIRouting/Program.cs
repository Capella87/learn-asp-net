using Microsoft.AspNetCore.HttpLogging;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Components.Web;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, options) =>
{
    options.ListenAnyIP(2010, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2AndHttp3;
        listenOptions.UseHttps();
    });
});

// Services

builder.Services.AddAntiforgery();

// Use development environment logging; Not to be used in production
builder.Services.AddHttpLogging(opts =>
    opts.LoggingFields = HttpLoggingFields.RequestProperties);
builder.Logging.AddFilter("Microsoft.AspNetCore.HttpLogging", LogLevel.Debug);
builder.Services.AddProblemDetails();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseHsts();
app.UseHttpsRedirection();
app.UseHttpLogging();
app.UseAntiforgery();

app.UseStatusCodePages();
app.UseExceptionHandler();

// Routing

// We can use WithName for reference
app.MapGet("/", () => "Hello 🌏")
    .WithName("hello");

// Endpoint names are case-sensitive, but route templates (URL) are NOT case-sensitive.
app.MapGet("/product/{name}", (string name) => $"The product is {name}")
            .WithName("product"); // Add name metadata
// LinkGenerator can create an URL to endpoint.
app.MapGet("/links", (LinkGenerator links) =>
{
    // Create a link dynamically (in runtime)
    string link = links.GetPathByName("product",
        new { name = "big-widget" });

    return $"View the project at {link}";
});

// But in ASP.NET Core Razor, redirection to generated link is more widely used..
// Results.RedirectToRoute returns 302 Found response code in default,
// But we can permanent and preserveMethod parameters to change response code.
app.MapGet("/redirect", () => Results.RedirectToRoute("hello"));

app.Run();
