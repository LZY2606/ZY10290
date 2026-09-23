namespace PairwiseGsb.Core.Sdp;

/// <summary>一条 SDP 行。Raw 永远保留原文，Known 属性额外提供结构化视图。</summary>
public sealed class SdpLine
{
    private SdpLine(string raw, char kind, string value)
    {
        Raw = raw;
        Kind = kind;
        Value = value;
    }

    public string Raw { get; }
    public char Kind { get; }
    public string Value { get; }

    /// <summary>a= 行的属性名（不含值）；非 a= 行为空串。</summary>
    public string AttributeName
    {
        get
        {
            if (Kind != 'a') return string.Empty;
            var idx = Value.IndexOf(':', StringComparison.Ordinal);
            return idx < 0 ? Value : Value[..idx];
        }
    }

    /// <summary>a= 行冒号后的值；无冒号则为空串。</summary>
    public string AttributeValue
    {
        get
        {
            if (Kind != 'a') return string.Empty;
            var idx = Value.IndexOf(':', StringComparison.Ordinal);
            return idx < 0 ? string.Empty : Value[(idx + 1)..];
        }
    }

    public static SdpLine Parse(string raw)
    {
        if (raw.Length >= 2 && raw[1] == '=')
            return new SdpLine(raw, raw[0], raw[2..]);
        // 畸形行也原样保留。
        return new SdpLine(raw, '\0', raw);
    }
}
