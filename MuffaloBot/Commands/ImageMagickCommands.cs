﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using ImageMagick;
using MuffaloBot.Attributes;

namespace MuffaloBot.Commands
{
    [Cooldown(1, 60, CooldownBucketType.User)]
    [RequireChannelInGuild("RimWorld", "bot-commands")]
    public class ImageMagickCommands
    {
        [Command("swirl")]
        [Description("Applies a swirl effect to an image. The image can be provided as an attachment or a link.")]
        public async Task ImageMagickDistort(CommandContext ctx,
            [Description("Optional. The link to the image you want to apply the effect to.")]
            string link = null)
        {
            await DoImageMagickCommand(ctx, ImageEditMode.Swirl, link).ConfigureAwait(false);
        }

        [Command("wonky")]
        [Description(
            "Shifts and rescales the image in weird ways. The image can be provided as an attachment or a link.")]
        public async Task ImageMagickWonky(CommandContext ctx,
            [Description("Optional. The link to the image you want to apply the effect to.")]
            string link = null)
        {
            await DoImageMagickCommand(ctx, ImageEditMode.Rescale, link).ConfigureAwait(false);
        }

        [Command("wave")]
        [Description("Applies a wave effect to an image. The image can be provided as an attachment or a link.")]
        public async Task ImageMagickWave(CommandContext ctx,
            [Description("Optional. The link to the image you want to apply the effect to.")]
            string link = null)
        {
            await DoImageMagickCommand(ctx, ImageEditMode.Wave, link).ConfigureAwait(false);
        }

        [Command("implode")]
        [Description("Applies an implosion effect to an image. The image can be provided as an attachment or a link.")]
        public async Task ImageMagickImplode(CommandContext ctx,
            [Description("Optional. The link to the image you want to apply the effect to.")]
            string link = null)
        {
            await DoImageMagickCommand(ctx, ImageEditMode.Implode, link).ConfigureAwait(false);
        }

        [Command("jpeg")]
        [Description("Compresses an image. A lot. The image can be provided as an attachment or a link.")]
        public async Task ImageMagickJPEG(CommandContext ctx,
            [Description("Optional. The link to the image you want to apply the effect to.")]
            string link = null)
        {
            await DoImageMagickCommand(ctx, ImageEditMode.JPEG, link).ConfigureAwait(false);
        }

        [Command("moarjpeg")]
        [Description(
            "Compresses an image. Even more than `!jpeg`. The image can be provided as an attachment or a link.")]
        public async Task ImageMagickMoreJPEG(CommandContext ctx,
            [Description("Optional. The link to the image you want to apply the effect to.")]
            string link = null)
        {
            await DoImageMagickCommand(ctx, ImageEditMode.MoreJPEG, link).ConfigureAwait(false);
        }

        [Command("mostjpeg")]
        [Description(
            "Compresses an image. So much that that will become smaller than a paperclip. The image can be provided as an attachment or a link.")]
        public async Task ImageMagickMostJPEG(CommandContext ctx,
            [Description("Optional. The link to the image you want to apply the effect to.")]
            string link = null)
        {
            await DoImageMagickCommand(ctx, ImageEditMode.MostJPEG, link).ConfigureAwait(false);
        }

