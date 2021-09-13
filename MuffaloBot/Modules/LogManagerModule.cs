using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;

namespace MuffaloBot.Modules
{
    public class LogManagerModule : BaseModule
    {
        private static readonly MethodInfo memberwiseCloneMethod =
            typeof(DiscordMessage).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly DiscordMessage[] discordMessageCache = new DiscordMessage[1000];

        private int currentIndex;

        protected override void Setup(DiscordClient client)
        {
            Client = client;
            client.MessageCreated += OnReceiveDiscordCreateLog;
            client.MessageDeleted += OnReceiveDiscordDeleteLog;
            client.MessageUpdated += OnReceiveDiscordModifyLog;
        }

        private int FindIndexOfIdInCache(ulong id)
        {
            for (var i = currentIndex; i < discordMessageCache.Length; i++)
            {
                if (discordMessageCache[i] != null && discordMessageCache[i].Id == id)
                {
                    return i;
                }
            }

            for (var i = 0; i < currentIndex; i++)
            {
                if (discordMessageCache[i] != null && discordMessageCache[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        private DiscordMessage ShallowCopyOf(DiscordMessage message)
        {
            return (DiscordMessage)memberwiseCloneMethod.Invoke(message, Array.Empty<object>());
        }

        private Task OnReceiveDiscordCreateLog(MessageCreateEventArgs e)
        {
            return PushMessage(e.Message);
        }

        private async Task PushMessage(DiscordMessage message)
        {
            await Task.Run(() =>
                discordMessageCache[currentIndex = ++currentIndex % discordMessageCache.Length] =
                    ShallowCopyOf(message)).ConfigureAwait(false);
        }

        private async Task OnReceiveDiscordDeleteLog(MessageDeleteEventArgs e)
        {
            if (e.Message.Channel?.Name == "logs" || (e.Message.Author?.IsBot ?? true))
            {
                return;
            }

            int ind;
            if ((ind = FindIndexOfIdInCache(e.Message.Id)) != -1)
            {
                await NotifyDeleteAsync(discordMessageCache[ind], e.Guild);
                discordMessageCache[ind] = null;
            }
            else
            {
                await NotifyDeleteAsync(e.Message, e.Guild);
            }
        }

        private async Task OnReceiveDiscordModifyLog(MessageUpdateEventArgs e)
        {
            if (e.Message.Channel.Name == "logs" || e.Message == null || string.IsNullOrEmpty(e.Message.Content) ||
                e.Message.Author.IsBot)
            {
                return;
            }

            int ind;
            if ((ind = FindIndexOfIdInCache(e.Message.Id)) != -1)
            {
                var before = discordMessageCache[ind];
                await NotifyModifyAsync(before, e.Message, e.Guild);
                discordMessageCache[ind] =
                    (DiscordMessage)memberwiseCloneMethod.Invoke(e.Message, Array.Empty<object>());
            }
            else
            {
                await NotifyModifyAsync(null, e.Message, e.Guild);
                await PushMessage(e.Message);
            }
        }

        private async Task NotifyDeleteAsync(DiscordMessage message, DiscordGuild guild)
        {
            if (guild == null)
            {
                return;
            }

            var content = message.Content ?? "(Message too old...)";
            if (content.Length == 0)
            {
                content = "(Empty)";
            }

            var channel = (await guild.GetChannelsAsync()).First(c => c.Name == "logs");
            var permissions = channel.PermissionsFor(guild.CurrentMember);
            if ((permissions & Permissions.SendMessages) != 0)
            {
                var embedBuilder = MakeDeleteMessageEmbed(message, content);
                await channel.SendMessageAsync(embed: embedBuilder.Build()).ConfigureAwait(false);
            }
        }

        private async Task NotifyModifyAsync(DiscordMessage before, DiscordMessage after, DiscordGuild guild)
        {
            var content = before?.Content ?? "(Message too old...)";
            if (content.Length == 0)
            {
                content = "(Empty)";
            }

            if (content == after.Content)
            {
                return;
            }

            var channel = (await guild.GetChannelsAsync()).First(c => c.Name == "logs");
            var permissions = channel.PermissionsFor(guild.CurrentMember);
            if ((permissions & Permissions.SendMessages) != 0)
            {
                var embedBuilderList = MakeModifyMessageEmbed(after, content);
                foreach (var discordEmbedBuilder in embedBuilderList.Reverse())
                {
                    await channel.SendMessageAsync(embed: discordEmbedBuilder.Build()).ConfigureAwait(false);
                }
            }
        }

        private static DiscordEmbedBuilder MakeDeleteMessageEmbed(DiscordMessage message, string content)
        {
            content = content.Replace("`", "'");
            var embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.WithTitle("Message Deleted");
            embedBuilder.WithAuthor($"{message.Author.Username} #{message.Author.Discriminator}", null,
                message.Author.AvatarUrl);
            embedBuilder.WithColor(DiscordColor.Red);
            embedBuilder.WithDescription($"```\n{content}```");
            embedBuilder.AddField("ID", message.Id.ToString(), true);
            embedBuilder.AddField("Author ID", message.Author.Id.ToString(), true);
            embedBuilder.AddField("Channel", "#" + message.Channel.Name, true);
            embedBuilder.AddField("Timestamp (UTC)", message.Timestamp.ToUniversalTime().ToString(), true);
            var attachments = message.Attachments;
            for (var i = 0; i < attachments.Count; i++)
            {
                embedBuilder.AddField($"Attachment {i + 1}",
                    $"{attachments[i].FileName} ({attachments[i].FileSize}) {attachments[i].Url}", true);
            }

            return embedBuilder;
        }

        private static DiscordEmbedBuilder[] MakeModifyMessageEmbed(DiscordMessage after, string content)
        {
            content = content.Replace("`", "'");
            var afterContent = after.Content?.Replace("`", "'");
            if (string.IsNullOrEmpty(afterContent))
            {
                afterContent = "(Empty)";
            }

            var title = "Message Modified";
            var description = $"Before```\n{content}```After```\n{afterContent}```";

            var embedBuilderList = new DiscordEmbedBuilder[1];
            var embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.WithAuthor($"{after.Author.Username} #{after.Author.Discriminator}", null,
                after.Author.AvatarUrl);
            embedBuilder.WithColor(DiscordColor.Yellow);
            if (description.Length >= 2048)
            {
                embedBuilder.WithTitle("Message Modified (1/2)");
                embedBuilderList = new DiscordEmbedBuilder[2];
                embedBuilder.WithDescription($"Before```\n{content}```");
                embedBuilderList[1] = embedBuilder;
                embedBuilder = new DiscordEmbedBuilder();
                title = "Message Modified (2/2)";
                description = $"After```\n{afterContent}```";
                embedBuilder.WithColor(DiscordColor.Yellow);
            }

            embedBuilder.WithTitle(title);
            embedBuilder.WithDescription(description);
            embedBuilder.AddField("ID", after.Id.ToString(), true);
            embedBuilder.AddField("Author ID", after.Author.Id.ToString(), true);
            embedBuilder.AddField("Channel", "#" + after.Channel.Name, true);
            embedBuilder.AddField("Timestamp (UTC)", after.Timestamp.ToUniversalTime().ToString(), true);
            var attachments = after.Attachments;
            for (var i = 0; i < attachments.Count; i++)
            {
                embedBuilder.AddField($"Attachment {i + 1}",
                    $"{attachments[i].FileName} ({attachments[i].FileSize}) {attachments[i].Url}", true);
            }

            embedBuilderList[0] = embedBuilder;

            return embedBuilderList;
        }
    }
}