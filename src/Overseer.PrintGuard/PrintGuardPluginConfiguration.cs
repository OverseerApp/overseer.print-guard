using Microsoft.Extensions.DependencyInjection;
using Overseer.Server.Integration;
using Overseer.Server.Integration.Automation;

namespace Overseer.PrintGuard;

public class PrintGuardPluginConfiguration : IPluginConfiguration
{
  public void ConfigureServices(IServiceCollection services)
  {
    services.AddSingleton<PrintGuardModel>();
    services.AddTransient<IPrintGuardCameraStreamer, PrintGuardCameraStreamer>();
    services.AddTransient<IFailureDetectionAnalyzer, PrintGuardFailureDetectionAnalyzer>();
  }
}
