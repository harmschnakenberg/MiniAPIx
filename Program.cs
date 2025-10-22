using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MiniAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            //TODO: lese alle CPUs aus Configuration-File
            TagCollection.AddCpu("A01", S7.Net.CpuType.S71500, "192.168.160.56", 0, 0);
            
            TagCollection.StartReading();

            var builder = WebApplication.CreateSlimBuilder(args);            
            //builder.Services.ConfigureHttpJsonOptions(options =>
            //{
            //    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
            //});

            var app = builder.Build();          
            app.UseWebSockets();
            app.Map("/", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var buffer = new byte[1024 * 4];

                    #region Anfrage neue Tags                    
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                    string newTagsStr = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Neue Tags: {newTagsStr}");
                    string[]? newTags = JsonSerializer.Deserialize<string[]>(newTagsStr);
                    if (newTags?.Length > 0)
                        TagCollection.AddTags(newTags);                   
                    #endregion

                    #region Sende Tag-Werte
                    if (newTags?.Length < 1)
                        return;

                    while (!context.RequestAborted.IsCancellationRequested)
                    {
                        string answer = TagCollection.PokeTagValues(newTags ?? Array.Empty<string>());
                        //Console.WriteLine($"Sende: {answer}");
                        var bytes = Encoding.UTF8.GetBytes(answer);
                        var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);

                        if (webSocket.State == System.Net.WebSockets.WebSocketState.Open)
                        {
                            await webSocket.SendAsync(arraySegment, System.Net.WebSockets.WebSocketMessageType.Text, true, context.RequestAborted);
                            await Task.Delay(1000);
                        }
                          
                    }

                  // await webSocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, context.RequestAborted);

                    #endregion
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            });
            app.Map("/test", async ctx =>             {
                {
                    ctx.Response.StatusCode = 200;
                    ctx.Response.ContentType = "text/html";
                    await ctx.Response.WriteAsync(HtmlTemplate.JSWebsocket);
                    await ctx.Response.CompleteAsync();
                }
            });

            #region Beispiel für eine Mini-API mit Todo-Elementen
            var sampleTodos = new Todo[] {
                new(1, "Walk the dog"),
                new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
                new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
                new(4, "Clean the bathroom"),
                new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
            };

            var todosApi = app.MapGroup("/todos");
            todosApi.MapGet("/", () => sampleTodos);
            todosApi.MapGet("/{id}", (int id) =>
                sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
                    ? Results.Ok(todo)
                    : Results.NotFound());
            #endregion

            app.Run();
        }
    }

    public record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

    [JsonSerializable(typeof(Todo[]))]
    internal partial class AppJsonSerializerContext(JsonSerializerOptions? options) : JsonSerializerContext(options)
    {
        public static IJsonTypeInfoResolver? Default { get; internal set; }

        protected override JsonSerializerOptions? GeneratedSerializerOptions => throw new NotImplementedException();

        public override JsonTypeInfo? GetTypeInfo(Type type)
        {
            throw new NotImplementedException();
        }
    }


}
