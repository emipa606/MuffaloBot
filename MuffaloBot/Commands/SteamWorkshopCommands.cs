﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using MuffaloBot.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MuffaloBot.Commands
{
    public class SteamWorkshopCommands
    {
        private const string baseUrl = "https://api.steampowered.com/";
        private const string relativeModPageEndpoint = "ISteamRemoteStorage/GetPublishedFileDetails/v1/";
        private const string relativeUserEndpoint = "ISteamUser/GetPlayerSummaries/v2/?key={0}&steamids={1}";

        private const string query = baseUrl +
                                     "IPublishedFileService/QueryFiles/v1/?key={0}&format=json&numperpage={1}&appid=294100&match_all_tags=1&search_text={2}&return_short_description=1&return_metadata=1&query_type={3}";

        private readonly HttpClient httpClient = new HttpClient();
        private readonly JsonSerializer jsonSerializer = new JsonSerializer();

        [Command("wshopmod")]
        [Description("Displays info about a workshop mod.")]
        public async Task Preview(CommandContext ctx,
            [RemainingText] [Description("The published file id or steam url of the mod")]
            string publishedFileId)
        {
            // Show Muffy typing while we wait.
            await ctx.TriggerTypingAsync();

            // Check if input is a steam url. If it is - extract published file id from it.
            if (Uri.TryCreate(publishedFileId, UriKind.Absolute, out var uri))
            {
                var queries = HttpUtility.ParseQueryString(uri.Query);
                publishedFileId = queries.Get("id");
            }

            if (publishedFileId is null || publishedFileId == string.Empty)
            {
                await ctx.RespondAsync("No results.");
                return;
            }

            // We only want the one and first workshop item.
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("itemcount", "1"),
                new KeyValuePair<string, string>("publishedfileids[0]", publishedFileId)
            });

            // The mod request.
            PublishedFileInfoModel.Publishedfiledetail mod;
            var modEndpointUrl = $"{baseUrl}{relativeModPageEndpoint}";
            using (var modResult = await httpClient.PostAsync(modEndpointUrl, content))
            await using (var modResponse = await modResult.Content.ReadAsStreamAsync())
            using (var modStreamReader = new StreamReader(modResponse))
            using (var modJsonReader = new JsonTextReader(modStreamReader))
            {
                var publishedFileIdInfo = jsonSerializer.Deserialize<PublishedFileInfoModel>(modJsonReader);
                mod = publishedFileIdInfo?.response?.PublishedFileDetails?.First();
            }

            // Checks if the workshop item exists at all, and belongs to the RimWorld domain.
            if (mod?.Creator_App_Id != 294100)
            {
                await ctx.RespondAsync("No results.");
                return;
            }

            // We want mod author as well, which we only get the id of above, so we make another request to get author info.
            List<Player> users;
            var userEndpointUrl =
                string.Format($"{baseUrl}{relativeUserEndpoint}", AuthResources.SteamApiKey, mod.Creator);
            using (var userResult = await httpClient.GetAsync(userEndpointUrl))
            await using (var userResponse = await userResult.Content.ReadAsStreamAsync())
            using (var userStreamReader = new StreamReader(userResponse))
            using (var userJsonReader = new JsonTextReader(userStreamReader))
            {
                var userModel = jsonSerializer.Deserialize<UserModel>(userJsonReader);
                users = userModel?.response.players;
            }

            if (users == null || users.Count == 0)
            {
                await ctx.RespondAsync("No results.");
                return;
            }

            var user = users.First();

            // Limiting the description to x nr of characters.
            var description = string.Concat(mod.Description.Take(150));

            // Remove all steam markup from description.
            var cleanDescription = $"{Regex.Replace(description, @"(\[\S*\])", string.Empty)}...";
            // Remove all links.
            cleanDescription = Regex.Replace(cleanDescription, @"(\[url=\S*\[/url\])", string.Empty);
            var versions = string.Join(", ",
                mod.Tags.Where(x => x.Tag != "Scenario" && x.Tag != "Mod").Select(x => x.Tag));
            var lastUpdated = mod.Time_Updated;

            var modEmbed = new DiscordEmbedBuilder()
                .WithColor(DiscordColor.Gold)
                .WithTitle(mod.Title)
                .WithUrl($"http://steamcommunity.com/sharedfiles/filedetails/?id={mod.PublishedFileId}")
                .AddField("Author", user.PersonaName, true)
                .AddField("Versions", versions, true)
                .AddField("Last Update", DateTimeOffset.FromUnixTimeSeconds(lastUpdated).Date.ToShortDateString(), true)
                .AddField("Description", cleanDescription)
                .AddField("Steam API Link", $"steam://url/CommunityFilePage/{mod.PublishedFileId}")
                .WithThumbnailUrl(user.avatarmedium)
                .WithImageUrl(mod.Preview_Url)
                .Build();

            await ctx.RespondAsync(embed: modEmbed);
        }

        private JObject Query(string content, string key, byte resultsCap = 5)
        {
            if (resultsCap > 20)
            {
                resultsCap = 20;
            }

            var request = string.Format(query, key, resultsCap, content, 3.ToString());
            var req = WebRequest.CreateHttp(request);
            var reader = new StreamReader(req.GetResponse().GetResponseStream());
            return JObject.Parse(reader.ReadToEnd());
        }

        [Command("wshopsearch")]
        [Description("Searches the steam workshop for content.")]
        public async Task Search(CommandContext ctx,
            [RemainingText] [Description("The search query.")]
            string localQuery)
        {
            var result = Query(localQuery, AuthResources.SteamApiKey);
            try
            {
                if (result["response"]["total"].Value<int>() == 0)
                {
                    await ctx.RespondAsync("No results.");
                }
                else
                {
                    var embedBuilder = new DiscordEmbedBuilder();
                    embedBuilder.WithColor(DiscordColor.DarkBlue);
                    embedBuilder.WithTitle($"Results for '{localQuery}'");
                    embedBuilder.WithDescription(
                        "Total results: " + result["response"]["publishedfiledetails"]?.Where(token =>
                                token != null && token["tags"].Any() &&
                                token["tags"].All(tag =>
                                    tag["tag"]?.ToString() != "Translation" && tag["tag"]?.ToString() != "Scenario"))
                            .Count());
                    foreach (var item in result["response"]["publishedfiledetails"]
                        .OrderByDescending(token => token["time_updated"]))
                    {
                        //embedBuilder.AddField(item["title"]?.ToString(),
                        //    $"**Views**: {item["views"]}\n" +
                        //    $"**Subs**: {item["subscriptions"]}\n" +
                        //    $"**Favs**: {item["favorited"]}\n" + 
                        //    $"**ID**: {item["publishedfileid"]}\n" +
                        //    $"[Link](http://steamcommunity.com/sharedfiles/filedetails/?id={item["publishedfileid"]})\n",
                        //    true);
                        var tags = string.Empty;
                        var isTranslation = false;
                        if (item["tags"] != null && item["tags"].Any())
                        {
                            tags = "\nTags: ";
                            var stringTags = new List<string>();
                            foreach (var jToken in item["tags"].OrderByDescending(token => token["tag"]))
                            {
                                if (jToken["tag"]?.ToString() == "Mod" || jToken["tag"]?.ToString() == "Scenario")
                                {
                                    continue;
                                }

                                if (jToken["tag"]?.ToString() == "Translation")
                                {
                                    isTranslation = true;
                                    continue;
                                }

                                stringTags.Add(jToken["tag"]?.ToString());
                            }

                            tags += string.Join(", ", stringTags);
                        }

                        if (isTranslation)
                        {
                            continue;
                        }

                        embedBuilder.AddField(
                            $"{item["title"]}\n",
                            $"Updated: {UnixTimeStampToDateTime((double)item["time_updated"]):yyyy-MM-dd}\n" +
                            $"Views: {item["views"]}\n" +
                            $"Subs: {item["subscriptions"]}" +
                            tags +
                            $"\n[{item["publishedfileid"]}](http://steamcommunity.com/sharedfiles/filedetails/?id={item["publishedfileid"]})",
                            true);
                    }

                    await ctx.RespondAsync(embed: embedBuilder.Build());
                }
            }
            catch (Exception e)
            {
                await ctx.RespondAsync(
                    "Oops! The Steam API doesn\'t seem to want to cooperate right now. Try again later :(");
                if (!(e is ArgumentNullException || e is NullReferenceException))
                {
                    throw;
                }
            }
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            var dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
    }
}