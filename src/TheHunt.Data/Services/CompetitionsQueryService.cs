using Microsoft.EntityFrameworkCore;
using TheHunt.Data.Models;

namespace TheHunt.Data.Services;

public class CompetitionsQueryService(AppDbContext dbContext)
{
    public async Task<Competition?> GetCompetition(ulong competitionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Competitions.AsNoTracking()
            .Where(c => c.ChannelId == competitionId)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<SheetsRef?> GetSpreadsheetRef(ulong competitionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Competitions.AsNoTracking()
            .Where(c => c.ChannelId == competitionId)
            .Select(c => c.Spreadsheet)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
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