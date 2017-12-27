﻿using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.Entities;
using MuffaloBotNetFramework2.DiscordComponent;
using MuffaloBotNetFramework2;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Net.Http;
using System.IO;
using Ionic.Zip;

namespace MuffaloBotNetFramework2.CommandsModules
{
    [MuffaloBotCommandsModule]
    class Meta
    {
        [Command("version"), Hidden]
        public Task GetVersion(CommandContext ctx)
        {
            AssemblyName name = Assembly.GetExecutingAssembly().GetName();
            return ctx.RespondAsync($"{name.Name} Version {name.Version}");
        }
        [Command("status"), RequireOwner, Hidden]
        public async Task SetStatus(CommandContext ctx, string status)
        {
            await ctx.Client.UpdateStatusAsync(new DiscordGame(status));
            await ctx.RespondAsync(DiscordEmoji.FromName(ctx.Client, ":ok_hand:").ToString());
        }
        [Command("die"), RequireOwner]
        public async Task Die(CommandContext ctx)
        {
            await ctx.RespondAsync("Restarting...");
            await ctx.Client.DisconnectAsync();
        }
        [Command("update"), RequireOwner]
        public async Task LoadNewVersion(CommandContext ctx, [RemainingText] string url)
        {
            await ctx.RespondAsync("Updating...");
            HttpClient client = new HttpClient();
            byte[] buffer = await client.GetByteArrayAsync(url).ConfigureAwait(false);
            using (ZipFile file = ZipFile.Read(new MemoryStream(buffer)))
            {
                file.ExtractAll("..", ExtractExistingFileAction.OverwriteSilently);
            }
            await ctx.RespondAsync("Success!");
            await Die(ctx);
        }
        [Command("exception"), RequireOwner, Hidden]
        public Task Crash(CommandContext ctx)
        {
            throw new Exception("oops.");
        }
        [Command("roleid")]
        public Task GetRole(CommandContext ctx, DiscordRole role)
        {
            return ctx.RespondAsync(role.Id.ToString());
        }
    }
}
