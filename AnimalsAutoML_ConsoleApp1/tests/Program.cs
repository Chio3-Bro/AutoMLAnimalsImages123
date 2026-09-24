using System.Net;
using System.Text;
using System.Text.Json;
using AnimalsAutoML_ConsoleApp1;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

var transport = new FakeTelegram();
using var http = new HttpClient(transport);
var client = new TelegramBotClient("123456:TEST_TOKEN", http);
var predictions = 0;
var failNext = false;
var handler = new AnimalBot(client, bytes =>
{
    predictions++;
    if (failNext) { failNext = false; throw new InvalidDataException(); }
    return [new("cat", .8f), new("dog", .15f), new("bird", .04f), new("other", .01f)];
});
Message Photo(int id, long chat = 1) => new()
{
    Id = id, Chat = new Chat { Id = chat, Type = ChatType.Private }, MediaGroupId = "album",
    Photo = [new() { FileId = "small", Width = 10, Height = 10 }, new() { FileId = "large", Width = 100, Height = 100 }]
};
async Task Send(Message message) => await handler.HandleAsync(message, CancellationToken.None);
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}
await Send(new Message { Id = 1, Chat = new Chat { Id = 1 }, Text = "/start" });
await Send(new Message { Id = 2, Chat = new Chat { Id = 1 }, Text = "hello" });
Check(predictions == 0 && transport.Downloads == 0, "commands/text do not access photos");
await Send(Photo(3));
await Send(Photo(4));
await Send(Photo(5, 2));
Check(predictions == 3, "every album image and another chat are processed");
Check(transport.Replies.Skip(2).Select(r => r.GetProperty("reply_parameters").GetProperty("message_id").GetInt32()).SequenceEqual(new[] { 3, 4, 5 }), "results reply to their source images");
Check(transport.FileIds.All(id => id == "large"), "highest resolution selected");
Check(transport.Replies[2].GetProperty("text").GetString()!.Contains("cat") && !transport.Replies[2].GetProperty("text").GetString()!.Contains("other"), "one compact top-three result");
await Send(new Message { Id = 6, Chat = new Chat { Id = 1 }, Document = new Document { FileId = "document", FileName = "cat.png", FileSize = 100 } });
Check(predictions == 4, "PNG document accepted");
await Send(new Message { Id = 7, Chat = new Chat { Id = 1 }, Document = new Document { FileId = "huge", FileName = "cat.jpg", FileSize = 11 * 1024 * 1024 } });
Check(predictions == 4 && !transport.FileIds.Contains("huge"), "oversize document rejected before download");
failNext = true;
await Send(Photo(8));
await Send(Photo(9));
Check(predictions == 6 && transport.Replies[^2].GetProperty("text").GetString()!.Contains("Не удалось") && transport.Replies[^1].GetProperty("text").GetString()!.Contains("cat"), "bad image does not prevent next image");
transport.ThrottleNext = true;
await Send(Photo(10));
Check(predictions == 7 && transport.Replies[^1].GetProperty("reply_parameters").GetProperty("message_id").GetInt32() == 10, "rate limit retries reply without repeating inference");
Console.WriteLine("All checks passed.");

sealed class FakeTelegram : HttpMessageHandler
{
    public List<JsonElement> Replies { get; } = [];
    public List<string> FileIds { get; } = [];
    public int Downloads;
    public bool ThrottleNext;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.AbsolutePath;
        string response;
        if (path.EndsWith("/getFile"))
        {
            var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement;
            FileIds.Add(body.GetProperty("file_id").GetString()!);
            response = "{\"ok\":true,\"result\":{\"file_id\":\"image\",\"file_unique_id\":\"unique\",\"file_size\":3,\"file_path\":\"photos/test.jpg\"}}";
        }
        else if (path.EndsWith("/sendMessage"))
        {
            if (ThrottleNext)
            {
                ThrottleNext = false;
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"ok\":false,\"error_code\":429,\"description\":\"Too Many Requests\",\"parameters\":{\"retry_after\":1}}", Encoding.UTF8, "application/json") };
            }
            Replies.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone());
            response = "{\"ok\":true,\"result\":{\"message_id\":100,\"date\":0,\"chat\":{\"id\":1,\"type\":\"private\"}}}";
        }
        else if (path.EndsWith("/photos/test.jpg"))
        {
            Downloads++;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
        }
        else throw new Exception("Unexpected request: " + path);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
    }
}
