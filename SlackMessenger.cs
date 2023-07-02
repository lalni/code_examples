using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
namespace RiskReporter
{
    class SlackMessenger
    {
        private static readonly HttpClient client = new HttpClient();

        // reponse from message methods
        public class SlackMessageResponse
        {
            public bool ok { get; set; }
            public string error { get; set; }
            public string channel { get; set; }
            public string ts { get; set; }
        }

        // a slack message
        public class SlackMessage
        {
            public string channel { get; set; }
            public string text { get; set; }
            public SlackAttachment[] attachments { get; set; }
        }

        // a slack message attachment
        public class SlackAttachment
        {
            public string fallback { get; set; }
            public string text { get; set; }
            public string image_url { get; set; }
            public string color { get; set; }
        }

        // sends a slack message asynchronous
        // throws exception if message can not be sent
        public static async Task SendMessageAsync(string token, SlackMessage msg)
        {
            // serialize method parameters to JSON
            var content = JsonConvert.SerializeObject(msg);
            var httpContent = new StringContent(
                content,
                Encoding.UTF8,
                "application/json"
            );

            // set token in authorization header
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // send message to API
            var response = await client.PostAsync("https://slack.com/api/chat.postMessage", httpContent);

            // fetch response from API
            var responseJson = await response.Content.ReadAsStringAsync();

            // convert JSON response to object
            SlackMessageResponse messageResponse =
                JsonConvert.DeserializeObject<SlackMessageResponse>(responseJson);

            // throw exception if sending failed
            if (messageResponse.ok == false)
            {

                throw new Exception(
                    "failed to send message. error: " + messageResponse.error
                );
            }
        }



        public static void Message(string message,bool goodnews, string header, string channel)
        {
            string color = goodnews ? "good" : "bad";
            var msg = new SlackMessage
            {
                channel = channel,
                text = header,
                attachments = new SlackAttachment[]
                {
                    new SlackAttachment
                    {
                        fallback = "unable to send attachment",
                        text = message,
                        color = color
                    }
                }
            };
            SendMessageAsync(
                "xoxb-902840698468-3830394358563-OCwezRsPlgVBehIXekEpUgC8",
                msg
            ).Wait();
        }


    }
}