using Discord;
using Discord.Interactions;
using TheHunt.Bot.Utils;
using TheHunt.Core.Exceptions;
using TheHunt.Data.Services;

namespace TheHunt.Bot.Modules;

public partial class CompetitionsModule
{
    public class CompetitionsShowModule(
        CompetitionsQueryService competitionsQueryService)
        : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("show", "Provides a high level overview of competition.")]
        public async Task Overview(
            [Summary(description: "Should the message be posted to the channel? Defaults to False.")]
            bool @public = false)
        {
            var competition = await competitionsQueryService.GetCompetition(Context.Channel.Id);
            if (competition == null)
                throw EntityNotFoundException.CompetitionNotFound;

            await RespondAsync(
                components: new ComponentBuilder()
                    .AddRow(new ActionRowBuilder()
                        .WithSpreadsheetRefButton("Overview", "📖", competition.Spreadsheet.SpreadsheetId,
                            competition.Spreadsheet.Sheets.Overview)
                        .WithSpreadsheetRefButton("Submissions", "🖼️", competition.Spreadsheet.SpreadsheetId,
                            competition.Spreadsheet.Sheets.Submissions))
                    .Build(),
                ephemeral: !@public);
        }

        [SlashCommand("show-config", "Provides a high level overview of competition.")]
        public async Task Config()
        {
            var competition = await competitionsQueryService.GetCompetition(Context.Channel.Id);
            if (competition == null)
                throw EntityNotFoundException.CompetitionNotFound;

            await RespondAsync(
                embed: new EmbedBuilder()
                    .WithTitle(Context.Channel.Name)
                    .WithUrl(FormatUtils.GetSheetUrl(competition.Spreadsheet.SpreadsheetId,
                        competition.Spreadsheet.Sheets.Overview))
                    .AddField("Verifier Role",
                        "The role user must be part of to be able to verify submissions.\n" +
                        $"\\> {MentionUtils.MentionRole(competition.VerifierRoleId)}")
                    .AddField("Restrict Items",
                        "When enabled, only items from __xyz_items, will be allowed to be verified.\n" +
                        $"\\> {(competition.Features.ItemsRestricted ? "Enabled" : "Disabled")}")
                    .WithColor(0xA44200)
                    .Build(),
                ephemeral: true);
        }
    }
}