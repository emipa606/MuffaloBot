using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using DSharpPlus;

namespace MuffaloBot.Modules
{
    internal class XmlDatabaseModule : BaseModule
    {
        private List<KeyValuePair<string, XmlDocument>> database = new List<KeyValuePair<string, XmlDocument>>();

        public XmlDatabaseModule()
        {
            UpdateDatabaseAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task UpdateDatabaseAsync()
        {
            var client = new HttpClient();
            await using var memory = new MemoryStream(await client
                .GetByteArrayAsync("https://github.com/Zero747/MuffaloBot/raw/master/MuffaloBot/Data/Defs.zip")
                .ConfigureAwait(false));
            using var archive = new ZipArchive(memory);
            database = new List<KeyValuePair<string, XmlDocument>>(archive.Entries.Count);
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".xml"))
                {
                    continue;
                }

                var document = new XmlDocument();
                document.Load(entry.Open());
                database.Add(new KeyValuePair<string, XmlDocument>(entry.FullName, document));
            }
        }

        public IEnumerable<KeyValuePair<string, XmlNode>> SelectNodesByXpath(string xpath)
        {
            return from KeyValuePair<string, XmlDocument> doc in database
                from XmlNode node in doc.Value.SelectNodes(xpath)
                select new KeyValuePair<string, XmlNode>(doc.Key, node);
        }

        public string GetSummaryForNodeSelection(string xpath)
        {
            var stringBuilder = new StringBuilder();
            var sw = new Stopwatch();
            sw.Start();
            var results = SelectNodesByXpath(xpath).ToList();
            sw.Stop();
            foreach (var result in results.Take(5))
            {
                stringBuilder.AppendLine($"<!-- In {result.Key}: -->");
                stringBuilder.AppendLine($"{result.Value.OuterXml.WithinChars(100)}\n");
            }

            stringBuilder.AppendFormat(
                "<!-- Summary: Found {0} results total (showing first 5 if applicable) -->\n<!-- Evaluation time {1} ticks ({2}ms) -->",
                results.Count, sw.ElapsedTicks, sw.ElapsedMilliseconds);
            return string.Concat("```xml\n", stringBuilder.ToString(), "```");
        }

        protected override void Setup(DiscordClient client)
        {
            Client = client;
        }
    }
}