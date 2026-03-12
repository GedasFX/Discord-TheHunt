using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using TheHunt.Core.Exceptions;
using TheHunt.Data;
using TheHunt.Data.Models;
using TheHunt.Sheets.Services;

namespace TheHunt.Bot.Modules;

[Group("competitions", "Tools to manage competitions.")]
public partial class CompetitionsModule(
    AppDbContext dbContext,
    SpreadsheetService sheet,
    SpreadsheetQueryService spreadsheetQueryService)
    : InteractionModuleBase<SocketInteractionContext>
{
    [RequireUserPermission(ChannelPermission.ManageChannels)]
    [SlashCommand("create", "Creates a new competition and binds it to the channel.")]
    public async Task Create(
        [Summary(description: "Google Spreadsheet Id. 'https://docs.google.com/spreadsheets/d/<SPREADSHEET_ID>/edit#gid=0'")]
        string spreadsheetId,
        
        [Summary(description: "Users with this role will be able to verify submissions.")]
        IRole verifierRole,
        
        [Summary(description: "Name of the competition. Defaults to specified channel name.")]
        string? name = null,
        
        CancellationToken cancellationToken = default
    )
    {
        if (spreadsheetId.Length != 44)
        {
            throw new EntityValidationException(
                "Invalid spreadsheet id. You can extract it from the URL: `https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit#gid=0`.");
        }

        await DeferAsync(ephemeral: true);

        if (await dbContext.Competitions.AsNoTracking().AnyAsync(c => c.ChannelId == Context.Channel.Id, cancellationToken: cancellationToken))
            throw new EntityValidationException("This channel already has a competition. Please use a different channel to create a new competition.");

        var entity = new Competition
        {
            ChannelId = Context.Channel.Id, VerifierRoleId = verifierRole.Id,
            Spreadsheet = await sheet.CreateCompetition(spreadsheetId, name ?? $"#{Context.Channel.Name}", cancellationToken)
        };

        dbContext.Competitions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        await FollowupAsync("Competition successfully created. You can view it with /competitions show");
    }

    [RequireUserPermission(ChannelPermission.ManageChannels)]
    [SlashCommand("reload", "Reloads the competition data from the spreadsheet.")]
    public async Task Reload(CancellationToken cancellationToken = default)
    {
        var competition =
            await dbContext.Competitions.FindAsync([Context.Channel.Id], cancellationToken: cancellationToken) ??
            throw EntityNotFoundException.CompetitionNotFound;

        await spreadsheetQueryService.ResetCache(competition.Spreadsheet, "items", cancellationToken);
        await spreadsheetQueryService.ResetCache(competition.Spreadsheet, "members", cancellationToken);

        await RespondAsync("Configuration reloaded.", ephemeral: true);
    }
}