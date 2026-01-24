using Overseer.Server.Integration.Automation;

namespace Overseer.PrintGuard.Tests;

public class PrintGuardFailureDetectionAnalyzerTests
{
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

    public PrintGuardFailureDetectionAnalyzerTests()
    {
        var httpClient = new HttpClient();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        _mockHttpClientFactory.Setup(_ => _.CreateClient(It.IsAny<string>())).Returns(httpClient);
    }

    [Fact]
    public void ShouldDetectFailure()
    {
        var analyzer = new PrintGuardFailureDetectionAnalyzer(
            new PrintGuardModel(_mockHttpClientFactory.Object),
            new FakeCameraStreamer("Overseer.PrintGuard.Tests.Resources.fail.jpg")
        );

        FailureDetectionAnalysisResult? result = null;
        for (var i = 0; i < 30; i++)
        {
            result = analyzer.Analyze();
            if (result.IsFailureDetected)
            {
                break;
            }
        }

        Assert.NotNull(result);
        Assert.True(result.IsFailureDetected);
    }

    [Fact]
    public void ShouldNotDetectFailure()
    {
        var analyzer = new PrintGuardFailureDetectionAnalyzer(
            new PrintGuardModel(_mockHttpClientFactory.Object),
            new FakeCameraStreamer("Overseer.PrintGuard.Tests.Resources.pass.jpg")
        );

        FailureDetectionAnalysisResult? result = null;
        for (var i = 0; i < 30; i++)
        {
            result = analyzer.Analyze();
            if (result.IsFailureDetected)
            {
                break;
            }
        }

        Assert.NotNull(result);
        Assert.False(result.IsFailureDetected);
    }
}
