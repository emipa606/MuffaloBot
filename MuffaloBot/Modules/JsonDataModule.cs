using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using Newtonsoft.Json.Linq;

namespace MuffaloBot.Modules
{
    public class JsonDataModule : BaseModule
    {
        public JObject data;

        protected override void Setup(DiscordClient client)
        {
            Client = client;
            ReloadDataAsync().ConfigureAwait(false).GetAwaiter().GetResult();
            client.MessageCreated += HandleQuoteAsync;
        }

        private async Task HandleQuoteAsync(MessageCreateEventArgs e)
        {
            var quote = data["quotes"]?[e.Message.Content];
            if (quote != null)
            {
                await e.Message.RespondAsync(quote.ToString());
            }
        }

        public async Task ReloadDataAsync()
        {
            var http = new HttpClient();
            var result = await http
                .GetStringAsync("https://raw.githubusercontent.com/Zero747/MuffaloBot/master/MuffaloBot/Data/data.json")
                .ConfigureAwait(false);
            var jObject = JObject.Parse(result);
            data = jObject;
        }
    }
}