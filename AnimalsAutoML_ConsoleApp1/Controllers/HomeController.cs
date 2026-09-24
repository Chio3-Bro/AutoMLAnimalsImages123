using Amazon.Rekognition;
using Amazon.Runtime;
using AnimalsAutoML_ConsoleApp1.Models;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;

namespace AnimalsAutoML_ConsoleApp1.Controllers;

public class HomeController(ImageQualityService qualityService, IServiceProvider services, IImageUrlDownloader urlDownloader,
    ILogger<HomeController> logger) : Controller
{
    private const int MaxPhotoBytes = 5 * 1024 * 1024;

    [HttpGet]
    public IActionResult Index() => View(new CompareFacesViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> Index(CompareFacesViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var first = await ReadPhotoAsync(model.FirstPhoto, model.FirstPhotoUrl, "Перше фото", cancellationToken);
        var second = await ReadPhotoAsync(model.SecondPhoto, model.SecondPhotoUrl, "Друге фото", cancellationToken);
        if (!ModelState.IsValid) return View(model);
        model.FirstQuality = qualityService.CheckQuality(first!);
        model.SecondQuality = qualityService.CheckQuality(second!);
        ValidateQuality(model.FirstQuality, nameof(model.FirstPhoto));
        ValidateQuality(model.SecondQuality, nameof(model.SecondPhoto));
        if (!ModelState.IsValid) return View(model);
        try
        {
            var rekognition = services.GetRequiredService<RekognitionService>();
            model.Similarity = await rekognition.CompareFacesAsync(first!, second!, cancellationToken);
            model.Compared = true;
        }
        catch (AmazonRekognitionException ex)
        {
            logger.LogWarning(ex, "Face comparison failed");
            ModelState.AddModelError(string.Empty, "Не вдалося порівняти фото. Перевірте, чи на кожному добре видно обличчя, та доступ до AWS Rekognition.");
        }
        catch (AmazonClientException ex)
        {
            logger.LogError(ex, "AWS configuration or connection failed");
            ModelState.AddModelError(string.Empty, "Сервіс AWS недоступний. Перевірте облікові дані та налаштування сервера.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "AWS connection failed");
            ModelState.AddModelError(string.Empty, "Немає зв’язку з AWS. Спробуйте пізніше.");
        }
        return View(model);
    }

    private async Task<byte[]?> ReadPhotoAsync(IFormFile? file, string? url, string field, CancellationToken cancellationToken)
    {
        try
        {
            byte[] bytes;
            if (file is not null)
            {
                if (file.Length == 0 || file.Length > MaxPhotoBytes)
                    throw new InvalidDataException("Фото має бути непорожнім і не перевищувати 5 МіБ.");
                using var stream = new MemoryStream();
                await file.CopyToAsync(stream, cancellationToken);
                bytes = stream.ToArray();
            }
            else
            {
                bytes = await urlDownloader.DownloadAsync(url?.Trim() ?? "", cancellationToken);
            }
            var info = SixLabors.ImageSharp.Image.Identify(bytes);
            if (info.Metadata.DecodedImageFormat?.Name is not ("JPEG" or "PNG"))
                throw new InvalidDataException("Потрібне пряме посилання на JPEG/PNG або файл зображення, а не вебсторінка.");
            if (info.Width < 2 || info.Height < 2 || (long)info.Width * info.Height > 20_000_000)
            {
                ModelState.AddModelError(field, "Розміри фото: щонайменше 2 × 2 пікселі та не більше 20 мегапікселів.");
                return null;
            }
            using var decoded = SixLabors.ImageSharp.Image.Load(bytes);
            return bytes;
        }
        catch (InvalidDataException ex)
        {
            ModelState.AddModelError(string.Empty, $"{field}: {ex.Message}");
            return null;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        {
            ModelState.AddModelError(string.Empty, $"{field}: потрібне коректне зображення JPEG/PNG. Посилання на вебсторінку не підходить.");
            return null;
        }
    }

    private void ValidateQuality(ImageQualityResult quality, string field)
    {
        if (quality.IsTooDark) ModelState.AddModelError(field, "Фото занадто темне. Оберіть світліше фото.");
        if (quality.IsBlurry) ModelState.AddModelError(field, "Фото недостатньо чітке. Оберіть чіткіше фото.");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();
}
