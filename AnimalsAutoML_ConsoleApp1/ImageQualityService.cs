using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AnimalsAutoML_ConsoleApp1
{
    public class ImageQualityResult
    {
        public bool IsGood { get; set; }
        public bool IsTooDark { get; set; }
        public bool IsBlurry { get; set; }

        public double Brightness { get; set; }
        public double Sharpness { get; set; }
    }


    public class ImageQualityService
    {
        public ImageQualityResult CheckQuality(byte[] imageBytes)
        {
            using var image =
                Image.Load<Rgba32>(imageBytes);

            double brightness =
                CalculateBrightness(image);

            double sharpness =
                CalculateSharpness(image);

            bool tooDark = brightness < 45;
            bool blurry = sharpness < 1.5;

            return new ImageQualityResult
            {
                Brightness = brightness,
                Sharpness = sharpness,

                IsTooDark = tooDark,
                IsBlurry = blurry,

                IsGood = !tooDark && !blurry
            };
        }


        private double CalculateBrightness(
            Image<Rgba32> image)
        {
            double totalBrightness = 0;
            long pixelCount = 0;

            for (int y = 0; y < image.Height; y += 5)
            {
                for (int x = 0; x < image.Width; x += 5)
                {
                    var pixel = image[x, y];

                    double brightness =
                        0.299 * pixel.R +
                        0.587 * pixel.G +
                        0.114 * pixel.B;

                    totalBrightness += brightness;

                    pixelCount++;
                }
            }

            return totalBrightness / pixelCount;
        }


        private double CalculateSharpness(
            Image<Rgba32> image)
        {
            double differenceSum = 0;
            long count = 0;

            for (int y = 0;
                 y < image.Height - 1;
                 y += 5)
            {
                for (int x = 0;
                     x < image.Width - 1;
                     x += 5)
                {
                    var current = image[x, y];
                    var right = image[x + 1, y];
                    var bottom = image[x, y + 1];

                    double currentGray =
                        (current.R +
                         current.G +
                         current.B) / 3.0;

                    double rightGray =
                        (right.R +
                         right.G +
                         right.B) / 3.0;

                    double bottomGray =
                        (bottom.R +
                         bottom.G +
                         bottom.B) / 3.0;

                    differenceSum +=
                        Math.Abs(
                            currentGray - rightGray
                        );

                    differenceSum +=
                        Math.Abs(
                            currentGray - bottomGray
                        );

                    count += 2;
                }
            }

            return count == 0 ? 0 : differenceSum / count;
        }
    }
}
