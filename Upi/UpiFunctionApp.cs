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
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Azure.Messaging.EventGrid;
using Newtonsoft.Json.Linq;

namespace FunctionApp1
{
    public class UpiFunctionApp
    {
        private const string HubName = "upi";
        private readonly static Dictionary<string, DateTime> _connections =
           new Dictionary<string, DateTime>();
        private static readonly object lockObject = new object();

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
           [SignalRConnectionInfo(HubName = HubName, UserId = "{headers.prvangap-id}")] SignalRConnectionInfo connectionInfo)
        {
            var id = req.Query["id"];
            _connections.Add(id, DateTime.UtcNow);
            return connectionInfo;
        }

        //[FunctionName(nameof(OnConnected))]
        //public async Task OnConnected([SignalRTrigger] InvocationContext invocationContext)
        //{
        //    invocationContext.Headers.TryGetValue("Authorization", out var auth);
        //}

        //[FunctionName(nameof(OnDisconnected))]
        //public void OnDisconnected([SignalRTrigger] InvocationContext invocationContext)
        //{
        //    if (invocationContext.Headers.TryGetValue("prvangap-id", out var headerId))
        //    {
        //        if (_connections.TryGetValue(headerId, out string id))
        //        {
        //            _connections.Remove(id);
        //        }
        //    }
        //}

        //[FunctionName("onconnected")]
        //public static void EventGridTest([EventGridTrigger] EventGridEvent eventGridEvent,
        //   [SignalR(HubName = HubName)] IAsyncCollector<SignalRMessage> signalRMessages)
        //{
        //    var message = eventGridEvent.Data.ToObjectFromJson<SignalREvent>();
        //    var connected = eventGridEvent.EventType == "Microsoft.SignalRService.ClientConnectionConnected";
        //    if (!connected)
        //    {
        //        if (_connections.TryGetValue(message.UserId, out string id))
        //        {
        //            _connections.Remove(id);
        //        }
        //    }
        //}

        [FunctionName("sendmessage")]
        public static async Task<IActionResult> SendMessage(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
            [SignalR(HubName = HubName)] IAsyncCollector<SignalRMessage> signalRMessages)
        {
            try
            {
                var content = new StreamReader(req.Body).ReadToEnd();
                var data = JsonConvert.DeserializeObject<Data>(content);
                if (_connections.ContainsKey(data.Id))
                {
                    await signalRMessages.AddAsync(
                        new SignalRMessage
                        {
                            UserId = data.Id,
                            Target = "onPaymentStatus",
                            Arguments = new[] { data.Response, _connections.Count.ToString() }
                        });

                    var successResult = new ObjectResult(data.Id)
                    {
                        StatusCode = StatusCodes.Status202Accepted
                    };

                    lock (lockObject)
                    {
                        _connections.Remove(data.Id);
                    }
                    return successResult;
                }

                var notFoundResult = new ObjectResult(data.Id)
                {
                    StatusCode = StatusCodes.Status404NotFound
                };

                return notFoundResult;
            }
            catch (Exception ex)
            {
                var internalErrorResult = new ObjectResult(ex.Message)
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };

                return internalErrorResult;
            }
        }

        [FunctionName("cleanuptimer")]
        public static void CleanupTimer([TimerTrigger("0 */5 * * * *")] TimerInfo myTimer)
        {
            var currentDateTime = DateTime.UtcNow;
            foreach (var item in _connections)
            {
                if ((currentDateTime - item.Value).TotalMinutes > 5)
                {
                    lock (lockObject)
                    {
                        _connections.Remove(item.Key);
                    }
                }
            }
        }


        public class Data
        {
            public string Response { get; set; }
            public string Id { get; set; }
        }

        public class SignalREvent
        {
            public DateTime Timestamp { get; set; }
            public string HubName { get; set; }
            public string ConnectionId { get; set; }
            public string UserId { get; set; }
        }
    }
}
