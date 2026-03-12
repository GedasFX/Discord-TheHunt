using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheHunt.Core;
using TheHunt.Sheets.Services;

namespace TheHunt.Sheets;

public class SheetsModule : Module
{
    public override void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => new SpreadsheetService("google.json"));
        services.AddScoped<SpreadsheetQueryService>();
    }
}