using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TheHunt.Core;

namespace TheHunt.Bot;

public class BotModule : Module
{
    public override void Configure(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds
        }));
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(1),
                LocalCacheExpiration = TimeSpan.FromHours(1),
            };
        });
    }
}