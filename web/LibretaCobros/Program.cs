using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using LibretaCobros;
using LibretaCobros.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<SupabaseAuthService>();
builder.Services.AddScoped<SupabaseClientService>();
builder.Services.AddScoped<ICobrosDataService, CobrosDataService>();
builder.Services.AddScoped<ThemeService>();

await builder.Build().RunAsync();
