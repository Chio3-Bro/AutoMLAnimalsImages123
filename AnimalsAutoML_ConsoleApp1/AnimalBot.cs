using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;

namespace AnimalsAutoML_ConsoleApp1;

public sealed class AnimalBot(
    ITelegramBotClient bot,
    Func<byte[], KeyValuePair<string, float>[]> classify)
{
    private const long MaxImageBytes = 10 * 1024 * 1024;
    private const string Help = "🐾 Пришлите фото животного — я попробую его распознать.\n\n" +
        "Можно выбрать сразу несколько фотографий и отправить их альбомом или отдельными сообщениями. " +
        "На каждое изображение я отвечу отдельно. Также принимаю JPG и PNG как файлы (до 10 МБ).\n\n" +
        "Лучше всего подходят чёткие фотографии с одним животным. /help — эта подсказка.";

    public async Task HandleAsync(Message message, CancellationToken ct)
    {
        try
        {
            var command = message.Text?.Split(' ', '\n')[0].Split('@')[0].ToLowerInvariant();
            if (command is "/start" or "/help")
            {
                await ReplyAsync(message, Help, ct);
                return;
            }

            string? fileId = null;
            long? size = null;
            if (message.Photo is { Length: > 0 } photos)
            {
                // Photo contains resolutions of ONE image. Album images arrive as separate messages.
                var photo = photos.MaxBy(p => (long)p.Width * p.Height)!;
                fileId = photo.FileId;
                size = photo.FileSize;
            }
            else if (message.Document is { } document &&
                (document.MimeType is "image/jpeg" or "image/png" ||
                 Path.GetExtension(document.FileName ?? "").ToLowerInvariant() is ".jpg" or ".jpeg" or ".png"))
            {
                fileId = document.FileId;
                size = document.FileSize;
            }

            if (fileId is null)
            {
                await ReplyAsync(message, "Нужна фотография или файл JPG/PNG. Можно отправить несколько изображений сразу. /help — помощь.", ct);
                return;
            }
            if (size > MaxImageBytes)
            {
                await ReplyAsync(message, "Изображение слишком большое. Пришлите файл до 10 МБ или отправьте его как фото.", ct);
                return;
            }

            var file = await bot.GetFile(fileId, ct);
            if (file.FileSize > MaxImageBytes)
            {
                await ReplyAsync(message, "Изображение слишком большое. Максимальный размер — 10 МБ.", ct);
                return;
            }
            if (string.IsNullOrWhiteSpace(file.FilePath))
                throw new InvalidDataException("Telegram returned no file path.");

            // No temporary files or collisions between photos/users.
            using var image = new MemoryStream();
            await bot.DownloadFile(file.FilePath, image, ct);
            if (image.Length == 0 || image.Length > MaxImageBytes)
                throw new InvalidDataException("Invalid image size.");
            var scores = classify(image.ToArray()).OrderByDescending(s => s.Value).Take(3).ToArray();
            if (scores.Length == 0)
                throw new InvalidDataException("Model returned no labels.");

            var lines = scores.Select((s, i) =>
                $"{i + 1}. {s.Key}: {s.Value.ToString("P1", CultureInfo.GetCultureInfo("ru-RU"))}");
            var result = "🐾 Результат распознавания:\n" + string.Join("\n", lines) +
                "\n\nОценки модели не гарантируют правильное распознавание.";
            await ReplyAsync(message, result, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Ошибка обработки сообщения {message.Id}: {error.GetType().Name}");
            await ReplyAsync(message, "Не удалось обработать изображение. Попробуйте другое фото или повторите отправку позже.", ct);
        }
    }

    private async Task ReplyAsync(Message message, string text, CancellationToken ct)
    {
        // Telegram may throttle bursts of album responses. Retry the reply without re-running inference.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await bot.SendMessage(message.Chat.Id, text,
                    replyParameters: new ReplyParameters { MessageId = message.Id, AllowSendingWithoutReply = true },
                    messageThreadId: message.MessageThreadId, cancellationToken: ct);
                return;
            }
            catch (ApiRequestException error) when (error.ErrorCode == 429 &&
                error.Parameters?.RetryAfter is > 0 && attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(error.Parameters.RetryAfter.Value), ct);
            }
        }
    }
}
