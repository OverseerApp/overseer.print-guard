using log4net;
using Overseer.Server.Integration.Automation;

namespace Overseer.PrintGuard;

public class PrintGuardFailureDetectionAnalyzer(PrintGuardModel model, IPrintGuardCameraStreamer cameraStreamer) : IFailureDetectionAnalyzer
{
  private static readonly ILog _log = LogManager.GetLogger(typeof(PrintGuardFailureDetectionAnalyzer));
  private readonly int _windowSize = 20;
  private readonly double _threshold = 0.7;
  private readonly Queue<FailureDetectionAnalysisResult> _history = new();
  private readonly SemaphoreSlim _semaphore = new(1, 1);

  public FailureDetectionAnalysisResult Analyze()
  {
    _semaphore.Wait();
    try
    {
      _log.Debug("Analyzing current frame...");
      var frame = cameraStreamer.GetProcessedFrame();
      if (frame == null || frame.Length == 0)
      {
        _log.Warn("No frame data available from camera streamer yet. Waiting for stream.");
        return new FailureDetectionAnalysisResult
        {
          IsFailureDetected = false,
          ConfidenceScore = 0.0,
          FailureReason = "Waiting",
          Details = "Waiting for initial frame data from the camera stream.",
        };
      }

      return AnalyzeFrame(frame);
    }
    finally
    {
      _semaphore.Release();
    }
  }

  /// <summary>
  /// Calculates the Euclidean Distance between two vectors.
  /// </summary>
  private static double CalculateDistance(float[] vectorA, float[] vectorB)
  {
    if (vectorA.Length != vectorB.Length)
      throw new ArgumentException("Vectors must be the same length.");

    double sum = 0;
    for (int i = 0; i < vectorA.Length; i++)
    {
      double diff = vectorA[i] - vectorB[i];
      sum += diff * diff;
    }

    return Math.Sqrt(sum);
  }

  /// <summary>
  /// Determines if the current frame is a success or failure.
  /// </summary>
  private FailureDetectionAnalysisResult AnalyzeFrame(float[] frame)
  {
    var currentEmbedding = model.GetEmbedding(frame);
    string bestLabel = "Undetermined";
    double shortestDistance = double.MaxValue;

    foreach (var proto in PrintGuardPrototypes.Get())
    {
      double dist = CalculateDistance(currentEmbedding, proto.Value);
      if (dist < shortestDistance)
      {
        shortestDistance = dist;
        bestLabel = proto.Key;
      }
    }

    bool isFailure = !bestLabel.Equals("success", StringComparison.CurrentCultureIgnoreCase);
    // Normalize distance to [0,1] for confidence
    var confidenceScore = Math.Clamp(1.0 - (shortestDistance / 10.0), 0.0, 1.0);
    var result = new FailureDetectionAnalysisResult
    {
      IsFailureDetected = isFailure,
      ConfidenceScore = confidenceScore,
      FailureReason = isFailure ? bestLabel : "None",
      Details = $"Detected {bestLabel} with confidence of: {confidenceScore:F4}",
    };

    _history.Enqueue(result);

    if (_history.Count > _windowSize)
    {
      _history.Dequeue();
    }

    if (_history.Count < _windowSize)
    {
      return new FailureDetectionAnalysisResult
      {
        IsFailureDetected = false,
        ConfidenceScore = 0.0,
        FailureReason = "Insufficient Data",
        Details = "Not enough data collected to determine failure.",
      };
    }

    int failureCount = _history.Count(x => x.IsFailureDetected);
    if ((double)failureCount / _history.Count >= _threshold)
    {
      _log.Warn($"Failure threshold reached ({failureCount}/{_history.Count}). Reporting failure.");
      var topFailure = _history
        .Where(x => x.IsFailureDetected)
        .GroupBy(x => x.FailureReason)
        .OrderByDescending(g => g.Count())
        .Select(g => g.FirstOrDefault())
        .FirstOrDefault();

      _history.Clear();

      return topFailure
        ?? new FailureDetectionAnalysisResult
        {
          IsFailureDetected = true,
          ConfidenceScore = 1.0,
          FailureReason = "Undetermined",
          Details = "Failure detected but unable to determine reason.",
        };
    }

    return new FailureDetectionAnalysisResult
    {
      IsFailureDetected = false,
      ConfidenceScore = 1.0,
      FailureReason = "No Failure",
      Details = "No failure detected based on recent analysis.",
    };
  }

  public void Start(string url)
  {
    _log.Info("Starting failure detection analyzer.");
    cameraStreamer.Start(url);
  }

  public void Stop()
  {
    _log.Info("Stopping failure detection analyzer.");
    cameraStreamer.Stop();
  }
}
