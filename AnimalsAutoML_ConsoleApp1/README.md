# Порівняння облич — ASP.NET Core MVC

Вебінтерфейс на .NET 8 замість Telegram-бота. Використовується чинний сценарій AWS Rekognition CompareFaces; локальна модель AutoML не потрібна.

## Запуск

Потрібні .NET SDK 8 або новіший та облікові дані AWS із дозволом `rekognition:CompareFaces`. Ключі задаються в окремому локальному файлі `AccessTokens.cs`: `AwsAccessKeyId` і `AwsSecretAccessKey`. Файл також містить `TelegramBotToken`, який MVC-застосунок не використовує. Запуск у PowerShell:

```powershell
$env:AWS__Region = "eu-central-1"
dotnet run --urls http://localhost:5080
```

Відкрийте http://localhost:5080, оберіть два фото та натисніть «Порівняти обличчя». Виклики AWS можуть тарифікуватися. Для розміщення поза локальним комп’ютером налаштуйте HTTPS на сервері або reverse proxy.

Фото: JPEG/PNG, до 5 МіБ кожне, до 20 мегапікселів. Перевіряються справжній формат і декодування. Збережено пороги якості: яскравість щонайменше 45, чіткість щонайменше 5. Неякісні фото не надсилаються до AWS. Для повторної спроби потрібно вибрати обидва фото знову.

## Структура

- `Controllers/HomeController.cs` — завантаження, валідація, виклик сервісів.
- `Models/CompareFacesViewModel.cs` — форма та результат.
- `Views/Home/Index.cshtml` — інтерфейс.
- `ImageQualityService.cs` — перевірка якості.
- `RekognitionService.cs` — AWS SDK.
- `Program.cs` — MVC, маршрути та залежності.

`AccessTokens.cs` виключений із Git. При перенесенні проєкту створіть цей файл окремо з класом `AccessTokens` у просторі імен `AnimalsAutoML_ConsoleApp1` та рядковими властивостями `AwsAccessKeyId`, `AwsSecretAccessKey`, `TelegramBotToken`. Значення ключів компілюються у збірку, тому не поширюйте її з робочими секретами.

Перевірка компіляції: `dotnet build`.
