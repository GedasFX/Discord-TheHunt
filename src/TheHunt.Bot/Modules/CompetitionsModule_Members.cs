using Discord;
using Discord.Interactions;
using TheHunt.Core.Exceptions;
using TheHunt.Data.Services;
using TheHunt.Sheets.Services;

namespace TheHunt.Bot.Modules;

public partial class CompetitionsModule
{
    [Group("members", "Provides tools for managing competition members.")]
    public class CompetitionsMembersModule(
        SpreadsheetService sheetService,
        SpreadsheetQueryService sheetQueryService,
        CompetitionsQueryService competitionsQueryService)
        : InteractionModuleBase<SocketInteractionContext>
    {
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("invite", "Adds a user to the competition.")]
        public async Task Invite(
            [Summary(description: "User to invite to the competition.")]
            IGuildUser user,

            [Summary(description: "Team name. If left unspecified, will not assign a team to the participant.")]
            string? team = null,

            CancellationToken cancellationToken = default)
        {
            await DeferAsync(ephemeral: true, options: new RequestOptions { CancelToken = cancellationToken });

            var competition = await competitionsQueryService.GetCompetition(Context.Channel.Id, cancellationToken) ??
                              throw EntityNotFoundException.CompetitionNotFound;

            if (await sheetQueryService.GetCompetitionMember(competition.Spreadsheet, user.Id) != null)
                throw new EntityValidationException(
                    $"{MentionUtils.MentionUser(user.Id)} is already part of the competition.");

            await sheetService.AddMember(competition.Spreadsheet, user.Id, user.DisplayName, team);
            await sheetQueryService.ResetCache(competition.Spreadsheet, "members", cancellationToken);

            await FollowupAsync($"{MentionUtils.MentionUser(user.Id)} was successfully added to the competition.",
                ephemeral: true, options: new RequestOptions { CancelToken = cancellationToken });
        }

        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("kick", "Removes a user from the competition.")]
        public async Task Kick(
            [Summary(description: "User to remove from the competition.")]
            IGuildUser user,

            CancellationToken cancellationToken = default)
        {
            await DeferAsync(ephemeral: true, options: new RequestOptions { CancelToken = cancellationToken });

            var competition = await competitionsQueryService.GetCompetition(Context.Channel.Id, cancellationToken) ??
                              throw EntityNotFoundException.CompetitionNotFound;

            var participant = await sheetQueryService.GetCompetitionMember(competition.Spreadsheet, user.Id);
            if (participant == null)
                throw new EntityValidationException(
                    $"{MentionUtils.MentionUser(user.Id)} is not part of the competition.");

            await sheetService.RemoveMember(competition.Spreadsheet, participant.RowIdx, cancellationToken);
            await sheetQueryService.ResetCache(competition.Spreadsheet, "members", cancellationToken);

            await FollowupAsync($"{MentionUtils.MentionUser(user.Id)} was successfully removed from the competition.",
                ephemeral: true, options: new RequestOptions { CancelToken = cancellationToken });
        }
    }
}