using System;
using System.Net;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Newtonsoft.Json.Linq;

namespace MuffaloBot.Commands
{
    public class WikiCommands
    {
        private const string queryAddress =
            "http://rimworldwiki.com/api.php?action=query&list=search&format=json&srlimit=5&srprop=size|wordcount|timestamp&srsearch={0}";

        [Command("wikisearch")]
        [Description("Searches the RimWorld wiki for content.")]
        public async Task Search(CommandContext ctx, [Description("The search query.")] string query)
        {
            await ctx.TriggerTypingAsync().ConfigureAwait(false);
            var webClient = new WebClient();
            var result = await webClient.DownloadStringTaskAsync(string.Format(queryAddress, query))
                .ConfigureAwait(false);
            var jObject = JObject.Parse(result);
            var builder = new DiscordEmbedBuilder();
            builder.WithTitle($"Results for '{query}'");
            builder.WithColor(DiscordColor.Azure);
            foreach (var token in jObject["query"]["search"])
            {
                builder.AddField(token["title"].ToString(),
                    $"**Info**\n{token["size"]} bytes\n" +
                    $"{token["wordcount"]} words\n" +
                    $"Last Edited UTC {DateTime.Parse(token["timestamp"].ToString())}\n" +
                    $"[Link](http://rimworldwiki.com/wiki/{token["title"].ToString().Replace(" ", "%20")})", true);
            }

            await ctx.RespondAsync(embed: builder.Build()).ConfigureAwait(false);
        }
    }
}