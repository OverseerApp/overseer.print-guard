using System.Reflection;
using System.Text.Json;
using log4net;

namespace Overseer.PrintGuard;

public static class PrintGuardPrototypes
{
  private static readonly ILog _log = LogManager.GetLogger(typeof(PrintGuardPrototypes));

  private static readonly Lazy<Dictionary<string, float[]>> _prototypes = new(() =>
  {
    _log.Info("Loading print guard prototypes from embedded resources.");
    var prototypesJson = LoadEmbeddedResource("Overseer.PrintGuard.Resources.print_guard_prototypes.json");
    var prototypesDict = JsonSerializer.Deserialize<Dictionary<string, float[]>>(prototypesJson);
    return prototypesDict ?? [];
  });

  public static Dictionary<string, float[]> Get() => _prototypes.Value;

  private static byte[] LoadEmbeddedResource(string resourceName)
  {
    var assembly = Assembly.GetExecutingAssembly();
    using var stream = assembly.GetManifestResourceStream(resourceName);

    if (stream == null)
    {
      var availableResources = assembly.GetManifestResourceNames();
      _log.Error($"Embedded resource '{resourceName}' not found. Available resources: {string.Join(", ", availableResources)}");
      throw new InvalidOperationException(
        $"Embedded resource '{resourceName}' not found. Available resources: {string.Join(", ", availableResources)}"
      );
    }

    using var memoryStream = new MemoryStream();
    stream.CopyTo(memoryStream);
    return memoryStream.ToArray();
  }
}
