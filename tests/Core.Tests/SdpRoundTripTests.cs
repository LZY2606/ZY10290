using PairwiseGsb.Core.Fixtures;
using PairwiseGsb.Core.Sdp;

namespace PairwiseGsb.Core.Tests;

public class SdpRoundTripTests
{
    public static IEnumerable<object[]> AllDocs()
    {
        yield return new object[] { BuiltInFixture.Round1Offer };
        yield return new object[] { BuiltInFixture.Round1Answer };
        yield return new object[] { BuiltInFixture.Round2Offer };
        yield return new object[] { BuiltInFixture.Round2Answer };
        yield return new object[] { BuiltInFixture.Round3Offer };
        yield return new object[] { BuiltInFixture.Round3Answer };
    }

    [Theory]
    [MemberData(nameof(AllDocs))]
    public void Serialize_IsByteIdentical_ToOriginal(string original)
    {
        var doc = SdpDocument.Parse(original);
        Assert.Equal(original, doc.Serialize());
    }

    [Fact]
    public void NewLineStyle_IsPreserved()
    {
        Assert.Equal("\r\n", SdpDocument.Parse(BuiltInFixture.Round1Offer).NewLine);
        var lf = SdpDocument.Parse("v=0\ns=x\n");
        Assert.Equal("\n", lf.NewLine);
        Assert.Equal("v=0\ns=x\n", lf.Serialize());
    }

    [Fact]
    public void UnknownAttributes_AreKeptInOriginalOrder()
    {
        var doc = SdpDocument.Parse(BuiltInFixture.Round1Offer);
        Assert.Equal("round1-offer", doc.Session.GetAttribute("x-acme-trace"));

        var video = doc.Media[1];
        var unknown = video.UnknownAttributes().Select(l => l.Raw).ToArray();
        Assert.Equal(new[] { "a=x-vendor-42:keepme" }, unknown);
    }

    [Fact]
    public void PayloadTypes_AreScopedPerMedia()
    {
        var doc = SdpDocument.Parse(BuiltInFixture.Round2Offer);
        Assert.Equal("OPUS/48000/2", doc.Media[0].CodecKeyFor("111"));
        Assert.Equal("VP9/90000/1", doc.Media[1].CodecKeyFor("111"));
    }
}
