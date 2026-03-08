using log4net;
using Microsoft.Extensions.DependencyInjection;
using Overseer.Server.Integration;
using Overseer.Server.Integration.Automation;

namespace Overseer.PrintGuard;

public class PrintGuardPluginConfiguration : IPluginConfiguration
{
  private static readonly ILog _log = LogManager.GetLogger(typeof(PrintGuardPluginConfiguration));

  public void ConfigureServices(IServiceCollection services)
  {
    _log.Info("Configuring PrintGuard plugin services");
    services.AddSingleton<PrintGuardModel>();
    services.AddTransient<IPrintGuardCameraStreamer, PrintGuardCameraStreamer>();
    services.AddTransient<IFailureDetectionAnalyzer, PrintGuardFailureDetectionAnalyzer>();
  }
}
