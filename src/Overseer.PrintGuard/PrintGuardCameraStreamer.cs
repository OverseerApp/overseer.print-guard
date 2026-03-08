using Emgu.CV;
using Emgu.CV.CvEnum;
using log4net;

namespace Overseer.PrintGuard;

public class PrintGuardCameraStreamer : IPrintGuardCameraStreamer, IDisposable
{
  private static readonly ILog _log = LogManager.GetLogger(typeof(PrintGuardCameraStreamer));
  private VideoCapture? _capture;
  private Mat _latestFrame = new();
  private readonly Lock _frameLock = new();
  private bool _disposed;

  public void Start(string url)
  {
    _log.Info($"Starting camera streamer for URL: {url}");
    _capture = new VideoCapture(url);
    _capture.Set(CapProp.FourCC, VideoWriter.Fourcc('M', 'J', 'P', 'G'));
    _capture.ImageGrabbed += (s, e) =>
    {
      lock (_frameLock)
      {
        // Grabs the frame into the Mat without blocking the main thread
        _capture?.Retrieve(_latestFrame);
      }
    };

    _capture.Start();
  }

  public void Stop()
  {
    if (_capture != null)
    {
      _log.Info("Stopping camera streamer.");
      _capture.Stop();
      _capture.Dispose();
      _capture = null;
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing)
    {
      Stop();
      lock (_frameLock)
      {
        _latestFrame.Dispose();
      }
    }

    _disposed = true;
  }

  public float[] GetProcessedFrame()
  {
    lock (_frameLock)
    {
      using var frame = _latestFrame.Clone();
      return PreprocessImage(frame);
    }
  }

  public static float[] PreprocessImage(Mat frame)
  {
    if (frame.IsEmpty)
    {
      _log.Warn("PreprocessImage received an empty frame. Returning an empty feature array.");
      return [];
    }

    // 1. Define ImageNet constants
    float[] mean = [0.485f, 0.456f, 0.406f];
    float[] std = [0.229f, 0.224f, 0.225f];
    int targetSize = 256; // Resize to 256 first
    int cropSize = 224; // Then center crop to 224

    using var grayFrame = new Mat();
    CvInvoke.CvtColor(frame, grayFrame, ColorConversion.Bgr2Gray);

    // Calculate scaling to ensure shorter side is targetSize
    double scale = Math.Max((double)targetSize / grayFrame.Width, (double)targetSize / grayFrame.Height);
    int newWidth = (int)Math.Round(grayFrame.Width * scale);
    int newHeight = (int)Math.Round(grayFrame.Height * scale);

    using var scaledFrame = new Mat();
    CvInvoke.Resize(grayFrame, scaledFrame, new System.Drawing.Size(newWidth, newHeight));

    // Center crop to targetSize x targetSize
    int targetX = (newWidth - targetSize) / 2;
    int targetY = (newHeight - targetSize) / 2;
    System.Drawing.Rectangle targetRoi = new System.Drawing.Rectangle(targetX, targetY, targetSize, targetSize);
    using var resizedFrame = new Mat(scaledFrame, targetRoi);

    // Center crop to cropSize x cropSize
    int cropX = (targetSize - cropSize) / 2;
    int cropY = (targetSize - cropSize) / 2;
    System.Drawing.Rectangle roi = new System.Drawing.Rectangle(cropX, cropY, cropSize, cropSize);
    using var croppedFrame = new Mat(resizedFrame, roi);

    // Ensure continuous memory block for direct array access
    using var clonedContinuous = croppedFrame.IsContinuous ? null : croppedFrame.Clone();
    var continuousCropped = clonedContinuous ?? croppedFrame;

    // 3. Prepare the flat array (CHW format: RRR... GGG... BBB...)
    // Even though grayscale, we replicate across 3 channels (as PrintGuard does)
    float[] normalizedData = new float[3 * cropSize * cropSize];

    byte[] grayPixels = new byte[cropSize * cropSize];
    System.Runtime.InteropServices.Marshal.Copy(continuousCropped.DataPointer, grayPixels, 0, grayPixels.Length);

    for (int i = 0; i < cropSize * cropSize; i++)
    {
      float grayValue = grayPixels[i] / 255.0f;
      // Red Channel
      normalizedData[0 * cropSize * cropSize + i] = (grayValue - mean[0]) / std[0];
      // Green Channel
      normalizedData[1 * cropSize * cropSize + i] = (grayValue - mean[1]) / std[1];
      // Blue Channel
      normalizedData[2 * cropSize * cropSize + i] = (grayValue - mean[2]) / std[2];
    }

    return normalizedData;
  }
}
