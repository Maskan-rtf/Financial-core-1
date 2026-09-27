using System.Text.Json;
using Core.Domain.Entities.Analytics;
using Core.Domain.Enums;

namespace Core.DemoSeeder.Seed;

internal static class DashboardSeeder
{
    public static void Seed(DemoDbContext db)
    {
        var payload = new
        {
            generatedFor = "demo",
            investment = new { total = 6, active = 4, completed = 1, rejected = 0 },
            guarantee = new { total = 6, active = 4, completed = 1, rejected = 1 },
            loan = new { total = 6, active = 4, completed = 1, revision = 1 },
            fund = new
            {
                guaranteeLimit = 500_000_000_000m,
                loanLimit = 350_000_000_000m,
                currency = "IRR"
            },
            note = "اسنپ‌شات دمو — در محیط واقعی توسط جاب تجمیع داشبورد بازنویسی می‌شود."
        };

        db.DashboardStatsSnapshots.Add(new DashboardStatsSnapshot
        {
            Id = DemoIds.Snapshot("executive-global"),
            SnapshotKey = "executive:global",
            SnapshotType = DashboardSnapshotType.Executive,
            PayloadJson = JsonSerializer.Serialize(payload),
            ComputedAtUtc = DateTimeOffset.UtcNow
        });
    }
}
