using PairwiseGsb.Core.Sdp;

namespace PairwiseGsb.Core.Analysis;

/// <summary>一轮 offer/answer，附带该轮之后到达的 trickle candidate。</summary>
public sealed class Round
{
    public required int Index { get; init; }
    public required SdpDocument Offer { get; init; }
    public required SdpDocument Answer { get; init; }

    /// <summary>该轮信令完成后才到达的 candidate（晚到 trickle）。</summary>
    public IReadOnlyList<TrickledCandidate> Trickle { get; init; } = Array.Empty<TrickledCandidate>();
}

public sealed record TrickledCandidate(string Mid, Candidate Candidate, bool ArrivedAfterAnswer);

public enum Severity { Info, Warning, Error }

public sealed record Diagnostic(
    Severity Severity, string Code, string Message,
    int RoundIndex, int? MediaIndex);

/// <summary>一个 m-line 在某一轮的协商结果。</summary>
public sealed class MediaNegotiation
{
    public required int MediaIndex { get; init; }
    public required string? Mid { get; init; }
    public required string Media { get; init; }
    public required bool Rejected { get; init; }
    public required string OfferDirection { get; init; }
    public required string? AnswerDirection { get; init; }
    public required string? NegotiatedDirection { get; init; }
    public required IReadOnlyList<string> CodecIntersection { get; init; }
    public required bool OwnsBundleTransport { get; init; }
    public required int? IceGeneration { get; init; }
}

/// <summary>一轮的协商图。</summary>
public sealed class RoundGraph
{
    public required int Index { get; init; }
    public required string? BundleMasterMid { get; init; }
    public required int IceGeneration { get; init; }
    public required List<MediaNegotiation> Media { get; init; }
    public required List<Diagnostic> Diagnostics { get; init; }
}

public sealed class NegotiationGraph
{
    public List<RoundGraph> Rounds { get; } = new();
    public List<Diagnostic> Diagnostics => Rounds.SelectMany(r => r.Diagnostics).ToList();
}
