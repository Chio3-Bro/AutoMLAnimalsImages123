using Amazon.Rekognition;
using Amazon.Runtime;
using AnimalsAutoML_ConsoleApp1.Models;
using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;

namespace AnimalsAutoML_ConsoleApp1.Controllers;

public class HomeController(ImageQualityService qualityService, IServiceProvider services,
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
        var first = await ReadPhotoAsync(model.FirstPhoto!, nameof(model.FirstPhoto), cancellationToken);
        var second = await ReadPhotoAsync(model.SecondPhoto!, nameof(model.SecondPhoto), cancellationToken);
        if (!ModelState.IsValid) return View(model);
        model.FirstQuality = qualityService.CheckQuality(first!);
        model.SecondQuality = qualityService.CheckQuality(second!);
        ValidateQuality(model.FirstQuality, nameof(model.FirstPhoto));
        ValidateQuality(model.SecondQuality, nameof(model.SecondPhoto));
        if (!ModelState.IsValid) return View(model);
        try
        {
            // Resolve AWS after local validation so the page works without credentials.
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

    private async Task<byte[]?> ReadPhotoAsync(IFormFile file, string field, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxPhotoBytes)
        {
            ModelState.AddModelError(field, "Фото має бути непорожнім і не перевищувати 5 МіБ.");
            return null;
        }
        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        var bytes = stream.ToArray();
        try
        {
            var info = SixLabors.ImageSharp.Image.Identify(bytes);
            if (info.Metadata.DecodedImageFormat?.Name is not ("JPEG" or "PNG"))
                throw new InvalidDataException();
            if (info.Width < 2 || info.Height < 2 || (long)info.Width * info.Height > 20_000_000)
            {
                ModelState.AddModelError(field, "Розміри фото: щонайменше 2 × 2 пікселі та не більше 20 мегапікселів.");
                return null;
            }
            using var decoded = SixLabors.ImageSharp.Image.Load(bytes);
            return bytes;
        }
        catch (Exception ex) when (ex is UnknownImageFormatException or InvalidImageContentException or InvalidDataException or NotSupportedException)
        {
            ModelState.AddModelError(field, "Завантажте коректне зображення JPEG або PNG.");
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
