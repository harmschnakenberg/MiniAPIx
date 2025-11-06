using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MiniAPI
{
    public partial class Program
    {
        static List<Message> Alerts { get; set; } = [];

        internal static async Task WebAppMain()
        {
            try
            {
                var builder = WebApplication.CreateSlimBuilder();
                var app = builder.Build();
                app.UseWebSockets();
                app.UseStaticFiles();

                app.MapGet("/exit", async context =>
                {
                    await context.Response.WriteAsync("Shutting down the server..." + new JsonTag("alert", "Server fährt herunter...", DateTime.Now));
                    await context.Response.CompleteAsync();
                    // Give the response some time to be sent before shutting down
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000);
                        Console.WriteLine("Server is shutting down...");
                        app.Lifetime.StopApplication();
                        running = false;
                    });
                });
              
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
                                Console.WriteLine($"Sende: {payload}");
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
                            catch
                            {
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

                app.MapGet("/sse/test", async ctx =>
                {

                    string html = $"<!DOCTYPE html>\r\n" +
                         $"<html>\r\n" +
                         $"<body>\r\n" +
                         $"\r\n" +
                         $"<h1>Getting Server Updates</h1>\r\n" +
                         $"\r\n" +
                         $"<div id='result'></div>\r\n" +
                         $"\r\n" +
                         $"<script>\r\n" +
                         $"const x = document.getElementById('result');\r\n" +
                         $"if(typeof(EventSource) !== 'undefined') {{\r\n" +
                         $"  var source = new EventSource('http://' + window.location.host + '/sse');\r\n" +
                         $"  source.onmessage = function(event) {{\r\n" +
                         $"    x.innerHTML += event.data + '<br>';\r\n" +
                         $"  }};" +
                         $"\r\n" +
                         $"}} else {{\r\n" +
                         $"  x.innerHTML = 'Sorry, no support for server-sent events.';\r\n" +
                         $"}}\r\n" +
                         $"</script>\r\n" +
                         $"\r\n" +
                         $"</body>\r\n" +
                         $"</html>\r\n";

                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    
                    await ctx.Response.WriteAsync(html);
                    await ctx.Response.CompleteAsync();

                });

                app.MapGet("/alert", async (HttpContext ctx, CancellationToken token) =>
                {
                    ctx.Response.Headers.CacheControl = "no-cache"; //.Append("Content", "text/event-stream");
                    ctx.Response.Headers.ContentType = "text/event-stream";// ("Cacher-Control", "no-cache");
                    //ctx.Response.Headers.Append("Connection", "keep-alive");
                    
                    List<Message> sentAlerts = [];

                    while (!token.IsCancellationRequested)
                    {
                        var newAlerts = Alerts.Except(sentAlerts).ToList();
                        Console.WriteLine(string.Join(' ', newAlerts));

                        foreach (var alert in newAlerts)
                        {
                            var payloadAlert = JsonSerializer.Serialize(new { alert.Type, alert.Text, alert.Timestamp });
                            await ctx.Response.WriteAsync($"data: {payloadAlert}", token);
                            await ctx.Response.Body.FlushAsync(token);
                        }
                        sentAlerts.AddRange(newAlerts);
                    }

                    Alerts.RemoveAll(a => a.Timestamp < DateTime.Now.AddMinutes(-1)); // Alte Alerts entfernen
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

                app.MapGet("/cpu", async ctx =>
                {
                    var allCpus = TagCollection.GetJsonCpus();                        
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsJsonAsync(allCpus);
                    await ctx.Response.CompleteAsync();
                });

                // Beispiel-Endpunkt, der Authentifizierung erfordert
                //app.MapGet("/secure", [Authorize] () => "Dies sind geschützte Daten!")
                //    .RequireAuthorization();


                // Beispiel-Endpunkt für die Token-Generierung (Login)
                //app.MapPost("/login", (UserLogin userLogin) =>
                //{
                //    // Hier echte Benutzervalidierung durchführen (z.B. aus DB)
                //    if (userLogin.Username == "test" && userLogin.Password == "password")
                //    {
                //        var issuer = builder.Configuration["Jwt:Issuer"];
                //        var audience = builder.Configuration["Jwt:Audience"];
                //        var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]?? string.Empty);
                //        var signingCredentials = new SigningCredentials(
                //            new SymmetricSecurityKey(key),
                //            SecurityAlgorithms.HmacSha256
                //        );

                //        var subject = new ClaimsIdentity(new[]
                //        {
                //            new Claim(JwtRegisteredClaimNames.Sub, userLogin.Username),
                //            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                //        });

                //        var expires = DateTime.UtcNow.AddMinutes(30);
                //        var tokenDescriptor = new SecurityTokenDescriptor
                //        {
                //            Subject = subject,
                //            Expires = expires,
                //            Issuer = issuer,
                //            Audience = audience,
                //            SigningCredentials = signingCredentials
                //        };

                //        var tokenHandler = new JwtSecurityTokenHandler();
                //        var token = tokenHandler.CreateToken(tokenDescriptor);
                //        var jwtToken = tokenHandler.WriteToken(token);
                //        return Results.Ok(new { token = jwtToken });
                //    }
                //    return Results.Unauthorized();
                //});


                //app.MapGet("/{name}", (string name) => $"Hello {name}!");

                await app.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Program Fehler: {ex.Message}");
            }


        }

    }

    // Hilfsklasse für Login-Daten
    public record UserLogin(string Username, string Password);
}
