using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;
using TheHunt.Core.Exceptions;
using TheHunt.Data.Services;
using TheHunt.Sheets.Services;

namespace TheHunt.Bot.Modules;

public partial class CompetitionsModule
{
    [Group("items", "Provides tools for managing competition items.")]
    public class CompetitionsItemsModule(
        SpreadsheetService sheetService,
        SpreadsheetQueryService sheetQueryService,
        CompetitionsQueryService competitionsQueryService)
        : InteractionModuleBase<SocketInteractionContext>
    {
        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("add", "Adds an item to the competition.")]
        public async Task Add(
            [Summary(description: "Name of the item.")]
            string name,

            [Summary(description: "Points value. Default = 0.")]
            int pointsValue = 0,

            CancellationToken cancellationToken = default)
        {
            await DeferAsync(ephemeral: true, options: new RequestOptions { CancelToken = cancellationToken });

            var competition = await competitionsQueryService.GetCompetition(Context.Channel.Id, cancellationToken) ??
                              throw EntityNotFoundException.CompetitionNotFound;

            if (await sheetQueryService.VerifyItemExists(competition.Spreadsheet, name, cancellationToken))
            {
                // Not an exception to handle component interactions
                await FollowupAsync($"Item '{name}' was already registered.", ephemeral: true, options: new RequestOptions { CancelToken = cancellationToken });
                return;
            }

            await sheetService.AddItem(competition.Spreadsheet, name, pointsValue, cancellationToken);
            await sheetQueryService.ResetCache(competition.Spreadsheet, "items", cancellationToken);

            await FollowupAsync($"Item '{name}' was registered. To undo, run:\n```/competitions items remove name:{name}```",
                ephemeral: true, options: new RequestOptions { CancelToken = cancellationToken });
        }

        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [SlashCommand("remove", "Removes an item from the competition.")]
        public async Task Remove(
            [Summary(description: "User to remove from the competition.")]
            [Autocomplete(typeof(ItemsListAutocompleteHandler))]
            string name,
            
            CancellationToken cancellationToken = default)
        {
            await DeferAsync(ephemeral: true);
            
            var competition = await competitionsQueryService.GetCompetition(Context.Channel.Id, cancellationToken) ??
                              throw EntityNotFoundException.CompetitionNotFound;
            
            var item = await sheetQueryService.GetCompetitionItem(competition.Spreadsheet, name, cancellationToken);
            if (item == null)
                throw new EntityValidationException($"Item '{name}' was not registered.");

            await sheetService.RemoveItem(competition.Spreadsheet, item.RowIdx, cancellationToken);
            await sheetQueryService.ResetCache(competition.Spreadsheet, "items", cancellationToken);

            await FollowupAsync($"Item '{name}' was unregistered. To undo, run:\n```/competitions items add name:{name}```",
                ephemeral: true);
        }

        [RequireUserPermission(ChannelPermission.ManageChannels)]
        [ComponentInteraction("i:*", ignoreGroupNames: true)]
        public async Task AddFromInteraction(string name, CancellationToken cancellationToken = default)
        {
            // Hack - using '|' as whitespace. If item has '|' in its name, tough luck, we will not show button.
            await Add(name.Replace('|', ' '), cancellationToken: cancellationToken);
        }
        
        public class ItemsListAutocompleteHandler : AutocompleteHandler
        {
            public override async Task<AutocompletionResult> GenerateSuggestionsAsync(IInteractionContext context, IAutocompleteInteraction autocompleteInteraction,
                IParameterInfo parameter, IServiceProvider services)
            {
                var val = autocompleteInteraction.Data.Current.Value as string ?? "";
                
                var queryService = services.GetRequiredService<CompetitionsQueryService>();
                
                var sheetsRef = await queryService.GetSpreadsheetRef(context.Channel.Id);
                if (sheetsRef == null)
                    return AutocompletionResult.FromError(EntityNotFoundException.CompetitionNotFound);

                var sheetsQueryService = services.GetRequiredService<SpreadsheetQueryService>();
                var items = await sheetsQueryService.GetCompetitionItems(sheetsRef);

                return AutocompletionResult.FromSuccess(
                    items.Where(e => e.Key.Contains(val))
                        .Select(e => new AutocompleteResult(e.Key, e.Key))
                        .Take(25));
            }
        }
    }
}