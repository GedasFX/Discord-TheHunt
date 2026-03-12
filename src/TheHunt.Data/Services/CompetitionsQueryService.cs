using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using TheHunt.Data.Models;

namespace TheHunt.Data.Services;

public class CompetitionsQueryService(AppDbContext dbContext, HybridCache hybridCache)
{
    private static readonly HybridCacheEntryOptions HybridCacheEntryOptions = new()
    {
        Flags = HybridCacheEntryFlags.DisableDistributedCache,
        Expiration = TimeSpan.FromSeconds(30),
        LocalCacheExpiration = TimeSpan.FromSeconds(30)
    };

    public async Task<Competition?> GetCompetition(ulong competitionId, CancellationToken cancellationToken = default)
    {
        return await hybridCache.GetOrCreateAsync(
            $"competition_{competitionId}",
            async ct => await dbContext.Competitions.AsNoTracking()
                .Where(c => c.ChannelId == competitionId)
                .FirstOrDefaultAsync(cancellationToken: ct),
            HybridCacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<SheetsRef?> GetSpreadsheetRef(ulong competitionId, CancellationToken cancellationToken = default)
    {
        return await hybridCache.GetOrCreateAsync(
            $"spreadsheet_ref_{competitionId}",
            async cancel => await dbContext.Competitions.AsNoTracking()
                .Where(c => c.ChannelId == competitionId)
                .Select(c => c.Spreadsheet)
                .FirstOrDefaultAsync(cancellationToken: cancel),
            HybridCacheEntryOptions,
            cancellationToken: cancellationToken);
    }

    public async Task<bool> CompetitionExists(ulong competitionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Competitions.AsNoTracking()
            .Where(c => c.ChannelId == competitionId)
            .AnyAsync(cancellationToken: cancellationToken);
    }
    
    public async Task<ulong> GetVerifierRoleId(ulong competitionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Competitions.AsNoTracking()
            .Where(c => c.ChannelId == competitionId)
            .Select(c => c.VerifierRoleId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }
}