        private async Task DoImageMagickCommand(CommandContext ctx, ImageEditMode mode, string link)
        {
            await ctx.TriggerTypingAsync().ConfigureAwait(false);
            string attachmentUrl = null;
            if (!string.IsNullOrWhiteSpace(ctx.RawArgumentString) && link != null &&
                Uri.TryCreate(link, UriKind.Absolute, out _))
            {
                attachmentUrl = link;
            }
            else
            {
                var messages = await ctx.Channel.GetMessagesAsync(10);
                foreach (var discordMessage in messages)
                {
                    if (discordMessage.Attachments.Count == 0)
                    {
                        continue;
                    }

                    attachmentUrl = discordMessage.Attachments[0].Url;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(attachmentUrl))
            {
                var client = new HttpClient();
                byte[] buffer;
                try
                {
                    buffer = await client.GetByteArrayAsync(attachmentUrl);
                }
                catch (HttpRequestException)
                {
                    await ctx.RespondAsync("Error connecting to image link.");
                    return;
                }

                if (attachmentUrl.EndsWith(".gif"))
                {
                    await DoImageMagickCommandForGif(ctx, buffer, mode);
                }
                else
                {
                    await DoImageMagickCommandForStillImage(ctx, buffer, mode);
                }
            }
            else
            {
                await ctx.RespondAsync("No image found.");
            }
        }

        private async Task DoImageMagickCommandForGif(CommandContext ctx, byte[] buffer, ImageEditMode mode)
        {
            if (mode == ImageEditMode.Rescale)
            {
                await ctx.RespondAsync(
                    "This mode is not supported for gifs since it is slow and often dramatically increases gif size");
                return;
            }

            MagickImageCollection image;
            try
            {
                image = new MagickImageCollection(buffer);
            }
            catch (MagickMissingDelegateErrorException)
            {
                await ctx.RespondAsync("Image format not recognised.");
                return;
            }

            int originalWidth = image[0].Width, originalHeight = image[0].Height;
            if (originalHeight * originalWidth > 1000000)
            {
                await ctx.RespondAsync(
                    $"Gif exceeds maximum size of 1000000 pixels (Actual size: {originalHeight * originalWidth})");
                return;
            }

            if (image.Count > 100)
            {
                await ctx.RespondAsync($"Gif exceeds maximum frame count of 100 pixels (Actual count: {image.Count})");
                return;
            }

            image.Coalesce();
            long rawLength;
            await using (var stream = new MemoryStream())
            {
                image.Write(stream);
                rawLength = stream.Length;
            }

            var exceed = rawLength / 4194304d;
            double rescale = 1f;
            if (exceed > 1.0)
            {
                rescale = Math.Sqrt(exceed);
            }

            await ctx.TriggerTypingAsync();
            foreach (var frame in image)
            {
                if (rescale > 1f)
                {
                    if (rescale > 2f)
                    {
                        frame.AdaptiveResize((int)(frame.Width / rescale), (int)(frame.Height / rescale));
                    }
                    else
                    {
                        frame.Resize((int)(frame.Width / rescale), (int)(frame.Height / rescale));
                    }
                }

                DoMagic(mode, frame, originalWidth, originalHeight);
            }

            await ctx.TriggerTypingAsync();
            image.OptimizeTransparency();
            await using (Stream stream = new MemoryStream())
            {
                image.Write(stream);
                stream.Seek(0, SeekOrigin.Begin);
                await ctx.RespondWithFileAsync(stream, "magic.gif");
            }
        }

        private async Task DoImageMagickCommandForStillImage(CommandContext ctx, byte[] buffer, ImageEditMode mode)
        {
            MagickImage image;
            try
            {
                image = new MagickImage(buffer);
            }
            catch (MagickMissingDelegateErrorException)
            {
                await ctx.RespondAsync("Image format not recognised.");
                return;
            }

            int originalWidth = image.Width, originalHeight = image.Height;
            if (originalHeight * originalWidth > 2250000)
            {
                await ctx.RespondAsync(
                    $"Image exceeds maximum size of 2250000 pixels (Actual size: {originalHeight * originalWidth})");
                return;
            }

            // Do magic
            var exceed = buffer.Length / 8388608d;
            double rescale = 1f;
            if (exceed > 1.0)
            {
                rescale = 1.0 / Math.Sqrt(exceed);
            }

            if (rescale < 1f)
            {
                if (rescale < 0.5f)
                {
                    image.AdaptiveResize((int)(image.Width * rescale), (int)(image.Height * rescale));
                }
                else
                {
                    image.Resize((int)(image.Width * rescale), (int)(image.Height * rescale));
                }
            }

            DoMagic(mode, image, originalWidth, originalHeight);
            await using Stream stream = new MemoryStream();
            if (mode == ImageEditMode.JPEG || mode == ImageEditMode.MoreJPEG || mode == ImageEditMode.MostJPEG)
            {
                image.Write(stream, MagickFormat.Jpeg);
            }
            else
            {
                image.Write(stream);
            }

            stream.Seek(0, SeekOrigin.Begin);
            if (mode == ImageEditMode.JPEG || mode == ImageEditMode.MoreJPEG || mode == ImageEditMode.MostJPEG)
            {
                await ctx.RespondWithFileAsync(stream, "magic.jpeg");
            }
            else
            {
                await ctx.RespondWithFileAsync(stream, "magic.png");
            }
        }

        private void DoMagic(ImageEditMode mode, IMagickImage image, int originalWidth, int originalHeight)
        {
            switch (mode)
            {
                case ImageEditMode.Swirl:
                    image.Swirl(360);
                    break;
                case ImageEditMode.Rescale:
                    image.LiquidRescale(image.Width / 2, image.Height / 2);
                    image.LiquidRescale(image.Width * 3 / 2, image.Height * 3 / 2);
                    image.Resize(originalWidth, originalHeight);
                    break;
                case ImageEditMode.Wave:
                    image.BackgroundColor = MagickColor.FromRgb(0, 0, 0);
                    image.Wave(image.Interpolate, 10.0, 150.0);
                    break;
                case ImageEditMode.Implode:
                    image.Implode(0.5d, PixelInterpolateMethod.Average);
                    break;
                case ImageEditMode.JPEG:
                    image.Quality = 10;
                    break;
                case ImageEditMode.MoreJPEG:
                    image.Quality = 5;
                    break;
                case ImageEditMode.MostJPEG:
                    image.Quality = 1;
                    break;
            }
        }

        private enum ImageEditMode
        {
            Swirl,
            Rescale,
            Wave,
            Implode,
            JPEG,
            MoreJPEG,
            MostJPEG
        }
    }
}