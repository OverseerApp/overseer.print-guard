namespace Overseer.PrintGuard;

public interface IPrintGuardCameraStreamer
{
  void Start(string url);
  void Stop();
  float[] GetProcessedFrame();
}
