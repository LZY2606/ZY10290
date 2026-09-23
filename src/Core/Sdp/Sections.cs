namespace PairwiseGsb.Core.Sdp;

/// <summary>会话级（首个 m= 之前）的行集合。</summary>
public sealed class SessionSection
{
    internal SessionSection(List<SdpLine> lines) => Lines = lines;

    public IReadOnlyList<SdpLine> Lines { get; }

    /// <summary>a=group:BUNDLE 的 mid 列表；未声明为 null。</summary>
    public IReadOnlyList<string>? BundleMids
    {
        get
        {
            foreach (var line in Lines)
            {
                if (line.Kind == 'a' && line.AttributeName == "group"
                    && line.AttributeValue.StartsWith("BUNDLE", StringComparison.Ordinal))
                {
                    return line.AttributeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Skip(1).ToArray();
                }
            }
            return null;
        }
    }

    public string? GetAttribute(string name) =>
        Lines.FirstOrDefault(l => l.Kind == 'a' && l.AttributeName == name)?.AttributeValue;
}

/// <summary>一个 media section：m= 行 + 其后全部属性行（原序保留）。</summary>
public sealed class MediaSection
{
    private readonly List<RtpMap> _rtpMaps = new();
    private readonly List<Fmtp> _fmtps = new();
    private readonly List<RtcpFb> _rtcpFbs = new();
    private readonly List<ExtMap> _extMaps = new();
    private readonly List<Candidate> _candidates = new();

    private MediaSection(SdpLine mLine)
    {
        MLine = mLine;
        var parts = mLine.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Media = parts.ElementAtOrDefault(0) ?? string.Empty;
        Port = int.TryParse(parts.ElementAtOrDefault(1)?.Split('/')[0], out var p) ? p : 0;
        Proto = parts.ElementAtOrDefault(2) ?? string.Empty;
        Formats = parts.Skip(3).ToArray();
    }

    internal static MediaSection From(SdpLine mLine) => new(mLine);

    public SdpLine MLine { get; }
    public List<SdpLine> Lines { get; } = new();

    public string Media { get; }
    public int Port { get; }
    public string Proto { get; }

    /// <summary>m= 行的 format 列表（RTP 为 PT 字符串）。</summary>
    public IReadOnlyList<string> Formats { get; }

    /// <summary>端口为 0 表示被拒绝；被拒绝的 section 不从列表中移除。</summary>
    public bool IsRejected => Port == 0;

    public string? Mid => GetAttr("mid");
    public string? IceUfrag => GetAttr("ice-ufrag");
    public string? IcePwd => GetAttr("ice-pwd");
    public string? Fingerprint => GetAttr("fingerprint");
    public string? Msid => GetAttr("msid");

    /// <summary>方向属性；缺省按 RFC 4566 视为 sendrecv。</summary>
    public string Direction
    {
        get
        {
            foreach (var line in Lines)
            {
                if (line.Kind != 'a') continue;
                if (line.Value is "sendrecv" or "sendonly" or "recvonly" or "inactive")
                    return line.Value;
            }
            return "sendrecv";
        }
    }

    public IReadOnlyList<RtpMap> RtpMaps => _rtpMaps;
    public IReadOnlyList<Fmtp> Fmtps => _fmtps;
    public IReadOnlyList<RtcpFb> RtcpFbs => _rtcpFbs;
    public IReadOnlyList<ExtMap> ExtMaps => _extMaps;
    public IReadOnlyList<Candidate> Candidates => _candidates;

    /// <summary>未知（未建模）属性，按原顺序返回。</summary>
    public IEnumerable<SdpLine> UnknownAttributes()
    {
        foreach (var line in Lines)
        {
            if (line.Kind != 'a') { yield return line; continue; }
            if (!KnownAttributes.Names.Contains(line.AttributeName))
                yield return line;
        }
    }

    internal void Index()
    {
        foreach (var line in Lines)
        {
            if (line.Kind != 'a') continue;
            switch (line.AttributeName)
            {
                case "rtpmap":
                    if (RtpMap.TryParse(line.AttributeValue, out var rm)) _rtpMaps.Add(rm);
                    break;
                case "fmtp":
                    if (Fmtp.TryParse(line.AttributeValue, out var fp)) _fmtps.Add(fp);
                    break;
                case "rtcp-fb":
                    if (RtcpFb.TryParse(line.AttributeValue, out var fb)) _rtcpFbs.Add(fb);
                    break;
                case "extmap":
                    if (ExtMap.TryParse(line.AttributeValue, out var em)) _extMaps.Add(em);
                    break;
                case "candidate":
                    if (Candidate.TryParse(line.AttributeValue, out var c)) _candidates.Add(c);
                    break;
            }
        }
    }

    /// <summary>PT 仅在本 media 内解释：返回本 section 内某 PT 的 codec 标识。</summary>
    public string? CodecKeyFor(string pt)
    {
        var rm = _rtpMaps.FirstOrDefault(r => r.PayloadType == pt);
        if (rm is not null) return rm.CodecKey;
        return StaticPayloadTable.TryGet(pt);
    }

    private string? GetAttr(string name) =>
        Lines.FirstOrDefault(l => l.Kind == 'a' && l.AttributeName == name)?.AttributeValue;
}

internal static class KnownAttributes
{
    public static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "mid", "msid", "rtpmap", "fmtp", "rtcp-fb", "extmap",
        "ice-ufrag", "ice-pwd", "ice-options", "fingerprint", "candidate",
        "setup", "rtcp-mux", "rtcp", "group", "sctp-port", "max-message-size",
        "sendrecv", "sendonly", "recvonly", "inactive", "ssrc", "rid", "simulcast",
        "rtcp-rsize", "ptime", "maxptime", "bundle-only", "connection", "crypto",
        "ice-lite", "msid-semantic", "ssrc-group", "framerate", "orientation",
    };
}
