using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using MuffaloBot.Modules;

namespace MuffaloBot.Commands
{
    public class XPathCommands
    {
        [Command("xpath")]
        [Description(
            "Searches the RimWorld Xml database for xml nodes that match the xpath. **Examples:**\n`!xpath Defs/ThingDef[defName=\"Steel\"]/description`\n`!xpath Defs/ThingDef[@Name=\"BuildingBase\"]`\n`!xpath //*`")]
        public async Task XPathCommand(CommandContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.RawArgumentString))
            {
                return;
            }

            try
            {
                await ctx.RespondAsync(ctx.Client.GetModule<XmlDatabaseModule>()
                    .GetSummaryForNodeSelection(ctx.RawArgumentString));
            }
            catch (XPathException ex)
            {
                await ctx.RespondAsync("Invalid XPath! Error: " + ex.Message);
            }
        }

        [Command("iteminfo")]
        [Description("Displays the stats for an item or building in RimWorld.")]
        public async Task InfoCommand(CommandContext ctx,
            [RemainingText] [Description("The name of the item.")]
            string itemName)
        {
            if (!new Regex("^[a-zA-Z0-9\\-_ ]*$").IsMatch(itemName))
            {
                await ctx.RespondAsync("Invalid name! Only letters, numbers, spaces, underscores or dashes allowed.");
                return;
            }

            var xmlDatabase = ctx.Client.GetModule<XmlDatabaseModule>();
            var results =
                xmlDatabase
                    .SelectNodesByXpath(
                        $"Defs/ThingDef[translate(defName,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')=\"{itemName.ToLower()}\"]")
                    .Concat(xmlDatabase.SelectNodesByXpath(
                        $"Defs/ThingDef[translate(label,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')=\"{itemName.ToLower()}\"]"))
                    .Concat(xmlDatabase.SelectNodesByXpath($"Defs/ThingDef[contains(label, \"{itemName.ToLower()}\")]"))
                    .Distinct();
            var builder = new DiscordEmbedBuilder();
            builder.WithColor(DiscordColor.Azure);
            var didYouMean = new List<string>();
            foreach (var item in results)
            {
                if (string.IsNullOrEmpty(builder.Title))
                {
                    builder.WithTitle(
                        $"Info for item \"{item.Value["label"]?.InnerXml.CapitalizeFirst()}\" (defName: {item.Value["defName"]?.InnerXml})");
                    builder.WithDescription(item.Value["description"]?.InnerXml ?? "No description.");
                    if (InnerXmlOfPathFromDef(xmlDatabase, item.Value, "category") == "Item")
                    {
                        var itemStatsBuilder = new StringBuilder();
                        itemStatsBuilder.AppendLine("Stack limit: " +
                                                    InnerXmlOfPathFromDef(xmlDatabase, item.Value, "stackLimit", "1"));
                        itemStatsBuilder.AppendLine("Automatically haulable: " +
                                                    InnerXmlOfPathFromDef(xmlDatabase, item.Value, "alwaysHaulable",
                                                        "false"));
                        if (item.Value["stuffProps"] != null)
                        {
                            itemStatsBuilder.AppendLine("Small volume: " +
                                                        InnerXmlOfPathFromDef(xmlDatabase, item.Value, "smallVolume",
                                                            "false"));
                        }

                        builder.AddField("Item stats", itemStatsBuilder.ToString());
                    }

                    var stringBuilder = new StringBuilder();
                    AllStatBasesForThingDef(xmlDatabase, item.Value, stringBuilder, new HashSet<string>());
                    var str = stringBuilder.ToString();
                    if (!string.IsNullOrEmpty(str))
                    {
                        builder.AddField("Base stats", str);
                    }

                    if (item.Value["stuffProps"] == null)
                    {
                        continue;
                    }

                    stringBuilder = new StringBuilder();
                    var color = builder.Color;
                    AllStuffPropertiesForThingDef(xmlDatabase, item.Value, stringBuilder, new HashSet<string>(),
                        ref color);
                    builder.Color = color;
                    var result = stringBuilder.ToString();
                    if (!string.IsNullOrEmpty(result))
                    {
                        builder.AddField("Stuff properties - General", result);
                    }

                    stringBuilder = new StringBuilder();
                    AllStatFactorsForThingDef(xmlDatabase, item.Value, stringBuilder, new HashSet<string>());
                    result = stringBuilder.ToString();
                    if (!string.IsNullOrEmpty(result))
                    {
                        builder.AddField("Stat modifiers - Factors", result);
                    }

                    stringBuilder = new StringBuilder();
                    AllStatOffsetsForThingDef(xmlDatabase, item.Value, stringBuilder, new HashSet<string>());
                    result = stringBuilder.ToString();
                    if (!string.IsNullOrEmpty(result))
                    {
                        builder.AddField("Stat modifiers - Offsets", result);
                    }
                }
                else
                {
                    didYouMean.Add($"`{item.Value["defName"]?.InnerXml}`");
                }
            }

            var didYouMeanStr = string.Join(", ", didYouMean);
            if (!string.IsNullOrEmpty(didYouMeanStr))
            {
                builder.AddField("Did you mean", didYouMeanStr);
            }

            if (string.IsNullOrEmpty(builder.Title))
            {
                await ctx.RespondAsync("No results.");
            }
            else
            {
                await ctx.RespondAsync(embed: builder.Build());
            }
        }

        private void AllStuffPropertiesForThingDef(XmlDatabaseModule xmlDatabase, XmlNode node,
            StringBuilder stringBuilder, HashSet<string> foundProps, ref DiscordColor color)
        {
            var nodeList = node["stuffProps"]?.ChildNodes;
            for (var i = 0; i < (nodeList?.Count ?? 0); i++)
            {
                if (nodeList?[i] != null && nodeList[i].NodeType == XmlNodeType.Element &&
                    !foundProps.Contains(nodeList[i].Name))
                {
                    switch (nodeList[i].Name)
                    {
                        case "color":
                            var str = nodeList[i].InnerXml;
                            str = str.TrimStart('(', 'R', 'G', 'B', 'A');
                            str = str.TrimEnd(new[] { ')' });
                            var array2 = str.Split(new[] { ',' });
                            var f1 = float.Parse(array2[0]);
                            var f2 = float.Parse(array2[1]);
                            var f3 = float.Parse(array2[2]);
                            if (f1 > 1f || f2 > 1f || f3 > 1f)
                            {
                                color = new DiscordColor((byte)f1, (byte)f2, (byte)f3);
                            }
                            else
                            {
                                color = new DiscordColor(f1, f2, f3);
                            }

                            stringBuilder.AppendLine($"Color: {nodeList[i].InnerXml}");
                            foundProps.Add(nodeList[i].Name);
                            break;
                        default:
                            if (nodeList[i].ChildNodes.Cast<XmlNode>().All(xml => xml.NodeType == XmlNodeType.Text) &&
                                !string.IsNullOrEmpty(nodeList[i].InnerXml))
                            {
                                stringBuilder.AppendLine(
                                    $"{nodeList[i].Name.MakeFieldSemiReadable()}: {nodeList[i].InnerXml}");
                                foundProps.Add(nodeList[i].Name);
                            }

                            break;
                    }
                }
            }

            var parent = node.Attributes?["ParentName"];
            if (parent == null)
            {
                return;
            }

            var xmlNode = xmlDatabase.SelectNodesByXpath($"Defs/ThingDef[@Name=\"{parent.InnerXml}\"]")
                .FirstOrDefault().Value;
            AllStuffPropertiesForThingDef(xmlDatabase, xmlNode, stringBuilder, foundProps, ref color);
        }

        private void AllStatBasesForThingDef(XmlDatabaseModule xmlDatabase, XmlNode node, StringBuilder stringBuilder,
            HashSet<string> foundStats)
        {
            XmlNode statBasesNode = node["statBases"];
            XmlNode xmlNode;
            if (statBasesNode != null)
            {
                foreach (XmlNode child in statBasesNode.ChildNodes)
                {
                    if (child.NodeType != XmlNodeType.Element)
                    {
                        continue;
                    }

                    var statDefName = child.Name;
                    if (foundStats.Contains(statDefName))
                    {
                        continue;
                    }

                    foundStats.Add(statDefName);

                    xmlNode = xmlDatabase.SelectNodesByXpath($"Defs/StatDef[defName=\"{statDefName}\"]/label")
                        .FirstOrDefault().Value;
                    if (xmlNode != null)
                    {
                        statDefName = xmlNode.InnerXml;
                    }

                    stringBuilder.AppendLine($"{statDefName.CapitalizeFirst()}: {child.InnerXml}");
                }
            }

            var attributes = node.Attributes;
            var parentAttr = attributes?["ParentName"];
            if (parentAttr == null)
            {
                return;
            }

            xmlNode = xmlDatabase.SelectNodesByXpath($"Defs/ThingDef[@Name=\"{parentAttr.InnerXml}\"]")
                .FirstOrDefault().Value;
            AllStatBasesForThingDef(xmlDatabase, xmlNode, stringBuilder, foundStats);
        }

        private void AllStatFactorsForThingDef(XmlDatabaseModule xmlDatabase, XmlNode node, StringBuilder stringBuilder,
            HashSet<string> foundStats)
        {
            XmlNode xmlNode;
            if (node["stuffProps"] != null)
            {
                XmlNode statFactors = node["stuffProps"]["statFactors"];
                if (statFactors != null)
                {
                    foreach (XmlNode child in statFactors.ChildNodes)
                    {
                        if (child.NodeType != XmlNodeType.Element)
                        {
                            continue;
                        }

                        var statDefName = child.Name;
                        if (foundStats.Contains(statDefName))
                        {
                            continue;
                        }

                        foundStats.Add(statDefName);

                        xmlNode = xmlDatabase
                            .SelectNodesByXpath($"Defs/StatDef[defName=\"{statDefName}\"]/label").FirstOrDefault()
                            .Value;
                        if (xmlNode != null)
                        {
                            statDefName = xmlNode.InnerXml;
                        }

                        stringBuilder.AppendLine($"{statDefName.CapitalizeFirst()}: x{child.InnerXml}");
                    }
                }
            }

            var attributes = node.Attributes;
            var parentAttr = attributes?["ParentName"];
            if (parentAttr == null)
            {
                return;
            }

            xmlNode = xmlDatabase.SelectNodesByXpath($"Defs/ThingDef[@Name=\"{parentAttr.InnerXml}\"]")
                .FirstOrDefault().Value;
            AllStatFactorsForThingDef(xmlDatabase, xmlNode, stringBuilder, foundStats);
        }

        private void AllStatOffsetsForThingDef(XmlDatabaseModule xmlDatabase, XmlNode node, StringBuilder stringBuilder,
            HashSet<string> foundStats)
        {
            XmlNode xmlNode;
            if (node["stuffProps"] != null)
            {
                XmlNode statFactors = node["stuffProps"]["statOffsets"];
                if (statFactors != null)
                {
                    foreach (XmlNode child in statFactors.ChildNodes)
                    {
                        if (child.NodeType != XmlNodeType.Element)
                        {
                            continue;
                        }

                        var statDefName = child.Name;
                        if (foundStats.Contains(statDefName))
                        {
                            continue;
                        }

                        foundStats.Add(statDefName);

                        xmlNode = xmlDatabase
                            .SelectNodesByXpath($"Defs/StatDef[defName=\"{statDefName}\"]/label").FirstOrDefault()
                            .Value;
                        if (xmlNode != null)
                        {
                            statDefName = xmlNode.InnerXml;
                        }

                        var val = child.InnerXml;
                        val = float.Parse(val).ToStringSign();
                        stringBuilder.AppendLine($"{statDefName.CapitalizeFirst()}: {val}");
                    }
                }
            }

            var attributes = node.Attributes;
            var parentAttr = attributes?["ParentName"];
            if (parentAttr == null)
            {
                return;
            }

            xmlNode = xmlDatabase.SelectNodesByXpath($"Defs/ThingDef[@Name=\"{parentAttr.InnerXml}\"]")
                .FirstOrDefault().Value;
            AllStatOffsetsForThingDef(xmlDatabase, xmlNode, stringBuilder, foundStats);
        }

        private string InnerXmlOfPathFromDef(XmlDatabaseModule database, XmlNode def, string xpath,
            string defaultValue = null)
        {
            XmlNode result = null;
            while (result == null)
            {
                var list = def.SelectNodes(xpath);
                if (list is { Count: > 0 })
                {
                    result = list.Item(0);
                }
                else
                {
                    var attributeCollection = def.Attributes;
                    if (attributeCollection?["ParentName"] != null)
                    {
                        def = database
                            .SelectNodesByXpath(
                                $"Defs/ThingDef[@Name=\"{attributeCollection["ParentName"].InnerXml}\"]")
                            .FirstOrDefault().Value;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return result?.InnerXml ?? defaultValue;
        }
    }
}