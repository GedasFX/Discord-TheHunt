using Discord;
using Discord.Interactions;
using TheHunt.Core.Exceptions;
using TheHunt.Data;

namespace TheHunt.Bot.Modules;

public partial class CompetitionsModule
{
    [Group("config", "Manage competition configuration. To see active config, run /competitions show-config.")]
    public class CompetitionsConfigModule(AppDbContext dbContext)
        : InteractionModuleBase<SocketInteractionContext>
    {
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("verifier-role", "Changes the verifier role for the competition.")]
        public async Task VerifierRole(
            [Summary(description: "Users with this role will be able to verify submissions.")]
            IRole newRole,

            CancellationToken cancellationToken = default)
        {
            var competition =
                await dbContext.Competitions.FindAsync([Context.Channel.Id], cancellationToken: cancellationToken) ??
                throw EntityNotFoundException.CompetitionNotFound;

            var previousVerifierRoleId = competition.VerifierRoleId;

            competition.VerifierRoleId = newRole.Id;
            await dbContext.SaveChangesAsync(cancellationToken);

            await RespondAsync(
                $"Verifier role was updated from {MentionUtils.MentionRole(previousVerifierRoleId)} to {MentionUtils.MentionRole(competition.VerifierRoleId)}.",
                ephemeral: true,
                options: new RequestOptions { CancelToken = cancellationToken });
        }

        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("items-restricted", "Prevent submission of unknown items.")]
        public async Task ItemsRestricted(
            [Summary(description: "If set to 'true', when verifying an unknown item (not in __xyz_items), verification will fail.")]
            bool restricted,

            CancellationToken cancellationToken = default)
        {
            var competition = await dbContext.Competitions.FindAsync([Context.Channel.Id], cancellationToken: cancellationToken) ??
                              throw EntityNotFoundException.CompetitionNotFound;

            competition.Features.ItemsRestricted = restricted;
            await dbContext.SaveChangesAsync(cancellationToken);

            await RespondAsync(
                $"When verifying an unknown item (not present in __xyz_items), verification will now {(restricted ? "fail" : "succeed")}.",
                ephemeral: true,
                options: new RequestOptions { CancelToken = cancellationToken });
        }
    }
}