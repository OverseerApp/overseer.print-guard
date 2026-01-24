namespace Overseer.PrintGuard.Tests;

/// <summary>
/// A fake camera streamer that loads images from embedded resources for testing.
/// </summary>
public class FakeCameraStreamer(string resourceName) : IPrintGuardCameraStreamer
{
    public float[] GetProcessedFrame()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return [];

        // Load directly with ImageSharp to avoid Emgu.CV native dependency
        using var image = Image.Load<Rgb24>(stream);

        // Preprocess inline (same logic as CameraStreamer.PreprocessImage)
        float[] mean = [0.485f, 0.456f, 0.406f];
        float[] std = [0.229f, 0.224f, 0.225f];
        int targetSize = 256; // Resize to 256 first
        int cropSize = 224; // Then center crop to 224

        // Convert to grayscale (matching PrintGuard's preprocessing)
        image.Mutate(x => x.Grayscale());

        // Resize to 256
        image.Mutate(x =>
            x.Resize(
                new ResizeOptions
                {
                    Size = new Size(targetSize, targetSize),
                    Mode = ResizeMode.Crop,
                }
            )
        );

        // Center crop to 224x224
        int cropX = (targetSize - cropSize) / 2;
        int cropY = (targetSize - cropSize) / 2;
        image.Mutate(x => x.Crop(new Rectangle(cropX, cropY, cropSize, cropSize)));

        float[] normalizedData = new float[3 * cropSize * cropSize];

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var pixel = image[x, y];
                // Grayscale image - all RGB channels have the same value
                // Normalize the grayscale value and replicate across all 3 channels
                float grayValue = pixel.R / 255.0f;

                normalizedData[0 * cropSize * cropSize + y * cropSize + x] =
                    (grayValue - mean[0]) / std[0];
                normalizedData[1 * cropSize * cropSize + y * cropSize + x] =
                    (grayValue - mean[1]) / std[1];
                normalizedData[2 * cropSize * cropSize + y * cropSize + x] =
                    (grayValue - mean[2]) / std[2];
            }
        }

        return normalizedData;
    }

    public void Start(string url)
    {
        return;
    }

    public void Stop()
    {
        return;
    }
}
