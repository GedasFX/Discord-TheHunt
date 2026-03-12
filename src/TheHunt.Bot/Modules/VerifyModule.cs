using System.Diagnostics;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Caching.Hybrid;
using TheHunt.Bot.Utils;
using TheHunt.Data.Services;
using TheHunt.Sheets.Services;

namespace TheHunt.Bot.Modules;

public class VerifyModule(
    CompetitionsQueryService competitionsQueryService,
    SpreadsheetService spreadsheetService,
    SpreadsheetQueryService spreadsheetQueryService,
    HybridCache hybridCache
)
    : InteractionModuleBase<SocketInteractionContext>
{
    private static IEmote VerifiedEmote { get; } = new Emoji("✅");

    private static string SelectedMessageCacheKey(ulong userId, ulong channelId) =>
        $"__selected_message_{userId}_{channelId}";

    [CommandContextType(InteractionContextType.Guild)]
    [MessageCommand("Verify Submission")]
    public async Task VerifySubmission(IUserMessage message, CancellationToken cancellationToken = default)
    {
        await RespondWithModalAsync<SubmissionModal>($"submission_create:{message.Id}",
            new RequestOptions() { CancelToken = cancellationToken });
    }

    [CommandContextType(InteractionContextType.Guild)]
    [MessageCommand("Quick Verify Submission")]
    public async Task QuickVerifySubmission(IUserMessage message, CancellationToken cancellationToken = default)
    {
        await VerifySubmission(message.Id, message.Content.Split('\n')[0], cancellationToken: cancellationToken);
    }

    public class SubmissionModal : IModal
    {
        public string Title => "Verify Submission";

        [RequiredInput(false), InputLabel("Item")]
        [ModalTextInput("sub_name", TextInputStyle.Short, "Hylian Shield / Broken Zenith", maxLength: 240)]
        public string? Item { get; set; }

        // Strings with the ModalTextInput attribute will automatically become components.
        [RequiredInput(false), InputLabel("[Optional] Bonus Points")]
        [ModalTextInput("sub_bonus", placeholder: "0 / 16 / -55", maxLength: 11)]
        public string? Bonus { get; set; }
    }

    [ModalInteraction("submission_create:*")]
    public async Task VerifySubmissionCallback(ulong messageId, SubmissionModal modal, CancellationToken cancellationToken = default)
    {
        await VerifySubmission(messageId, modal.Item, int.TryParse(modal.Bonus, out var bonus) ? bonus : 0, cancellationToken);
    }


    private async Task VerifySubmission(ulong messageId, string? item = null, int bonusPoints = 0,
        CancellationToken cancellationToken = default)
    {
        if (Context.User is not SocketGuildUser contextGuildUser)
            throw new UnreachableException("Invoked VerifySubmission in DM Context.");

        await DeferAsync(ephemeral: true, new RequestOptions { CancelToken = cancellationToken });

        var message = await Context.Channel.GetMessageAsync(messageId, options: new RequestOptions { CancelToken = cancellationToken });

        if (message.Reactions.TryGetValue(VerifiedEmote, out var dat) && dat.IsMe)
        {
            await FollowupAsync("Submission was already verified.", ephemeral: true, options: new RequestOptions { CancelToken =  cancellationToken });
            return;
        }

        var competition = await competitionsQueryService.GetCompetition(message.Channel.Id, cancellationToken);
        if (competition == null)
        {
            await FollowupAsync("Unable to verify submission: Channel is not associated with a competition.",
                ephemeral: true, options: new RequestOptions() { CancelToken = cancellationToken });
            return;
        }

        if (!contextGuildUser.Roles.Any(r => r.Id == competition.VerifierRoleId))
        {
            await FollowupAsync(
                $"Unable to verify submission: Only members in {MentionUtils.MentionRole(competition.VerifierRoleId)} role can verify submissions.",
                ephemeral: true,
                options: new RequestOptions() { CancelToken = cancellationToken });
            return;
        }

        var sheetsRef = (await competitionsQueryService.GetSpreadsheetRef(Context.Channel.Id, cancellationToken))!;

        if (item != null && competition.Features.ItemsRestricted &&
            !await spreadsheetQueryService.VerifyItemExists(competition.Spreadsheet, item, cancellationToken))
        {
            await FollowupAsync($"Restricted items: Item with name '{item}' was not found.",
                components: ((SocketGuildUser)Context.User).GuildPermissions.Has(GuildPermission.ManageChannels) &&
                            item.Length is > 0 and < 99 && !item.Contains('|')
                    ? new ComponentBuilder().WithButton(label: "Add item", emote: new Emoji("➕"),
                        customId: $"i:{item.Replace(' ', '|')}").Build()
                    : null,
                ephemeral: true, options: new RequestOptions() { CancelToken = cancellationToken });
            return;
        }

        await spreadsheetService.AddSubmission(sheetsRef, message.Id, message.GetJumpUrl(), message.Author.Id,
            Context.User.Id, GetAttachedImageUrl(message),
            message.Timestamp.UtcDateTime, item, bonusPoints, cancellationToken);

        await message.AddReactionAsync(VerifiedEmote, new RequestOptions { CancelToken = cancellationToken });
        await FollowupAsync("Submission verified successfully!", ephemeral: true,
            components: new ComponentBuilder().AddRow(new ActionRowBuilder()
                .WithSpreadsheetRefButton("Open Google Sheets", "📑", sheetsRef.SpreadsheetId,
                    sheetsRef.Sheets.Submissions)).Build(),
            options: new RequestOptions { CancelToken = cancellationToken });
    }

    private static string? GetAttachedImageUrl(IMessage message)
    {
        return message.Attachments.FirstOrDefault(a => a.ContentType.StartsWith("image/"))?.Url ??
               message.Embeds.FirstOrDefault(e => e.Image != null || e.Thumbnail != null)?.Url;
    }
}