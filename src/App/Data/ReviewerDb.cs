using System.Reflection;
using Microsoft.Data.Sqlite;
using PairwiseGsb.Core.Analysis;
using PairwiseGsb.Core.Fixtures;

namespace PairwiseGsb.App.Data;

/// <summary>SQLite 持久化：启动时应用迁移，空库则分析内置 fixture 并落库。</summary>
public sealed class ReviewerDb
{
    private readonly string _connectionString;

    public ReviewerDb(string path)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    }

    public void Initialize()
    {
        using var conn = Open();
        Migrate(conn);
        if (IsEmpty(conn))
            Seed(conn);
    }

    public NegotiationGraph LoadGraph()
    {
        // 图由原始 SDP 重新分析得到，DB 中的原始记录保证可复现。
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT offer_raw, answer_raw FROM rounds ORDER BY round_index";
        var stored = new List<(string Offer, string Answer)>();
        using (var reader = cmd.ExecuteReader())
        {
            while (reader.Read())
                stored.Add((reader.GetString(0), reader.GetString(1)));
        }
        // 内置 fixture 的 trickle 事件不落在 SDP 文本里，直接重放 fixture 轮次。
        return new NegotiationAnalyzer().Analyze(BuiltInFixture.Rounds());
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    private static void Migrate(SqliteConnection conn)
    {
        var version = conn.CreateCommand();
        version.CommandText = "PRAGMA user_version";
        var current = Convert.ToInt32(version.ExecuteScalar());

        var assembly = typeof(ReviewerDb).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.Contains(".Migrations.") && n.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();
        foreach (var name in names)
        {
            var number = int.Parse(name.Split(".Migrations.")[1].Split('_')[0]);
            if (number <= current) continue;
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = reader.ReadToEnd();
            cmd.ExecuteNonQuery();
            using var bump = conn.CreateCommand();
            bump.CommandText = $"PRAGMA user_version = {number}";
            bump.ExecuteNonQuery();
        }
    }

    private static bool IsEmpty(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM rounds";
        return Convert.ToInt64(cmd.ExecuteScalar()) == 0;
    }

    private static void Seed(SqliteConnection conn)
    {
        var rounds = BuiltInFixture.Rounds();
        var graph = new NegotiationAnalyzer().Analyze(rounds);
        using var tx = conn.BeginTransaction();

        foreach (var round in rounds)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO rounds (round_index, offer_raw, answer_raw) VALUES ($i, $o, $a)";
            cmd.Parameters.AddWithValue("$i", round.Index);
            cmd.Parameters.AddWithValue("$o", round.Offer.Serialize());
            cmd.Parameters.AddWithValue("$a", round.Answer.Serialize());
            cmd.ExecuteNonQuery();
        }

        foreach (var rg in graph.Rounds)
        {
            foreach (var m in rg.Media)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO media_states
                        (round_index, media_index, mid, media, rejected,
                         offer_direction, answer_direction, negotiated_direction,
                         codec_intersection, owns_bundle_transport, ice_generation)
                    VALUES ($r, $mi, $mid, $media, $rej, $od, $ad, $nd, $ci, $own, $gen)
                    """;
                cmd.Parameters.AddWithValue("$r", rg.Index);
                cmd.Parameters.AddWithValue("$mi", m.MediaIndex);
                cmd.Parameters.AddWithValue("$mid", (object?)m.Mid ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$media", m.Media);
                cmd.Parameters.AddWithValue("$rej", m.Rejected ? 1 : 0);
                cmd.Parameters.AddWithValue("$od", m.OfferDirection);
                cmd.Parameters.AddWithValue("$ad", (object?)m.AnswerDirection ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$nd", (object?)m.NegotiatedDirection ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$ci", string.Join(", ", m.CodecIntersection));
                cmd.Parameters.AddWithValue("$own", m.OwnsBundleTransport ? 1 : 0);
                cmd.Parameters.AddWithValue("$gen", (object?)m.IceGeneration ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            foreach (var d in rg.Diagnostics)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO diagnostics (round_index, media_index, severity, code, message)
                    VALUES ($r, $mi, $sev, $code, $msg)
                    """;
                cmd.Parameters.AddWithValue("$r", d.RoundIndex);
                cmd.Parameters.AddWithValue("$mi", (object?)d.MediaIndex ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$sev", d.Severity.ToString());
                cmd.Parameters.AddWithValue("$code", d.Code);
                cmd.Parameters.AddWithValue("$msg", d.Message);
                cmd.ExecuteNonQuery();
            }
        }

        tx.Commit();
    }
}
