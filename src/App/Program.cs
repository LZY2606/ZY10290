using PairwiseGsb.App.Data;
using PairwiseGsb.App.Web;

var builder = WebApplication.CreateBuilder(args);
var dbPath = builder.Configuration["REVIEWER_DB"]
    ?? Path.Combine(AppContext.BaseDirectory, "reviewer.db");
builder.Services.AddSingleton(new ReviewerDb(dbPath));
var app = builder.Build();

var db = app.Services.GetRequiredService<ReviewerDb>();
db.Initialize();

app.MapGet("/", (ReviewerDb store) =>
    Results.Content(GraphPage.Render(store.LoadGraph()), "text/html; charset=utf-8"));

app.MapGet("/api/graph", (ReviewerDb store) =>
{
    var graph = store.LoadGraph();
    return Results.Json(new
    {
        rounds = graph.Rounds.Select(r => new
        {
            r.Index,
            r.BundleMasterMid,
            r.IceGeneration,
            media = r.Media.Select(m => new
            {
                m.MediaIndex, m.Mid, m.Media, m.Rejected,
                m.OfferDirection, m.AnswerDirection, m.NegotiatedDirection,
                m.CodecIntersection, m.OwnsBundleTransport, m.IceGeneration,
            }),
            diagnostics = r.Diagnostics,
        }),
    });
});

app.Run();
