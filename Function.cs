using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Amazon.Lambda.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TsukiichiWatcher
{
    public class Function
    {
        private string discordWebhook;
        public async Task FunctionHandler()
        {
            var json = await File.ReadAllTextAsync("config.json");
            var config = JsonConvert.DeserializeObject<dynamic>(json);
            discordWebhook = config.discord_webhook;
            var target = config.target;

            var client = new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "TsukiichiWatcher/1.0 (https://github.com/YDKK/TsukiichiWatcher)");

            async Task<dynamic> get(string url)
            {
                var result = await client.GetAsync(url);
                var json = await result.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<dynamic>(json);
            }

            var getSchedules = get("https://splatoon2.ink/data/schedules.json");
            var getLocale = get("https://splatoon2.ink/data/locale/ja.json");
            var schedules = await getSchedules;
            var league = ((JArray)schedules.league).Cast<dynamic>().OrderBy(x => x.start_time).ToArray();
            var current = league.Last();

            if (current.rule.key == target.rule_key)
            {
                var matchStageA = current.stage_a.id == target.stage_a_id || current.stage_a.id == target.stage_b_id;
                var matchStageB = current.stage_b.id == target.stage_a_id || current.stage_b.id == target.stage_b_id;
                var perfect = matchStageA && matchStageB;
                if (matchStageA || matchStageB)
                {
                    var startTime = DateTimeOffset.FromUnixTimeSeconds((long)current.start_time).ToLocalTime().ToString("g");
                    var stage = matchStageA ? $"{current.stage_a.id}" : $"{current.stage_b.id}";
                    var rule = current.rule.key;
                    var locale = await getLocale;
                    if (perfect)
                    {
                        var message = $":exclamation:完全一致:exclamation: {startTime} からの{locale.rules[(string)rule].name}はツキイチ・リーグマッチと完全に同じルール・マップ！";
                        await PostToDiscord(message);
                    }
                    else
                    {
                        var message = $"【部分一致】{startTime} からの{locale.stages[(string)stage].name} {locale.rules[(string)rule].name}はツキイチ・リーグマッチと同じルール・マップ！";
                        await PostToDiscord(message);
                    }
                }
            }
        }

        private async Task PostToDiscord(string message)
        {
            var client = new HttpClient();
            var payload = new
            {
                content = message
            };
            var json = JsonConvert.SerializeObject(payload);
            await client.PostAsync(discordWebhook, new StringContent(json, Encoding.UTF8, "application/json"));
        }
    }
}
