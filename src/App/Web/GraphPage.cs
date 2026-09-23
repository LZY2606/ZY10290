using System.Net;
using System.Text;
using PairwiseGsb.Core.Analysis;

namespace PairwiseGsb.App.Web;

/// <summary>把协商图渲染成自包含 HTML 页面。</summary>
public static class GraphPage
{
    public static string Render(NegotiationGraph graph)
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "—");

        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html>
            <html lang="zh-CN">
            <head>
            <meta charset="utf-8">
            <title>协商岔路图</title>
            <style>
            body{font-family:system-ui,sans-serif;margin:2rem;background:#0f1420;color:#e6e9f0}
            h1{color:#8fd0ff}
            .round{border:1px solid #2a3550;border-radius:8px;margin:1.5rem 0;padding:1rem;background:#151b2c}
            .round h2{margin-top:0}
            table{border-collapse:collapse;width:100%;margin:.5rem 0}
            th,td{border:1px solid #2a3550;padding:.35rem .6rem;text-align:left;font-size:.9rem}
            th{background:#1d2740}
            .badge{display:inline-block;padding:.1rem .5rem;border-radius:10px;font-size:.78rem;margin-right:.3rem}
            .owner{background:#134e2f;color:#9ff0c0}
            .rejected{background:#5a1f1f;color:#ffb3b3}
            .diag{margin:.2rem 0;padding:.3rem .6rem;border-radius:6px;font-size:.88rem}
            .Info{background:#1d2b45}.Warning{background:#4a3a14}.Error{background:#4d1d1d}
            code{color:#ffd479}
            </style>
            </head>
            <body>
            <h1>协商岔路图</h1>
            <p>按 offer/answer 轮次展示 codec 交集、方向变化、BUNDLE 传输归属与 ICE 代次。</p>
            """);

        foreach (var round in graph.Rounds)
        {
            sb.Append($"""
                <section class="round">
                <h2>第 {round.Index} 轮 <small>bundle master: <code>{E(round.BundleMasterMid)}</code> · ICE generation: <code>{round.IceGeneration}</code></small></h2>
                <table>
                <tr><th>#</th><th>mid</th><th>媒体</th><th>状态</th><th>codec 交集</th>
                <th>方向(offer→answer→协商)</th><th>传输归属</th><th>ICE 代次</th></tr>
                """);
            foreach (var m in round.Media)
            {
                var state = m.Rejected
                    ? "<span class=\"badge rejected\">已拒绝(端口0)</span>"
                    : "<span class=\"badge\" style=\"background:#1d3a5f;color:#9fd0ff\">协商中</span>";
                var owner = m.OwnsBundleTransport
                    ? "<span class=\"badge owner\">bundle 传输归属</span>" : "";
                sb.Append($"""
                    <tr>
                    <td>{m.MediaIndex}</td><td><code>{E(m.Mid)}</code></td><td>{E(m.Media)}</td>
                    <td>{state}</td>
                    <td>{E(string.Join(", ", m.CodecIntersection))}</td>
                    <td><code>{E(m.OfferDirection)} → {E(m.AnswerDirection)} → {E(m.NegotiatedDirection)}</code></td>
                    <td>{owner}</td>
                    <td>{(m.IceGeneration?.ToString() ?? "—")}</td>
                    </tr>
                    """);
            }
            sb.Append("</table>");
            if (round.Diagnostics.Count > 0)
            {
                sb.Append("<div><strong>诊断</strong>");
                foreach (var d in round.Diagnostics)
                    sb.Append($"<div class=\"diag {d.Severity}\">[{d.Severity}] <code>{E(d.Code)}</code> {E(d.Message)}</div>");
                sb.Append("</div>");
            }
            sb.Append("</section>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }
}
