using Amazon;
using Amazon.Rekognition;
using AnimalsAutoML_ConsoleApp1;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
    options.MemoryBufferThreshold = 11 * 1024 * 1024);
builder.Services.AddSingleton<ImageQualityService>();
builder.Services.AddScoped<RekognitionService>();
builder.Services.AddSingleton<IAmazonRekognition>(_ => new AmazonRekognitionClient(
    AccessTokens.AwsAccessKeyId,
    AccessTokens.AwsSecretAccessKey,
    RegionEndpoint.GetBySystemName(builder.Configuration["AWS:Region"] ?? "eu-central-1")));
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
app.Run();
