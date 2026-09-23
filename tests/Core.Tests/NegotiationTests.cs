using PairwiseGsb.Core.Analysis;
using PairwiseGsb.Core.Fixtures;

namespace PairwiseGsb.Core.Tests;

public class NegotiationTests
{
    private static NegotiationGraph Graph() => new NegotiationAnalyzer().Analyze(BuiltInFixture.Rounds());

    [Fact]
    public void RejectedMedia_KeepsItsPosition()
    {
        var graph = Graph();
        var round1 = graph.Rounds[0];
        Assert.Equal(3, round1.Media.Count);
        Assert.False(round1.Media[0].Rejected);
        Assert.False(round1.Media[1].Rejected);
        Assert.True(round1.Media[2].Rejected);
        Assert.Equal("2", round1.Media[2].Mid);
        Assert.Contains(round1.Diagnostics, d => d.Code == "media-rejected" && d.MediaIndex == 2);
    }

    [Fact]
    public void RejectedMedia_RecycledInLaterRound()
    {
        var graph = Graph();
        var round3 = graph.Rounds[2];
        Assert.False(round3.Media[2].Rejected);
        Assert.Contains(round3.Diagnostics, d => d.Code == "media-recycled" && d.MediaIndex == 2);
    }

    [Fact]
    public void IceGeneration_IncrementsOnRestartOnly()
    {
        var graph = Graph();
        Assert.Equal(0, graph.Rounds[0].IceGeneration);
        Assert.Equal(1, graph.Rounds[1].IceGeneration);
        Assert.Equal(1, graph.Rounds[2].IceGeneration);
        Assert.Contains(graph.Rounds[1].Diagnostics, d => d.Code == "ice-credential-change");
        Assert.DoesNotContain(graph.Rounds[2].Diagnostics, d => d.Code == "ice-credential-change");
    }

    [Fact]
    public void BundleTransportOwnership_FollowsMaster()
    {
        var graph = Graph();
        Assert.Equal("0", graph.Rounds[0].BundleMasterMid);
        Assert.True(graph.Rounds[0].Media[0].OwnsBundleTransport);
        Assert.False(graph.Rounds[0].Media[1].OwnsBundleTransport);

        Assert.Equal("1", graph.Rounds[1].BundleMasterMid);
        Assert.True(graph.Rounds[1].Media[1].OwnsBundleTransport);
        Assert.Contains(graph.Rounds[1].Diagnostics, d => d.Code == "bundle-master-migration");
    }

    [Fact]
    public void Direction_IsNegotiatedFromOffererPerspective()
    {
        var graph = Graph();
        var video = graph.Rounds[0].Media[1];
        Assert.Equal("sendrecv", video.OfferDirection);
        Assert.Equal("sendonly", video.AnswerDirection);
        Assert.Equal("recvonly", video.NegotiatedDirection);

        var audio = graph.Rounds[0].Media[0];
        Assert.Equal("sendrecv", audio.NegotiatedDirection);
    }

    [Fact]
    public void CodecIntersection_UsesCodecIdentityNotPt()
    {
        var graph = Graph();
        Assert.Equal(new[] { "OPUS/48000/2" }, graph.Rounds[0].Media[0].CodecIntersection);
        Assert.Equal(new[] { "VP8/90000/1" }, graph.Rounds[0].Media[1].CodecIntersection);
        Assert.Equal(new[] { "VP8/90000/1", "VP9/90000/1" }, graph.Rounds[1].Media[1].CodecIntersection);
    }

    [Fact]
    public void SameDynamicPt_DifferentCodec_IsDiagnosed()
    {
        var graph = Graph();
        Assert.Contains(graph.Rounds[1].Diagnostics,
            d => d.Code == "pt-codec-mismatch" && d.Message.Contains("111"));
    }

    [Fact]
    public void ExtmapConflict_IsDiagnosed()
    {
        var graph = Graph();
        Assert.Contains(graph.Rounds[1].Diagnostics,
            d => d.Code == "extmap-conflict" && d.MediaIndex == 1);
        Assert.DoesNotContain(graph.Rounds[2].Diagnostics, d => d.Code == "extmap-conflict");
    }

    [Fact]
    public void LateTrickleCandidate_IsDiagnosed()
    {
        var graph = Graph();
        Assert.Contains(graph.Rounds[2].Diagnostics, d => d.Code == "late-trickle-candidate");
        Assert.DoesNotContain(graph.Rounds[0].Diagnostics, d => d.Code == "late-trickle-candidate");
    }
}
