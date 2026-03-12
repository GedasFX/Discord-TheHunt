using Discord;
using Discord.Interactions;

namespace TheHunt.Bot.Modules;

public partial class CompetitionsModule
{
    [Group("help", "Provides tools for managing competition members.")]
    public class CompetitionsHelpModule : InteractionModuleBase<SocketInteractionContext>
    {
        private static Embed HelpCreateEmbed { get; } = new EmbedBuilder()
            .WithTitle("Help - /competitions create")
            .AddField("Step 1 - Create a Google Spreadsheet", """
The spreadsheet will be used to track submissions and manage members of the competition. You can reuse older spreadsheets for multiple competitions.
""")
            .AddField("Step 2 - Invite the bot Service Account", """
For the bot to be able to make edits, it must have access to the spreadsheet. To achieve this, invite the bot with 'Editor' role. 
**Service Account (Email):**
```
the-hunt@the-hunt-373015.iam.gserviceaccount.com
```
""")
            .AddField("Step 3 - Extract Spreadsheet ID", """
Spreadsheet ID will be used to bind competitions to spreadsheets. 
ID can be extracted from the url (`SPREADSHEET_ID`):
```
https://docs.google.com/spreadsheets/d/SPREADSHEET_ID/edit#gid=0
```
""")
            .AddField("Step 4 - Create the Competition Channel", """
Create a new channel for the competition. This is the place where participants will be able to submit competition entries.

**NB!** Once competition is created, this channel be permanently bound to that competition.
""")
            .AddField("Step 5 - Create the Verifier Role", """
Create a new (or reuse an existing) role. Users with this role will be able to verify submissions.
""")
            .AddField("Step 6 - /competitions create", """
In the channel you wish to create the competition, run `/competitions create SPREADSHEET_ID VERIFIER_ROLE`. This will create the competition, bind it to the spreadsheet, and create new sheets there for use in the competition.

**NB!** You need to have `Manage Channels` permission to run this command.

**NB!** It is not recommended to rename the newly created sheets (with exception of Overview) while competition is in progress.

**NB!** For more information about manually editing the sheets, use `/competitions help manual-edit`.
""")
            .AddField("[Optional] Step 7 - /competitions show", """
To see the competition information and status, use `/competitions show` in the channel the competition was created.
""")
            .Build();

        [SlashCommand("create", "Instructions on how to create a competition.")]
        public async Task HelpCreate()
        {
            await RespondAsync(embed: HelpCreateEmbed, ephemeral: true);
        }

        private static Embed HelpVerifyEmbed { get; } = new EmbedBuilder()
            .WithTitle("Help - Submission Verification")
            .WithImageUrl("https://cdn.discordapp.com/attachments/1059186725034934303/1059186752910262362/image.png")
            .AddField("Prerequisites", """
1. Post must be in the submissions channel. To see if channel is bound to a competition run `/competitions show`.
2. The person verifying submissions must be in the verifiers role. This role can be seen on the `/competitions show` as well.
""")
            .AddField("Verify Submission - Part 1", """
Right-click the message (see image) and click `Apps -> Verify Submission`. This will open a dialog window where you can provide additional details.
""")
            .AddField("Verify Submission - Part 2", """
After you start verification process, you will be greeted with 2 input fields - item and bonus points.
1. Item field takes an item name, which would cross-reference the pre-populated items sheet and would give the appropriate amount of points.
2. Bonus Points are for cases where submission deserves additional rewards (or penalty). This number can be either positive or negative.
""")
            .AddField("Help! I made a mistake!", """
If you have made a mistake (wrong item, or incorrect point amount), you can always manually edit the underlying Google Sheet where the data is stored.

In case you would need to delete a submission, remove the '✅' reaction and delete the row in the Google Sheet. It is important that you delete the row and not just make it empty.

For additional information about manual edits, see `/competitions help manual-edit`.
""")
            .AddField("Quick Verifications",
                """
                There is also a "Quick Verify Submission" button present. Clicking it would send the same verification request as normal, but will prefill the "item" with the first line of the message contents.

                It works great for cases where the users can help identify what the item they got is. Example:
                ```
                Banana Bandanna
                https://i.imgur.whatever/
                ```
                """)
            .Build();

        [SlashCommand("verify", "Instructions on how to verify submissions.")]
        public async Task HelpVerify()
        {
            await RespondAsync(embed: HelpVerifyEmbed, ephemeral: true);
        }
        
        private static Embed HelpManualEditEmbed { get; } = new EmbedBuilder()
            .WithTitle("Help - Manual Edit")
            .AddField("Sheets", """
On competition creation, the bot generates 5 sheets - `overview`, `config`, `members`, `items`, and `submissions`.

While a competition is in-progress, it is not recommended to rename these sheets. An exception applies to the `overview` sheet, which is not used for any bot activities or cross-referencing.
""")
            .AddField("Deleting rows", """
It is possible to remove items from the competition, however the bot does not like empty rows, so if you must delete an entry, delete the entire row (right-click the row number and select `Delete Row`).
""")
            .AddField("Additional columns", """
You can extend the functionality of the bot by manually adding additional columns.

One good use case for additional columns would be to track the value of an item in the items sheet and to put the item value in the submissions sheet. If the competition occurs in an MMO, it may be cool to know how much total gold value was gained in the competition / who gained the most.

To add additional columns, right-click on the last column and select `Insert 1 column right`.

**NB!** It is important to not insert columns in-between the generated columns, only at the end, as the column indexes are important for the bot operations.
""")
            .AddField("Is it safe to make edits?", """
Yes. It is safe to make manual edits on the spreadsheet. If you do not rename sheets, properly delete rows, and correctly add additional columns, you should not have any issues.

**NB!** We have no validation for manual edits, so some invalid values (such as text instead of numbers) may lead to broken formulas elsewhere.
""")
            .Build();

        [SlashCommand("manual-edit", "Instructions on how to verify submissions.")]
        public async Task HelpManualEdit()
        {
            await RespondAsync(embed: HelpManualEditEmbed, ephemeral: true);
        }
    }
}