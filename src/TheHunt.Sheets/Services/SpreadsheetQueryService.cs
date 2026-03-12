using Microsoft.Extensions.Caching.Hybrid;
using TheHunt.Data.Models;
using TheHunt.Sheets.Models;

namespace TheHunt.Sheets.Services;

public class SpreadsheetQueryService(SpreadsheetService spreadsheetService, HybridCache cache)
{
    private static TimeSpan CacheExpiration { get; } = TimeSpan.FromMinutes(1);

    #region Members

    public async Task<IReadOnlyDictionary<ulong, CompetitionUser>> GetCompetitionMembers(SheetsRef sheetRef,
        CancellationToken cancellationToken = default)
    {
        var result = await UseCache($"__{sheetRef.SpreadsheetId}_members",
            async ct =>
            {
                var members = await spreadsheetService.GetMembers(sheetRef, ct);
                return members.ToDictionary(c => c.UserId);
            }, cancellationToken);

        return result!;
    }

    public async Task<CompetitionUser?> GetCompetitionMember(SheetsRef sheetRef, ulong userId)
    {
        var members = await GetCompetitionMembers(sheetRef);
        return members.TryGetValue(userId, out var val) ? val : null;
    }

    #endregion

    #region Items

    public async Task<IReadOnlyDictionary<string, CompetitionItem>> GetCompetitionItems(SheetsRef sheetRef,
        CancellationToken cancellationToken = default)
    {
        var result = await UseCache($"__{sheetRef.SpreadsheetId}_items",
            async ct =>
            {
                var items = await spreadsheetService.GetItems(sheetRef, ct);
                return items.ToDictionary(c => c.Name);
            },
            cancellationToken);

        return result!;
    }

    public async Task<CompetitionItem?> GetCompetitionItem(SheetsRef sheetRef, string name,
        CancellationToken cancellationToken = default)
    {
        var items = await GetCompetitionItems(sheetRef, cancellationToken);
        return items.TryGetValue(name, out var val) ? val : null;
    }

    public async Task<bool> VerifyItemExists(SheetsRef sheetRef, string? itemName, CancellationToken cancellationToken = default)
    {
        return itemName != null && (await GetCompetitionItems(sheetRef, cancellationToken)).ContainsKey(itemName);
    }

    #endregion

    public async Task ResetCache(SheetsRef sheetRef, string type, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync($"__{sheetRef.SpreadsheetId}_{type}", cancellationToken);
    }

    private async Task<T?> UseCache<T>(string cacheKey, Func<CancellationToken, ValueTask<T?>> fetcher,
        CancellationToken cancellationToken = default) where T : class
    {
        return await cache.GetOrCreateAsync(
            cacheKey,
            fetcher,
            new HybridCacheEntryOptions
            {
                Expiration = CacheExpiration, LocalCacheExpiration = CacheExpiration
            }, cancellationToken: cancellationToken);
    }
}