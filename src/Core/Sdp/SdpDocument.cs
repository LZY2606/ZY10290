using System.Text;

namespace PairwiseGsb.Core.Sdp;

/// <summary>
/// 解析后的 SDP 文档。所有原始行按序保留（含换行方式），
/// 序列化时逐行回写，未理解的行绝不被改写。
/// </summary>
public sealed class SdpDocument
{
    private readonly List<SdpLine> _lines;

    private SdpDocument(string newLine, List<SdpLine> lines, SessionSection session, List<MediaSection> media)
    {
        NewLine = newLine;
        _lines = lines;
        Session = session;
        Media = media;
    }

    /// <summary>原文使用的换行符（\r\n 或 \n）。</summary>
    public string NewLine { get; }

    public SessionSection Session { get; }

    /// <summary>m-line 顺序即身份，拒绝（端口 0）的 section 也保留在原位置。</summary>
    public IReadOnlyList<MediaSection> Media { get; }

    public static SdpDocument Parse(string text)
    {
        if (text is null) throw new ArgumentNullException(nameof(text));
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var rawLines = text.Split(newLine, StringSplitOptions.None);
        // 末尾换行会产生空尾巴，丢弃。
        if (rawLines.Length > 0 && rawLines[^1].Length == 0)
            rawLines = rawLines[..^1];

        var lines = new List<SdpLine>(rawLines.Length);
        var sessionLines = new List<SdpLine>();
        var media = new List<MediaSection>();
        MediaSection? current = null;

        foreach (var raw in rawLines)
        {
            var line = SdpLine.Parse(raw);
            lines.Add(line);
            if (line.Kind == 'm')
            {
                current = MediaSection.From(line);
                media.Add(current);
            }
            else if (current is null)
            {
                sessionLines.Add(line);
            }
            else
            {
                current.Lines.Add(line);
            }
        }

        foreach (var section in media)
            section.Index();

        return new SdpDocument(newLine, lines, new SessionSection(sessionLines), media);
    }

    /// <summary>逐行回写，保证与原文逐字节一致。</summary>
    public string Serialize()
    {
        var sb = new StringBuilder();
        foreach (var line in _lines)
        {
            sb.Append(line.Raw);
            sb.Append(NewLine);
        }
        return sb.ToString();
    }
}
