﻿using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using MuffaloBot.Modules;

namespace MuffaloBot.Commands
{
    public class EvalGobals
    {
        public CommandContext ctx;
    }

    public class Meta
    {
        [Command("mbhelp")]
        public Task ShowHelpAsync(CommandContext ctx, params string[] command)
        {
            return ctx.CommandsNext.DefaultHelpAsync(ctx, command);
        }

        [Command("about")]
        [Description("Shows info about the bot.")]
        public Task About(CommandContext ctx)
        {
            var embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.WithTitle("About MuffaloBot");
            embedBuilder.WithUrl("https://github.com/Zero747/MuffaloBot");
            embedBuilder.WithDescription(@"Contributors: spdskatr
Library: [DSharpPlus](https://dsharpplus.emzi0767.com/) (.NET)
Other libraries: [Magick.NET](https://github.com/dlemstra/Magick.NET) Wrapper for [ImageMagick](http://www.imagemagick.org/) (.NET)
Hosted by: Mehni
GitHub Repository: https://github.com/spdskatr/MuffaloBot
Beta testing discord: https://discord.gg/6MHVepE
This bot account will not have an invite link. It is exclusive to the RimWorld discord.");
            embedBuilder.WithColor(DiscordColor.Azure);
            return ctx.RespondAsync(embed: embedBuilder.Build());
        }

        [Command("update")]
        [RequirePermissions(Permissions.ViewAuditLog)]
        [Hidden]
        public async Task UpdateAsync(CommandContext ctx)
        {
            await RunUpdateAsync(ctx).ConfigureAwait(false);
        }

        private async Task RunUpdateAsync(CommandContext ctx)
        {
            var message = await ctx.RespondAsync("```\nUpdating... [          ] 0%\n```");
            await ctx.Client.GetModule<JsonDataModule>().ReloadDataAsync();
            await message.ModifyAsync("```\nUpdating... [████░     ] 42%\n```");
            await ctx.Client.GetModule<XmlDatabaseModule>().UpdateDatabaseAsync();
            if (DateTime.Now.Millisecond < 500)
            {
                await message.ModifyAsync("```\nUpdating... [██████████] 99.999999%\n```");
                await Task.Delay(3000);
                await message.ModifyAsync("```\nUpdating... [**********] ERROR!\n```");
                await Task.Delay(3000);
                await message.ModifyAsync("```\nUpdating... [██████████] jk <3\n```");
                await Task.Delay(1000);
            }

            await message.ModifyAsync("```\nUpdating... [██████████] Done!\n```");
        }

        [Command("version")]
        [Hidden]
        public Task GetVersionAsync(CommandContext ctx)
        {
            var name = Assembly.GetExecutingAssembly().GetName();
            return ctx.RespondAsync($"{name.Name} Version {name.Version}");
        }

        [Command("status")]
        [RequireOwner]
        [Hidden]
        public async Task SetStatusAsync(CommandContext ctx, string status)
        {
            await ctx.Client.UpdateStatusAsync(new DiscordGame(status));
            await ctx.RespondAsync(DiscordEmoji.FromName(ctx.Client, ":ok_hand:").ToString());
        }

        [Command("die")]
        [RequireOwner]
        [Hidden]
        public async Task DieAsync(CommandContext ctx)
        {
            await ctx.RespondAsync("Restarting...");
            await ctx.Client.DisconnectAsync();
            Environment.Exit(0);
        }

        [Command("crash")]
        [RequireOwner]
        [Hidden]
        public Task CrashAsync(CommandContext ctx)
        {
            throw new Exception("oops.");
        }

        [Command("roleid")]
        [Hidden]
        public Task GetRoleAsync(CommandContext ctx, DiscordRole role)
        {
            return ctx.RespondAsync(role.Id.ToString());
        }

        [Command("isimmortal")]
        [RequireOwner]
        [Hidden]
        public Task IsImmortalAsync(CommandContext ctx)
        {
            return ctx.RespondAsync(Environment.GetCommandLineArgs().Any(s => s == "-immortal").ToString());
        }

        [Command("eval")]
        [RequireOwner]
        [Hidden]
        public async Task EvalAsync(CommandContext ctx, [RemainingText] string code)
        {
            await ctx.TriggerTypingAsync().ConfigureAwait(false);
            var actualCode = code.TrimStart('`', 'c', 's', 'h', 'a', 'r', 'p').TrimEnd('`');
            var options = ScriptOptions.Default.WithImports("System", "System.Collections.Generic",
                    "System.Diagnostics", "System.Linq", "System.Net.Http", "System.Reflection", "System.Text",
                    "System.Text.RegularExpressions", "System.Threading.Tasks", "DSharpPlus", "DSharpPlus.CommandsNext",
                    "DSharpPlus.Entities", "DSharpPlus.EventArgs", "DSharpPlus.Exceptions", "MuffaloBot",
                    "MuffaloBot.Commands")
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(xa => !xa.IsDynamic && !string.IsNullOrWhiteSpace(xa.Location)));
            Script script = CSharpScript.Create(actualCode, options, typeof(EvalGobals));

            Exception ex;
            ScriptState state = null;
            try
            {
                state = await script.RunAsync(new EvalGobals { ctx = ctx }).ConfigureAwait(false);
                ex = state.Exception;
            }
            catch (Exception e)
            {
                ex = e;
            }

            if (ex != null)
            {
                await ctx.RespondAsync($"**Error** ```{ex}```");
            }
            else
            {
                await ctx.RespondAsync($"Result: ```{state.ReturnValue ?? "(null)"}```");
            }
        }

        [Command("dontask")]
        [Hidden]
        public Task DontAsk(CommandContext ctx)
        {
            var embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.WithTitle("**Don't ask to ask, just ask**");
            embedBuilder.WithUrl("http://sol.gfxile.net/dontask.html");
            embedBuilder.WithDescription(
                @"If someone is around who can help, it's better if they can see the full question.
So, instead of
```Is someone around who can help me```
ask
```How do I reference my custom class in xml ?```
This gets you a response faster and prevents wasting time
");
            embedBuilder.WithColor(DiscordColor.Azure);
            return ctx.RespondAsync(embed: embedBuilder.Build());
        }
    }
}