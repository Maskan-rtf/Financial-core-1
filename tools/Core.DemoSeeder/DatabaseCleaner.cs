using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Core.DemoSeeder;

internal static class DatabaseCleaner
{
    private static readonly (string Schema, string Table)[] PreferredOrder =
    [
        ("Analytics", "dashboard_stats_snapshots"),
        ("Process", "process_instances"),
        ("Fund", "fund_credit_limits"),
        ("Investment", "case_comment_attachments"),
        ("Investment", "case_comments"),
        ("Investment", "case_documents"),
        ("Investment", "case_evaluation_items"),
        ("Investment", "case_evaluations"),
        ("Investment", "case_revisions"),
        ("Investment", "case_valuations"),
        ("Investment", "case_workflow_history"),
        ("Investment", "financial_worksheets"),
        ("Investment", "payment_records"),
        ("Investment", "case_data_entry_1"),
        ("Investment", "case_data_entry_2"),
        ("Investment", "investment_cases"),
        ("Guarantee", "guarantee_amendment_history_records"),
        ("Guarantee", "guarantee_case_comments"),
        ("Guarantee", "guarantee_case_documents"),
        ("Guarantee", "guarantee_case_workflow_history"),
        ("Guarantee", "guarantee_approval_forms"),
        ("Guarantee", "guarantee_case_applications"),
        ("Guarantee", "guarantee_renewal_cases"),
        ("Guarantee", "guarantee_applicant_credit_profiles"),
        ("Guarantee", "guarantee_cases"),
        ("Loan", "loan_payments"),
        ("Loan", "loan_installments"),
        ("Loan", "loan_disbursement_plan_items"),
        ("Loan", "loan_case_comments"),
        ("Loan", "loan_case_documents"),
        ("Loan", "loan_case_workflow_history"),
        ("Loan", "loan_approval_details"),
        ("Loan", "loan_case_applications"),
        ("Loan", "loan_cases"),
        ("Identity", "RefreshTokens"),
        ("Identity", "UserSessions"),
        ("Identity", "Company"),
        ("Identity", "User")
    ];

    /// <summary>
    /// Clears application data only. Schema / migrations / Elsa tables are left untouched.
    /// Truncates only tables that actually exist on the target database.
    /// </summary>
    public static async Task ClearAsync(DemoDbContext db, CancellationToken ct)
    {
        var connectionString = db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Missing connection string.");

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        var existing = new HashSet<(string Schema, string Table)>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_type = 'BASE TABLE'
              AND table_schema IN ('Identity','Investment','Guarantee','Loan','Analytics','Fund','Process');
            """, conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                existing.Add((reader.GetString(0), reader.GetString(1)));
        }

        var toTruncate = PreferredOrder
            .Where(t => existing.Contains(t))
            .Select(t => $"\"{t.Schema}\".\"{t.Table}\"")
            .ToList();

        if (toTruncate.Count == 0)
            return;

        var sql = $"TRUNCATE TABLE {string.Join(", ", toTruncate)} RESTART IDENTITY CASCADE;";
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
