using PairwiseGsb.Core.Sdp;

namespace PairwiseGsb.Core.Analysis;

/// <summary>
/// 按 offer/answer 规则（RFC 3264 / BUNDLE / ICE restart）构建逐轮协商图。
/// </summary>
public sealed class NegotiationAnalyzer
{
    public NegotiationGraph Analyze(IReadOnlyList<Round> rounds)
    {
        var graph = new NegotiationGraph();
        string? prevBundleMaster = null;
        (string? Ufrag, string? Pwd) prevCreds = (null, null);
        var iceGeneration = 0;
        var prevRejected = new HashSet<int>();

        foreach (var round in rounds)
        {
            var diagnostics = new List<Diagnostic>();
            var offer = round.Offer;
            var answer = round.Answer;

            var bundleMids = answer.Session.BundleMids ?? offer.Session.BundleMids;
            var bundleMaster = bundleMids?.FirstOrDefault();

            if (prevBundleMaster is not null && bundleMaster is not null
                && !string.Equals(prevBundleMaster, bundleMaster, StringComparison.Ordinal))
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Warning, "bundle-master-migration",
                    $"BUNDLE master 由 mid={prevBundleMaster} 迁移到 mid={bundleMaster}，传输归属随之切换。",
                    round.Index, null));
            }

            // 传输归属：bundle master 对应 section 的 ICE 凭据代表整组传输。
            var owner = FindByMid(answer, bundleMaster) ?? FindByMid(offer, bundleMaster);
            var creds = (owner?.IceUfrag, owner?.IcePwd);
            if (prevCreds != (null, null) && creds != (null, null)
                && (creds.Item1 != prevCreds.Ufrag || creds.Item2 != prevCreds.Pwd))
            {
                iceGeneration++;
                diagnostics.Add(new Diagnostic(
                    Severity.Warning, "ice-credential-change",
                    $"ICE ufrag/pwd 发生变化（restart），代次升为 {iceGeneration}。",
                    round.Index, null));
            }
            if (creds != (null, null)) prevCreds = creds;
            if (bundleMaster is not null) prevBundleMaster = bundleMaster;

            var media = new List<MediaNegotiation>();
            var rejectedNow = new HashSet<int>();

            for (var i = 0; i < offer.Media.Count; i++)
            {
                var o = offer.Media[i];
                var a = i < answer.Media.Count ? answer.Media[i] : null;
                var mid = a?.Mid ?? o.Mid;
                var rejected = a?.IsRejected ?? o.IsRejected;

                if (a is not null && a.IsRejected && !o.IsRejected)
                {
                    diagnostics.Add(new Diagnostic(
                        Severity.Info, "media-rejected",
                        $"m={o.Media}（mid={mid}，序号 {i}）被 answer 以端口 0 拒绝；列表位置保留。",
                        round.Index, i));
                }
                if (prevRejected.Contains(i) && !o.IsRejected)
                {
                    diagnostics.Add(new Diagnostic(
                        Severity.Info, "media-recycled",
                        $"此前被拒绝的 m={o.Media}（mid={mid}，序号 {i}）在本轮以端口 {o.Port} 回收复用。",
                        round.Index, i));
                }
                if (rejected) rejectedNow.Add(i);

                string? negotiated = null;
                if (!rejected && a is not null)
                    negotiated = NegotiateDirection(o.Direction, a.Direction);

                var intersection = a is null || rejected
                    ? (IReadOnlyList<string>)Array.Empty<string>()
                    : CodecIntersection(o, a);

                if (a is not null && !rejected)
                    CheckExtmapConflicts(o, a, round.Index, i, mid, diagnostics);

                media.Add(new MediaNegotiation
                {
                    MediaIndex = i,
                    Mid = mid,
                    Media = o.Media,
                    Rejected = rejected,
                    OfferDirection = o.Direction,
                    AnswerDirection = a?.Direction,
                    NegotiatedDirection = negotiated,
                    CodecIntersection = intersection,
                    OwnsBundleTransport = mid is not null && mid == bundleMaster,
                    IceGeneration = rejected ? null : iceGeneration,
                });
            }

            CheckDynamicPtCollisions(offer, round.Index, diagnostics);
            CheckDynamicPtCollisions(answer, round.Index, diagnostics);

            foreach (var trickle in round.Trickle.Where(t => t.ArrivedAfterAnswer))
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Warning, "late-trickle-candidate",
                    $"candidate（mid={trickle.Mid}，{trickle.Candidate.Address}:{trickle.Candidate.Port} "
                    + $"{trickle.Candidate.Type}）在 answer 完成之后晚到。",
                    round.Index, null));
            }

            graph.Rounds.Add(new RoundGraph
            {
                Index = round.Index,
                BundleMasterMid = bundleMaster,
                IceGeneration = iceGeneration,
                Media = media,
                Diagnostics = diagnostics,
            });
            prevRejected = rejectedNow;
        }

        return graph;
    }

    /// <summary>offerer 视角的协商后方向。</summary>
    public static string NegotiateDirection(string offer, string answer)
    {
        if (offer == "inactive" || answer == "inactive") return "inactive";
        var sends = offer is "sendrecv" or "sendonly" && answer is "sendrecv" or "recvonly";
        var receives = offer is "sendrecv" or "recvonly" && answer is "sendrecv" or "sendonly";
        return (sends, receives) switch
        {
            (true, true) => "sendrecv",
            (true, false) => "sendonly",
            (false, true) => "recvonly",
            _ => "inactive",
        };
    }

    /// <summary>按 codec 身份（rtpmap 归一化）求交集，PT 号不参与比较。</summary>
    public static IReadOnlyList<string> CodecIntersection(MediaSection offer, MediaSection answer)
    {
        var offerKeys = new HashSet<string>(
            offer.Formats.Select(offer.CodecKeyFor).Where(k => k is not null)!,
            StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var pt in answer.Formats)
        {
            var key = answer.CodecKeyFor(pt);
            if (key is not null && offerKeys.Contains(key) && !result.Contains(key))
                result.Add(key);
        }
        return result;
    }

    private static void CheckExtmapConflicts(
        MediaSection offer, MediaSection answer, int round, int mediaIndex, string? mid,
        List<Diagnostic> diagnostics)
    {
        var offerById = offer.ExtMaps
            .GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First().Uri, StringComparer.Ordinal);
        foreach (var ext in answer.ExtMaps)
        {
            if (offerById.TryGetValue(ext.Id, out var offerUri)
                && !string.Equals(offerUri, ext.Uri, StringComparison.Ordinal))
            {
                diagnostics.Add(new Diagnostic(
                    Severity.Error, "extmap-conflict",
                    $"mid={mid} 的 extmap id={ext.Id} 冲突：offer 为 {offerUri}，answer 为 {ext.Uri}。",
                    round, mediaIndex));
            }
        }
    }

    /// <summary>同一文档内，动态 PT 同号映射到不同 codec 时给出诊断（PT 仅在本 media 内有效）。</summary>
    private static void CheckDynamicPtCollisions(SdpDocument doc, int round, List<Diagnostic> diagnostics)
    {
        var seen = new Dictionary<string, (string Key, string Mid)>(StringComparer.Ordinal);
        foreach (var section in doc.Media)
        {
            foreach (var pt in section.Formats)
            {
                if (!StaticPayloadTable.IsDynamic(pt)) continue;
                var key = section.CodecKeyFor(pt);
                if (key is null) continue;
                var mid = section.Mid ?? "?";
                if (seen.TryGetValue(pt, out var prev) && prev.Key != key)
                {
                    diagnostics.Add(new Diagnostic(
                        Severity.Warning, "pt-codec-mismatch",
                        $"动态 PT {pt} 在 mid={prev.Mid} 中为 {prev.Key}，在 mid={mid} 中为 {key}；同号不代表同 codec。",
                        round, null));
                }
                else
                {
                    seen[pt] = (key, mid);
                }
            }
        }
    }

    private static MediaSection? FindByMid(SdpDocument doc, string? mid) =>
        mid is null ? null : doc.Media.FirstOrDefault(m => m.Mid == mid);
}
