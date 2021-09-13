using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using MuffaloBot.Modules;
using Newtonsoft.Json.Linq;

namespace MuffaloBot.Commands
{
    public class QuoteCommands
    {
        [Command("quotes")]
        [Aliases("quote", "listquotes")]
        [Description("List all the available quote commands.")]
        public async Task ListQuotesAsync(CommandContext ctx)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Listing all quotes:");
            var data = ctx.Client.GetModule<JsonDataModule>().data;
            if (data != null && data["quotes"] != null)
            {
                foreach (var item in data["quotes"])
                {
                    var pair = (JProperty)item;
                    stringBuilder.Append($"`{pair.Name}` ");
                }
            }

            await ctx.RespondAsync(stringBuilder.ToString());
        }
    }
}