using System;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Exceptions;

namespace MuffaloBot.Modules
{
    public class ExceptionHandlerModule : BaseModule
    {
        public async Task HandleClientError(CommandErrorEventArgs e)
        {
            if (e.Exception is CommandNotFoundException || e.Exception is UnauthorizedException ||
                e.Exception.Message.StartsWith("Could not convert specified value to given type."))
            {
                return;
            }

            if (e.Exception is ChecksFailedException)
            {
                await e.Context.RespondAsync("You can't do that. >:V");
                return;
            }

            await HandleClientError(e.Exception, e.Context.Client, "Command " + (e.Command?.Name ?? "unknown"));
        }

        public Task HandleClientError(ClientErrorEventArgs e)
        {
            return HandleClientError(e.Exception, (DiscordClient)e.Client, "Event " + e.EventName);
        }

        public async Task HandleClientError(Exception e, DiscordClient client, string action)
        {
            var builder = new DiscordEmbedBuilder();
            builder.WithTitle("Unhandled exception");
            builder.WithDescription($"Action: {action}\n```\n{e}```");
            builder.WithColor(DiscordColor.Red);
            DiscordChannel channel = await client.CreateDmAsync(client.CurrentApplication.Owner);
            await client.SendMessageAsync(channel, embed: builder.Build());
        }

        protected override void Setup(DiscordClient client)
        {
            Client = client;
            client.ClientErrored += HandleClientError;
            client.GetCommandsNext().CommandErrored += HandleClientError;
        }
    }
}