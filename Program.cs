using Microsoft.Extensions.Options;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using static MiniAPI.Program;

namespace MiniAPI
{
    public partial class Program
    {
       internal static bool running = true;

        public static async Task Main()
        {
            
            TagCollection.AddCpu("A02", S7.Net.CpuType.S71500, "10.0.11.60", 0, 0);
            //TagCollection.AddCpu("A01", S7.Net.CpuType.S71500, "192.168.160.56", 0, 0);

            //lade CPUs aus Konfiguration
            TagCollection.AddCpuConfig();

            // Hintergrundlese-Task starten
            TagCollection.StartReading();

            do {
            await WebAppMain();
              await Task.Delay(1000);
            } while (running);

      
#if DEBUG
            Console.WriteLine("WebApplication ist geschlossen.");
#endif
            // Hintergrundlese-Task stoppen
            TagCollection.StopReading();
        }

        private static async Task<string> ReadFullMessageAsync(WebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[4096];
            using var ms = new MemoryStream();
            WebSocketReceiveResult? result;

            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            ms.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(ms, Encoding.UTF8);
            return await reader.ReadToEndAsync(ct);
        }
   



        public static void Message(Message.MessageType type, string alert)
        {
#if DEBUG
            Console.WriteLine($"[{nameof(type)}] {alert}");
#endif
            // TODO: implement alerting 
            Alerts.Add(new Message(type,alert));
        }


    }


    public class Message(Message.MessageType type, string message)
    {
        public enum MessageType
        {
            Alert,
            Warning,
            Success,
            Info
        }

        [JsonPropertyName("type")]
        public MessageType Type { get; set; } = type;
        [JsonPropertyName("message")]
        public string Text { get; set; } = message;
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}