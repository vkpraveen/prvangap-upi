using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using System.Collections.Generic;

namespace FunctionApp1
{
    public class UpiFunctionApp : ServerlessHub
    {
        private readonly static HashSet<string> _connections =
           new HashSet<string>();

        [FunctionName("index")]
        public static IActionResult GetHomePage([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req, ExecutionContext context)
        {
            var path = Path.Combine(context.FunctionAppDirectory, "content", "index.html");
            return new ContentResult
            {
                Content = File.ReadAllText(path),
                ContentType = "text/html",
            };
        }

        [FunctionName("sender")]
        public static IActionResult GetSenderPage([HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req, ExecutionContext context)
        {
            var path = Path.Combine(context.FunctionAppDirectory, "content", "sender.html");
            return new ContentResult
            {
                Content = File.ReadAllText(path),
                ContentType = "text/html",
            };
        }

        [FunctionName("negotiate")]
        public static SignalRConnectionInfo Negotiate(
           [HttpTrigger(AuthorizationLevel.Anonymous)] HttpRequest req,
           [SignalRConnectionInfo(HubName = "upi", UserId = "{headers.prvangap-id}")] SignalRConnectionInfo connectionInfo)
        {
            var id = req.Query["id"];
            _connections.Add(id);
            return connectionInfo;
        }

        [FunctionName("sendmessage")]
        public static Task SendMessage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        [SignalR(HubName = "upi")] IAsyncCollector<SignalRMessage> signalRMessages)
        {
            try
            {
                var content = new StreamReader(req.Body).ReadToEnd();
                var data = JsonConvert.DeserializeObject<Data>(content);
                if (_connections.TryGetValue(data.Id, out string id))
                {
                    return signalRMessages.AddAsync(
                        new SignalRMessage
                        {
                            // the message will only be sent to these user IDs
                            UserId = id,
                            Target = "newMessage",
                            Arguments = new[] { data.Response }
                        });
                }

                return Task.FromResult<Data>(new Data { Response = "failed" });
            }
            catch (Exception ex)
            {
                return Task.FromResult<object>(null);
            }
        }

        public class Data
        {
            public string Response { get; set; }
            public string Id { get; set; }
        }
    }
}
