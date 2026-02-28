using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Personal.Dashboard.Web.Host;

var builder = WebAssemblyHostBuilder.CreateDefault(args)
    .AddPersonalDashboardWeb();

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

await builder.Build().RunAsync();
