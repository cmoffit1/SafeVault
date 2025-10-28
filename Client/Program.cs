using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Point HttpClient to the API backend (configurable via wwwroot/appsettings*.json)
// Use configuration key "ApiBaseUrl" or fall back to the legacy hard-coded address.
var apiBase = builder.Configuration["ApiBaseUrl"];
// If ApiBaseUrl isn't set, prefer same-origin base address (useful when hosting both client and API together).
if (string.IsNullOrWhiteSpace(apiBase))
{
	var hostBase = builder.HostEnvironment.BaseAddress;
	if (!string.IsNullOrWhiteSpace(hostBase) && (hostBase.StartsWith("http://") || hostBase.StartsWith("https://")))
	{
		// Use same origin as the client by default (assumes API is served from same host/port)
		apiBase = hostBase;
	}
	else
	{
		// Fallback to the API development HTTPS endpoint to avoid mixed-content issues
		apiBase = "https://localhost:7067/";
	}
}

// Log the resolved API base for easier debugging in browser console
System.Console.WriteLine($"Client configured ApiBaseUrl={apiBase}");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBase) });
builder.Services.AddScoped<Client.Services.IAuthService, Client.Services.AuthService>();

var host = builder.Build();

// Initialize auth (reads token from localStorage and applies Authorization header if present)
var auth = host.Services.GetRequiredService<Client.Services.IAuthService>();
await auth.InitializeAsync();

await host.RunAsync();
