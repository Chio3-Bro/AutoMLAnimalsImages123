using AnimalsAutoML_ConsoleApp1;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

Console.OutputEncoding = System.Text.Encoding.UTF8;
const string token = "8129847890:AAHWrKEaXCdEjRABJpGdFInCCE4Mx-z2xPY";

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
try
{
    // Load the model before accepting photos so configuration errors surface at startup.
    _ = AnimalsAutoML.PredictEngine.Value;
    var bot = new TelegramBotClient(token);
    var me = await bot.GetMe(shutdown.Token);
    if (args.Contains("--check"))
    {
        Console.WriteLine($"Проверка пройдена: модель загружена, Telegram подтвердил бота @{me.Username}.");
        return 0;
    }
    var handler = new AnimalBot(bot, bytes =>
        AnimalsAutoML.PredictAllLabels(new AnimalsAutoML.ModelInput { ImageSource = bytes }).ToArray());
    Console.WriteLine($"Бот @{me.Username} запущен. Для остановки нажмите Ctrl+C.");
    // Sequential processing also protects the shared ML.NET PredictionEngine.
    await bot.ReceiveAsync(
        async (_, update, ct) =>
        {
            if (update.Message is { } message)
                await handler.HandleAsync(message, ct);
        },
        async (_, error, ct) =>
        {
            Console.Error.WriteLine($"Ошибка Telegram: {error.GetType().Name}");
            await Task.Delay(TimeSpan.FromSeconds(3), ct);
        },
        new ReceiverOptions { AllowedUpdates = [UpdateType.Message] },
        shutdown.Token);
    return 0;
}
catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
{
    Console.WriteLine("Бот остановлен.");
    return 0;
}
catch (Exception error)
{
    var details = error.GetBaseException().Message.Replace(token, "[токен скрыт]");
    Console.Error.WriteLine($"Не удалось запустить бота: {error.GetType().Name}: {details}");
    return 1;
}
