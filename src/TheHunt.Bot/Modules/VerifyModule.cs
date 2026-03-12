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
    private static IEmote PendingEmote { get; } = new Emoji("⏳");

    private static string SelectedMessageCacheKey(ulong userId, ulong channelId) =>
        $"__selected_message_{userId}_{channelId}";

    [CommandContextType(InteractionContextType.Guild)]
    [MessageCommand("Verify Submission")]
    public async Task VerifySubmission(IUserMessage message)
    {
        await RespondWithModalAsync<SubmissionModal>($"submission_create:{message.Id}");
    }

    [CommandContextType(InteractionContextType.Guild)]
    [MessageCommand("Quick Verify Submission")]
    public async Task QuickVerifySubmission(IUserMessage message)
    {
        await VerifySubmission(message.Id, message.Content.Split('\n')[0]);
    }

    [CommandContextType(InteractionContextType.Guild)]
    [MessageCommand("Select for Verification")]
    public async Task SelectForVerification(IUserMessage message)
    {
        await hybridCache.SetAsync(
            SelectedMessageCacheKey(Context.User.Id, Context.Channel.Id),
            message.Id);
        await RespondAsync(
            "Message selected for verification. Now use `/verify` to complete the verification.",
            ephemeral: true);
    }

    [CommandContextType(InteractionContextType.Guild)]
    [SlashCommand("verify", "Verifies the previously selected submission.")]
    public async Task VerifySelected(
        [Summary(description: "Name of the item being submitted.")]
        [Autocomplete(typeof(CompetitionsModule.CompetitionsItemsModule.ItemsListAutocompleteHandler))]
        string? item,
        
        [Summary(description: "Bonus points to award.")]
        int bonus = 0)
    {
        var cacheKey = SelectedMessageCacheKey(Context.User.Id, Context.Channel.Id);
        var messageId = await hybridCache.GetOrCreateAsync(cacheKey, _ => ValueTask.FromResult<ulong>(0));
        if (messageId is 0)
        {
            await RespondAsync("No message selected. Right-click a message and choose **Select for Verification** first.",
                ephemeral: true);
            return;
        }

        await hybridCache.RemoveAsync(cacheKey);
        await VerifySubmission(messageId, item, bonus);
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
    public async Task VerifySubmissionCallback(ulong messageId, SubmissionModal modal)
    {
        await VerifySubmission(messageId, modal.Item, int.TryParse(modal.Bonus, out var bonus) ? bonus : 0);
    }


    private async Task VerifySubmission(ulong messageId, string? item = null, int bonusPoints = 0)
    {
        if (Context.User is not SocketGuildUser contextGuildUser)
            throw new UnreachableException("Invoked VerifySubmission in DM Context.");

        await DeferAsync(ephemeral: true);

        var message = await Context.Channel.GetMessageAsync(messageId);

        if (message.Reactions.TryGetValue(VerifiedEmote, out var verifiedReaction) && verifiedReaction.IsMe)
        {
            await FollowupAsync("Submission was already verified.", ephemeral: true);
            return;
        }

        if (message.Reactions.TryGetValue(PendingEmote, out var pendingReaction) && pendingReaction.IsMe)
        {
            await FollowupAsync("Submission is currently being verified by someone else.", ephemeral: true);
            return;
        }
        
        var competition = await competitionsQueryService.GetCompetition(message.Channel.Id);
        if (competition == null)
        {
            await FollowupAsync("Unable to verify submission: Channel is not associated with a competition.",
                ephemeral: true);
            return;
        }

        if (!contextGuildUser.Roles.Any(r => r.Id == competition.VerifierRoleId))
        {
            await FollowupAsync($"Unable to verify submission: Only members in {MentionUtils.MentionRole(competition.VerifierRoleId)} role can verify submissions.",
                ephemeral: true);
            return;
        }

        var sheetsRef = (await competitionsQueryService.GetSpreadsheetRef(Context.Channel.Id))!;

        if (item != null && competition.Features.ItemsRestricted &&
            !await spreadsheetQueryService.VerifyItemExists(competition.Spreadsheet, item))
        {
            await FollowupAsync($"Restricted items: Item with name '{item}' was not found.",
                components: ((SocketGuildUser)Context.User).GuildPermissions.Has(GuildPermission.ManageChannels) &&
                            item.Length is > 0 and < 99 && !item.Contains('|')
                    ? new ComponentBuilder().WithButton(label: "Add item", emote: new Emoji("➕"),
                        customId: $"i:{item.Replace(' ', '|')}").Build()
                    : null,
                ephemeral: true);
            return;
        }

        // Add pending reaction to lock this submission
        try
        {
            await message.AddReactionAsync(PendingEmote);
        }
        catch
        {
            // If we fail to add pending reaction, another verification might be in progress
            await FollowupAsync("Failed to add pending reaction. Please try again.", ephemeral: true);
            return;
        }
        
        try
        {
            await spreadsheetService.AddSubmission(sheetsRef, message.Id, message.GetJumpUrl(), message.Author.Id,
                Context.User.Id, GetAttachedImageUrl(message),
                message.Timestamp.UtcDateTime, item, bonusPoints);

            await message.AddReactionAsync(VerifiedEmote);

            await FollowupAsync($"Submission verified successfully!\nItem: **{item}**", ephemeral: true,
                components: new ComponentBuilder().AddRow(new ActionRowBuilder()
                    .WithSpreadsheetRefButton("Open Google Sheets", "📑", sheetsRef.SpreadsheetId,
                        sheetsRef.Sheets.Submissions)).Build());
        }
        finally
        {
            await message.RemoveReactionAsync(PendingEmote, Context.Client.CurrentUser);
        }
    }

    private static string? GetAttachedImageUrl(IMessage message)
    {
        return message.Attachments.FirstOrDefault(a => a.ContentType.StartsWith("image/"))?.Url ??
               message.Embeds.FirstOrDefault(e => e.Image != null || e.Thumbnail != null)?.Url;
    }
}