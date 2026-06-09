using Microsoft.EntityFrameworkCore;
using Oloraculo.Web;
using Oloraculo.Web.Components;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Services;
using Oloraculo.Web.Services.Simulation;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<OloraculoConfig>(builder.Configuration.GetSection("Oloraculo"));
var ConnectionString = builder.Configuration.GetConnectionString("Oloraculo") ?? 
    throw new ArgumentNullException("No connection string found in the config!");


builder.Services.AddDbContext<OloraculoDbContext>(options => options.UseSqlite(ConnectionString));

builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<EvaluationService>();
builder.Services.AddScoped<SnapshotService>();
builder.Services.AddScoped<SimulationService>();
builder.Services.AddHttpClient<ApiFootballService>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OloraculoConfig>>().Value;
    client.BaseAddress = new Uri(options.ApiFootballBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(45);
    client.DefaultRequestHeaders.Add("User-Agent", "Oloraculo");
    if (!string.IsNullOrWhiteSpace(options.ApiFootballApiKey))
    {
        client.DefaultRequestHeaders.Add("x-apisports-key", options.ApiFootballApiKey);
    }
});

var app = builder.Build();

using (var Scope = app.Services.CreateScope())
{
    var CsvImporterService = Scope.ServiceProvider.GetRequiredService<CsvImportService>();
    await CsvImporterService.ImportIfNeededAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
