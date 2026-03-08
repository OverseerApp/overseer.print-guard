using log4net;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Overseer.PrintGuard;

public class PrintGuardModel : IDisposable
{
  private static readonly ILog _log = LogManager.GetLogger(typeof(PrintGuardModel));
  private const string ModelUrl = "https://huggingface.co/oliverbravery/PrintGuard/resolve/main/model.onnx";
  private const string ModelFileName = "model.onnx";
  private const string OverseerDirectoryName = "overseer";

  private readonly IHttpClientFactory _httpClientFactory;
  private readonly Lazy<InferenceSession> _lazySession;
  private string? _inputName;

  public PrintGuardModel(IHttpClientFactory httpClientFactory)
  {
    _httpClientFactory = httpClientFactory;
    _lazySession = new Lazy<InferenceSession>(InitializeSession, LazyThreadSafetyMode.ExecutionAndPublication);
  }

  private InferenceSession InitializeSession()
  {
    _log.Info("Initializing ONNX InferenceSession.");
    var modelPath = GetModelPath();
    _log.Debug($"Model path: {modelPath}");
    EnsureModelDownloaded(modelPath, _httpClientFactory);

    var options = new SessionOptions();
    // Using default SessionOptions; adjust here if GPU or custom optimization is required.
    var session = new InferenceSession(modelPath, options);
    _inputName = session.InputMetadata.Keys.First();
    return session;
  }

  private static string GetModelPath()
  {
    var userDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    return Path.Combine(userDirectory, OverseerDirectoryName, ModelFileName);
  }

  private static void EnsureModelDownloaded(string modelPath, IHttpClientFactory httpClientFactory)
  {
    if (File.Exists(modelPath))
    {
      _log.Debug("Model file already exists.");
      return;
    }

    _log.Info($"Downloading model from {ModelUrl} to {modelPath}...");
    var directory = Path.GetDirectoryName(modelPath);
    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      Directory.CreateDirectory(directory);

    try
    {
      using var httpClient = httpClientFactory.CreateClient();
      httpClient.Timeout = TimeSpan.FromMinutes(10);

      using var request = new HttpRequestMessage(HttpMethod.Get, ModelUrl);
      using var response = httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
      response.EnsureSuccessStatusCode();

      using var contentStream = response.Content.ReadAsStream();
      using var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None);
      contentStream.CopyTo(fileStream);
    }
    catch (Exception ex)
    {
      _log.Error($"Model download failed: {ex.Message}");
      if (File.Exists(modelPath))
        File.Delete(modelPath);
      throw;
    }

    var downloadedFileSize = new FileInfo(modelPath).Length;
    if (downloadedFileSize == 0)
    {
      _log.Error("Downloaded model file is empty.");
      File.Delete(modelPath);
      throw new InvalidOperationException("Downloaded model file is empty or corrupt.");
    }

    _log.Info($"Model download complete. File size: {downloadedFileSize} bytes.");
  }

  public float[] GetEmbedding(float[] normalizedImageData)
  {
    _log.Debug("Getting embedding for processed frame...");
    // Ensure the session is initialized before using _inputName
    var session = _lazySession.Value;

    // 1. Create the input tensor
    // Shape for ShuffleNet/ImageNet: [BatchSize, Channels, Height, Width]
    var inputTensor = new DenseTensor<float>(normalizedImageData, [1, 3, 224, 224]);

    // 2. Wrap in NamedOnnxValue
    var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, inputTensor) };

    // 4. Extract the embedding as an array

    // 3. Run Inference
    using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Run(inputs);
    // We take the first output as a float array
    return [.. results[0].AsEnumerable<float>()];
  }

  public void Dispose()
  {
    if (_lazySession.IsValueCreated)
      _lazySession.Value.Dispose();
    GC.SuppressFinalize(this);
  }
}
