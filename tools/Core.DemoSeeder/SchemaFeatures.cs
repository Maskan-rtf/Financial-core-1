using Npgsql;

namespace Core.DemoSeeder;

internal sealed class SchemaFeatures
{
    public bool UserHasCompanyId { get; private init; }
    public bool HasProcessInstances { get; private init; }
    public bool HasFundCreditLimits { get; private init; }
    public bool HasGuaranteeApplicantCreditProfiles { get; private init; }
    public bool HasLoanDisbursementPlanItems { get; private init; }

    public static async Task<SchemaFeatures> DetectAsync(string connectionString, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        return new SchemaFeatures
        {
            UserHasCompanyId = await ColumnExistsAsync(conn, "Identity", "User", "CompanyId", ct),
            HasProcessInstances = await TableExistsAsync(conn, "Process", "process_instances", ct),
            HasFundCreditLimits = await TableExistsAsync(conn, "Fund", "fund_credit_limits", ct),
            HasGuaranteeApplicantCreditProfiles = await TableExistsAsync(conn, "Guarantee", "guarantee_applicant_credit_profiles", ct),
            HasLoanDisbursementPlanItems = await TableExistsAsync(conn, "Loan", "loan_disbursement_plan_items", ct)
        };
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection conn,
        string schema,
        string table,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT EXISTS (
              SELECT 1 FROM information_schema.tables
              WHERE table_schema = @schema AND table_name = @table
            );
            """, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }

    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection conn,
        string schema,
        string table,
        string column,
        CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            SELECT EXISTS (
              SELECT 1 FROM information_schema.columns
              WHERE table_schema = @schema AND table_name = @table AND column_name = @column
            );
            """, conn);
        cmd.Parameters.AddWithValue("schema", schema);
        cmd.Parameters.AddWithValue("table", table);
        cmd.Parameters.AddWithValue("column", column);
        return (bool)(await cmd.ExecuteScalarAsync(ct) ?? false);
    }
}
