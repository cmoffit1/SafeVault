using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen with HTTPS on 5161 so the client is served over TLS in dev.
builder.WebHost.ConfigureKestrel(options =>
{
    try
    {
        options.ListenLocalhost(5161, listenOptions => listenOptions.UseHttps());
    }
    catch (Exception ex)
    {
        // If HTTPS binding fails (missing cert), fall back to HTTP and log a warning.
        Console.WriteLine($"Warning: HTTPS binding on 5161 failed: {ex.Message}. Falling back to HTTP.");
        options.ListenLocalhost(5161);
    }
});

// Use a simple, minimal host to serve static files.
var app = builder.Build();

// Serve the built Blazor WebAssembly static files from the Client build output.
// This path is relative to the repo root and points to the standard SDK output folder.
// Use the project's source wwwroot for development so index.html and other top-level
// assets are available without requiring a publish step.
// Prefer a published output (Client/publish) when available because the publish step
// produces a processed index.html with correct script references. Fall back to
// the source `Client/wwwroot` for quick development scenarios.
var publishRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Client", "publish", "wwwroot"));
var sourceWwwRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Client", "wwwroot"));
string clientWwwRoot;
if (Directory.Exists(publishRoot))
{
    clientWwwRoot = publishRoot;
    Console.WriteLine($"Serving client files from publish folder: {clientWwwRoot}");
}
else if (Directory.Exists(sourceWwwRoot))
{
    clientWwwRoot = sourceWwwRoot;
    Console.WriteLine($"Serving client files from source wwwroot: {clientWwwRoot}");
}
else
{
    Console.WriteLine($"No client assets found. Build or publish the Client project first. Checked: {publishRoot} and {sourceWwwRoot}");
    return;
}
// Add a tiny middleware to rewrite some Blazor framework requests that omit
// the .js extension (the Blazor loader sometimes requests '/_framework/blazor.webassembly')
// to their actual file names present in the publish output (for static hosting).
// Explicitly map the common Blazor framework endpoints that omit extensions
// so static hosting can serve them from the publish output.
app.Map("/_framework/blazor.webassembly", builder =>
{
    builder.Run(async ctx =>
    {
        var file = Path.Combine(clientWwwRoot, "_framework", "blazor.webassembly.js");
        if (System.IO.File.Exists(file))
        {
            ctx.Response.ContentType = "application/javascript";
            await ctx.Response.SendFileAsync(file);
            return;
        }
        ctx.Response.StatusCode = 404;
    });
});

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    if (path.StartsWith("/_framework/", StringComparison.OrdinalIgnoreCase) && !Path.HasExtension(path))
    {
        // Try adding .js for framework entries if the file exists.
        var candidate = Path.Combine(clientWwwRoot, path.TrimStart('/')) + ".js";
        var exists = System.IO.File.Exists(candidate);
        Console.WriteLine($"ClientHost: framework request {path}, candidate={candidate}, exists={exists}");
        if (exists)
        {
            context.Request.Path = new Microsoft.AspNetCore.Http.PathString(path + ".js");
            Console.WriteLine($"ClientHost: rewritten {path} -> {path}.js");
        }
    }
    await next();
});

// Use a FileServer configured with the chosen PhysicalFileProvider. This ensures
// default files (index.html) and static files are served from the same provider.
var fileProvider = new PhysicalFileProvider(clientWwwRoot);
var fileServerOptions = new FileServerOptions
{
    FileProvider = fileProvider,
    EnableDefaultFiles = true,
    EnableDirectoryBrowsing = false,
};
// Ensure index.html is the default document
fileServerOptions.DefaultFilesOptions.DefaultFileNames.Clear();
fileServerOptions.DefaultFilesOptions.DefaultFileNames.Add("index.html");
app.UseFileServer(fileServerOptions);

Console.WriteLine("ClientHost Kestrel configured (prefers HTTPS on port 5161; fell back to HTTP if TLS unavailable).");
// Let Kestrel bindings configured via ConfigureKestrel take effect. Use app.Run()
// instead of supplying a URL here to avoid conflicts between ListenLocalhost and
// server address overrides.
app.Run();
