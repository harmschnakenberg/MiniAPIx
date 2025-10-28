using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniAPI
{
    public class Program
    {
       
        public static async Task Main(string[] args)
        {
            // TODO: lade CPUs aus Konfiguration
            TagCollection.AddCpu("A01", S7.Net.CpuType.S71500, "192.168.160.56", 0, 0);

            // Hintergrundlese-Task starten (optional)
            TagCollection.StartReading();
            
            try
            {
                var builder = WebApplication.CreateSlimBuilder(args);                
                var app = builder.Build();
                app.UseWebSockets();
                app.UseStaticFiles();
              
                app.MapGet("/{name}", (string name) => "Hello {name}!");

                app.Map("/", async context =>
                {
                    if (!context.WebSockets.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        return;
                    }

                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var ct = context.RequestAborted;

                    try
                    {
                        // 1) Vollständige initiale Nachricht einlesen (kann fragmentiert kommen)
                        var initialJson = await ReadFullMessageAsync(webSocket, ct);
#if DEBUG
                        Console.WriteLine($"Neue Tags (raw): {initialJson}");
#endif
                        var tagNames = JsonSerializer.Deserialize<string[]?>(initialJson) ?? [];
                        if (tagNames.Length == 0)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Keine Tags übermittelt", ct);
                            return;
                        }

                        // 2) Lokale Anfragemap aufbauen (per connection)
                        var requested = tagNames
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Distinct()
                            .ToDictionary(n => n, _ => (object?)null);

                        // 3) Tags global registrieren
                        TagCollection.AddTags(tagNames);

                        // 4) Schleife: nur geänderte Werte senden
                        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

                        while (!ct.IsCancellationRequested && webSocket.State == WebSocketState.Open)
                        {
                            var changed = TagCollection.PokeTagValues(ref requested);
                            if (changed.Count > 0)
                            {
                                var payload = JsonSerializer.Serialize(changed.ToArray(), options);
                                var bytes = Encoding.UTF8.GetBytes(payload);
                                await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
#if DEBUG
                               // Console.WriteLine($"Sende: {payload}");
#endif
                            }

                            // moderate Poll-Rate; Abbruch über ct möglich
                            await Task.Delay(1000, ct);
                        }

                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        Console.WriteLine("WebSocket Verbindungsabbruch erbeten..");
                    }
                    catch (WebSocketException wsex)
                    {
                        Console.WriteLine($"WebSocket Fehler: {wsex.Message}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Fehler in WebSocket-Handler: {ex.Message}");
                    }
                    finally
                    {
                        if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived)
                        {
                            try
                            {
                                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Schließe WebSocket Verbindung.");
                                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                            }
                            catch { 
                                Console.WriteLine("Fehler beim Schließen der WebSocket Verbindung.");
                            }
                        }
                    }
                });

                app.Map("/test", async ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    var file = File.ReadAllText("wwwroot/html/index.html", Encoding.UTF8);
                    await ctx.Response.WriteAsync(file);                  
                    await ctx.Response.CompleteAsync();
                });

                app.MapGet("/tags", async ctx =>
                {
                    var allTags = TagCollection.Tags.Values
                        .Select(t => new { t.Name, t.Value, t.Comment })
                        .ToArray();
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsJsonAsync(allTags);
                    await ctx.Response.CompleteAsync();
                });

                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Program Fehler: {ex.Message}");
            }
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
    }
}