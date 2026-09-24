using System.ComponentModel.DataAnnotations;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using AnimalsAutoML_ConsoleApp1;
using AnimalsAutoML_ConsoleApp1.Controllers;
using AnimalsAutoML_ConsoleApp1.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SixLabors.ImageSharp.PixelFormats;

using var image = new SixLabors.ImageSharp.Image<Rgba32>(100, 100);
for (int y = 0; y < 100; y++)
    for (int x = 0; x < 100; x++)
        image[x, y] = (x + y) % 2 == 0 ? new Rgba32(255, 255, 255) : new Rgba32(0, 0, 0);
using var png = new MemoryStream();
image.Save(png, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
var downloader = new FakeDownloader(png.ToArray());
var aws = new FakeAws();
using var provider = new ServiceCollection().AddSingleton<IAmazonRekognition>(aws).AddScoped<RekognitionService>().BuildServiceProvider();
int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS: " + name); }
IFormFile File() => new FormFile(new MemoryStream(png.ToArray()), 0, png.Length, "Photo", "photo.png");
async Task<(CompareFacesViewModel, HomeController)> Submit(CompareFacesViewModel model)
{
    var controller = new HomeController(new ImageQualityService(), provider, downloader, NullLogger<HomeController>.Instance);
    var errors = new List<ValidationResult>();
    Validator.TryValidateObject(model, new ValidationContext(model), errors, true);
    foreach (var error in errors) controller.ModelState.AddModelError(error.MemberNames.FirstOrDefault() ?? "", error.ErrorMessage!);
    var result = (ViewResult)await controller.Index(model, default);
    return ((CompareFacesViewModel)result.Model!, controller);
}
var (result, controller) = await Submit(new() { FirstPhotoUrl = "https://example.com/first.png", SecondPhotoUrl = "https://example.com/second.png" });
Check(result.Compared && result.Similarity == 98.5f && downloader.Calls == 2, "Two URLs reach comparison");
(result, controller) = await Submit(new() { FirstPhoto = File(), SecondPhotoUrl = "https://example.com/second.png" });
Check(result.Compared, "File plus URL");
(result, controller) = await Submit(new() { FirstPhoto = File(), SecondPhoto = File() });
Check(result.Compared, "Two uploads still work");
(result, controller) = await Submit(new());
Check(!result.Compared && controller.ModelState.ErrorCount == 2, "Missing inputs rejected");
(result, controller) = await Submit(new() { FirstPhoto = File(), FirstPhotoUrl = "https://example.com/a.png", SecondPhoto = File() });
Check(!result.Compared && !controller.ModelState.IsValid, "Ambiguous file and URL rejected");
var calls = aws.Calls;
(result, controller) = await Submit(new() { FirstPhotoUrl = "https://example.com/error", SecondPhoto = File() });
Check(!result.Compared && aws.Calls == calls && !controller.ModelState.IsValid, "URL failure shown without calling AWS");
(result, controller) = await Submit(new() { FirstPhotoUrl = "https://example.com/html", SecondPhoto = File() });
Check(!result.Compared && !controller.ModelState.IsValid, "HTML URL rejected as non-image");
(result, controller) = await Submit(new() { FirstPhotoUrl = "file:///c:/photo.png", SecondPhoto = File() });
Check(!result.Compared, "Non-HTTP URL rejected");
foreach (var ip in new[] { "127.0.0.1", "10.0.0.1", "169.254.169.254", "::1", "::ffff:127.0.0.1" })
    Check(!ImageUrlDownloader.IsPublicAddress(System.Net.IPAddress.Parse(ip)), "Private address blocked: " + ip);
using var oversized = new MemoryStream(new byte[5 * 1024 * 1024 + 1]);
try { await ImageUrlDownloader.ReadLimitedAsync(oversized, default); throw new Exception("Size limit not enforced"); }
catch (InvalidDataException) { Check(true, "Stream size limit enforced"); }
Console.WriteLine($"{checks} checks passed without external requests.");

sealed class FakeDownloader(byte[] bytes) : IImageUrlDownloader
{
    public int Calls;
    public Task<byte[]> DownloadAsync(string url, CancellationToken token)
    {
        Calls++;
        ImageUrlDownloader.ValidateUrl(url);
        if (url.EndsWith("error")) throw new InvalidDataException("Download failed");
        return Task.FromResult(url.EndsWith("html") ? System.Text.Encoding.UTF8.GetBytes("<html>Not an image</html>") : bytes);
    }
}
sealed class FakeAws() : AmazonRekognitionClient(new Amazon.Runtime.AnonymousAWSCredentials(), Amazon.RegionEndpoint.EUCentral1)
{
    public int Calls;
    public override Task<CompareFacesResponse> CompareFacesAsync(CompareFacesRequest request, CancellationToken token = default)
    {
        Calls++;
        if (request.SourceImage.Bytes.Length == 0 || request.TargetImage.Bytes.Length == 0) throw new Exception("Missing image data");
        return Task.FromResult(new CompareFacesResponse { FaceMatches = [new CompareFacesMatch { Similarity = 98.5f }] });
    }
}
