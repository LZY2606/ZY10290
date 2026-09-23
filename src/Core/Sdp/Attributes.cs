namespace PairwiseGsb.Core.Sdp;

/// <summary>a=rtpmap:&lt;pt&gt; &lt;name&gt;/&lt;clock&gt;[/&lt;channels&gt;]</summary>
public sealed record RtpMap(string PayloadType, string Name, int Clock, int Channels)
{
    /// <summary>codec 身份标识（大小写不敏感的名字 + 时钟 + 声道）。</summary>
    public string CodecKey => $"{Name.ToUpperInvariant()}/{Clock}/{Channels}";

    public static bool TryParse(string value, out RtpMap rtpMap)
    {
        rtpMap = null!;
        var sp = value.IndexOf(' ', StringComparison.Ordinal);
        if (sp <= 0) return false;
        var pt = value[..sp];
        var rest = value[(sp + 1)..].Split('/');
        if (rest.Length < 2) return false;
        if (!int.TryParse(rest[1], out var clock)) return false;
        var channels = rest.Length > 2 && int.TryParse(rest[2], out var ch) ? ch : 1;
        rtpMap = new RtpMap(pt, rest[0], clock, channels);
        return true;
    }
}

/// <summary>a=fmtp:&lt;pt&gt; &lt;params&gt;</summary>
public sealed record Fmtp(string PayloadType, string Parameters)
{
    public static bool TryParse(string value, out Fmtp fmtp)
    {
        fmtp = null!;
        var sp = value.IndexOf(' ', StringComparison.Ordinal);
        if (sp <= 0) return false;
        fmtp = new Fmtp(value[..sp], value[(sp + 1)..]);
        return true;
    }
}

/// <summary>a=rtcp-fb:&lt;pt|*&gt; &lt;type&gt; [subtype]</summary>
public sealed record RtcpFb(string PayloadType, string Type, string? SubType)
{
    public static bool TryParse(string value, out RtcpFb fb)
    {
        fb = null!;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        fb = new RtcpFb(parts[0], parts[1], parts.Length > 2 ? string.Join(' ', parts.Skip(2)) : null);
        return true;
    }
}

/// <summary>a=extmap:&lt;id&gt;[/direction] &lt;uri&gt; [ext-attributes]</summary>
public sealed record ExtMap(string Id, string Uri, string? Direction)
{
    public static bool TryParse(string value, out ExtMap extMap)
    {
        extMap = null!;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;
        var idPart = parts[0];
        string? direction = null;
        var slash = idPart.IndexOf('/', StringComparison.Ordinal);
        if (slash >= 0)
        {
            direction = idPart[(slash + 1)..];
            idPart = idPart[..slash];
        }
        extMap = new ExtMap(idPart, parts[1], direction);
        return true;
    }
}

/// <summary>a=candidate:foundation component transport priority ip port typ type ...</summary>
public sealed record Candidate(
    string Foundation, int Component, string Transport, long Priority,
    string Address, int Port, string Type)
{
    public static bool TryParse(string value, out Candidate candidate)
    {
        candidate = null!;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 8) return false;
        if (!int.TryParse(parts[1], out var comp)) return false;
        if (!long.TryParse(parts[3], out var prio)) return false;
        if (!int.TryParse(parts[5], out var port)) return false;
        if (parts[6] != "typ") return false;
        candidate = new Candidate(parts[0], comp, parts[2], prio, parts[4], port, parts[7]);
        return true;
    }
}

/// <summary>静态 PT（无 rtpmap 时）的知名 codec 表，仅覆盖常见值。</summary>
internal static class StaticPayloadTable
{
    private static readonly Dictionary<string, string> Table = new(StringComparer.Ordinal)
    {
        ["0"] = "PCMU/8000/1",
        ["3"] = "GSM/8000/1",
        ["4"] = "G723/8000/1",
        ["8"] = "PCMA/8000/1",
        ["9"] = "G722/8000/1",
        ["18"] = "G729/8000/1",
    };

    public static string? TryGet(string pt) => Table.TryGetValue(pt, out var key) ? key : null;

    public static bool IsDynamic(string pt) =>
        int.TryParse(pt, out var n) && n >= 96 && n <= 127;
}
