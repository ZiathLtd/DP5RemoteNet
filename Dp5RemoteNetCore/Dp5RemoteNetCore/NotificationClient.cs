using Dp5RemoteNetCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
namespace DP5RemoteNetCore
{
    class NotificationClient
    {
        private static readonly string Connection = "ws://localhost:8777/dp5-websocket";
        private static String[] NOTIFICATION_TYPES = { "DEVICE_LEGACY", "DEVICE_CONNECTED", "DEVICE_DISCONNECTED",
            "LINEAR_CONNECTED", "LINEAR_DISCONNECTED", "LINEAR_NEW_BARCODE", "LINEAR_PLUGGED_IN", "LINEAR_UNPLUGGED",
            "SCAN_MILESTONE", "ACTIVATOR_EVENT" };

        public static async Task createNotificationClient()
        {
            do
            {
                using (var socket = new ClientWebSocket())
                    try
                    {
                        await socket.ConnectAsync(new Uri(Connection), CancellationToken.None);

                        StompMessageSerializer serializer = new StompMessageSerializer();

                        var connect = new StompMessage("CONNECT");
                        connect["accept-version"] = "1.0,1.1,2.0";
                        await Send(socket,serializer.Serialize(connect));

                        try
                        {
                            Thread.Sleep(1000);
                        }
                        catch (Exception e)
                        { }

                        var sub = new StompMessage("SUBSCRIBE");
                        sub["id"] = "RandomID";
                        sub["destination"] = "/topic/events";
                        await Send(socket, serializer.Serialize(sub));

                        await Receive(socket);

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR - {ex.Message}");
                    }
            } while (true);
        }

        static async Task Send(ClientWebSocket socket, string data) =>
            await socket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, CancellationToken.None);

        static async Task Receive(ClientWebSocket socket)
        {
            var buffer = new ArraySegment<byte>(new byte[2048]);
            do
            {
                WebSocketReceiveResult result;
                using (var ms = new MemoryStream())
                {
                    do
                    {
                        result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                        ms.Write(buffer.Array, buffer.Offset, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    ms.Seek(0, SeekOrigin.Begin);
                   
                    StringBuilder notificationMessage = new StringBuilder();
                    using (var reader = new StreamReader(ms, Encoding.UTF8))
                        notificationMessage.Append(await reader.ReadToEndAsync());
                    string resultstr = notificationMessage.ToString();
                    if (resultstr.Contains('{'))
                    {
                        resultstr = resultstr.Substring(resultstr.IndexOf("{"));
                        try
                        {
                            JObject json = JObject.Parse(resultstr);
                            String notificationType = (string)json.GetValue("notificationType");
                            if (NOTIFICATION_TYPES.Contains(notificationType))
                            {
                                Console.WriteLine(notificationMessage.ToString());
                            }
                            
                        }
                        catch (Exception ex)
                        { }
                    }
                }
            } while (true);
        }
    }

}