using PairwiseGsb.App.Web;
using PairwiseGsb.Core.Analysis;
using PairwiseGsb.Core.Fixtures;

namespace PairwiseGsb.Core.Tests;

public class AppPageTests
{
    [Fact]
    public void Page_ContainsTitleAndDiagnostics()
    {
        var graph = new NegotiationAnalyzer().Analyze(BuiltInFixture.Rounds());
        var html = GraphPage.Render(graph);
        Assert.Contains("协商岔路图", html);
        Assert.Contains("bundle-master-migration", html);
        Assert.Contains("late-trickle-candidate", html);
    }
}
