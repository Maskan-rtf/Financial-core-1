using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Core.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Investment");

            migrationBuilder.EnsureSchema(
                name: "Identity");

            migrationBuilder.EnsureSchema(
                name: "Analytics");

            migrationBuilder.EnsureSchema(
                name: "Fund");

            migrationBuilder.EnsureSchema(
                name: "Guarantee");

            migrationBuilder.EnsureSchema(
                name: "Loan");

            migrationBuilder.EnsureSchema(
                name: "Process");

            migrationBuilder.CreateTable(
                name: "dashboard_stats_snapshots",
                schema: "Analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SnapshotType = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ComputedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_stats_snapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "fund_credit_limits",
                schema: "Fund",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleType = table.Column<int>(type: "integer", nullable: false),
                    CreditLimitWithCheck = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodStart = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresAt = table.Column<DateOnly>(type: "date", nullable: false),
                    LastSetByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fund_credit_limits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_applicant_credit_profiles",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreditLimitWithCheck = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LastSetByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_applicant_credit_profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "process_instances",
                schema: "Process",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowInstanceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkflowDefinitionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    WorkflowDefinitionVersion = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentProcessStep = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LastCommandCorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_instances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "Identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedByIp = table.Column<string>(type: "text", nullable: true),
                    CreatedByUserAgent = table.Column<string>(type: "text", nullable: true),
                    DeviceId = table.Column<string>(type: "text", nullable: true),
                    RevokedByIp = table.Column<string>(type: "text", nullable: true),
                    RevocationReason = table.Column<string>(type: "text", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                schema: "Identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentRefreshTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeviceId = table.Column<string>(type: "text", nullable: true),
                    UserAgent = table.Column<string>(type: "text", nullable: true),
                    IpAddress = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_comment_attachments",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: false),
                    S3Key = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    InvestmentCaseCommentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_comment_attachments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_comments",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    IsRevisionRequest = table.Column<bool>(type: "boolean", nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    WorkflowStatusAtCreation = table.Column<int>(type: "integer", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_case_comments_case_comments_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "Investment",
                        principalTable: "case_comments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "case_data_entry_1",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RepresentativeFullName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    BusinessStage = table.Column<int>(type: "integer", nullable: false),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_data_entry_1", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_data_entry_2",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvestmentAttractionBasis = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_data_entry_2", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_documents",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_evaluation_items",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EvaluationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsApproved = table.Column<bool>(type: "boolean", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_evaluation_items", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_evaluations",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    ReviewerUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ReviewerRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_evaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_revisions",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReviewResult = table.Column<int>(type: "integer", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_revisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_valuations",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_valuations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "case_workflow_history",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPhase = table.Column<int>(type: "integer", nullable: false),
                    ToPhase = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_case_workflow_history", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Company",
                schema: "Identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EconomicCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    NationalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    PhoneNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Address = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    City = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Province = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PostalCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Company", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_cases",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApplicantUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApplicantType = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentPhase = table.Column<int>(type: "integer", nullable: false),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    WorkflowInstanceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AmendmentType = table.Column<int>(type: "integer", nullable: true),
                    AmendmentReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AmendmentRequiresCreditReview = table.Column<bool>(type: "boolean", nullable: false),
                    AmendmentOriginalGuaranteeReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LegalOverrideApproved = table.Column<bool>(type: "boolean", nullable: false),
                    SettlementConfirmationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AmendmentRequestedValidityTo = table.Column<DateOnly>(type: "date", nullable: true),
                    AmendmentRequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmendmentApprovedValidityTo = table.Column<DateOnly>(type: "date", nullable: true),
                    AmendmentApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AmendmentCreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AmendmentCompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_cases_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "Identity",
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "investment_cases",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApplicantUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApplicantType = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentPhase = table.Column<int>(type: "integer", nullable: false),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    WorkflowInstanceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investment_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investment_cases_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "Identity",
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "loan_cases",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ApplicantUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApplicantType = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentPhase = table.Column<int>(type: "integer", nullable: false),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    WorkflowInstanceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_cases_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "Identity",
                        principalTable: "Company",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "User",
                schema: "Identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhoneNumber = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NationalCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsPhoneVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Company_CompanyId",
                        column: x => x.CompanyId,
                        principalSchema: "Identity",
                        principalTable: "Company",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "guarantee_amendment_history_records",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuaranteeCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmendmentType = table.Column<int>(type: "integer", nullable: false),
                    PreviousValues = table.Column<string>(type: "jsonb", nullable: false),
                    NewValues = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    ApprovalUser = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_amendment_history_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_amendment_history_records_guarantee_cases_Guarant~",
                        column: x => x.GuaranteeCaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_approval_forms",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditLimitWithCheck = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FundIssuedGuaranteesTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ActiveCommitments = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RemainingCredit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    GuaranteeType = table.Column<int>(type: "integer", nullable: true),
                    GuaranteeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    GuaranteeAmountInWords = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ContractSubject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Beneficiary = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IssuanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ActiveDurationDays = table.Column<int>(type: "integer", nullable: true),
                    DepositRatePercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    DepositAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    AnnualCommissionRatePercent = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: true),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CollateralDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GuarantorsDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OtherNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_approval_forms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_approval_forms_guarantee_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_case_applications",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuaranteeType = table.Column<int>(type: "integer", nullable: true),
                    ContractSubject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IsKnowledgeBasedProduct = table.Column<bool>(type: "boolean", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    BeneficiaryNationalId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    BeneficiaryCompanyType = table.Column<int>(type: "integer", nullable: true),
                    ApplicantCategory = table.Column<int>(type: "integer", nullable: false),
                    ApplicantCategoryOther = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ApplicantLegalForm = table.Column<int>(type: "integer", nullable: true),
                    BaseContractNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    BaseContractAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    BaseContractAmountInWords = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    PriceAdjustmentRatePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    ExecutionProvince = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedGuaranteeAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    InitialValidityDays = table.Column<int>(type: "integer", nullable: true),
                    ValidityFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidityTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CollateralDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    FacilitySubject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_case_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_case_applications_guarantee_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_case_comments",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsRevisionRequest = table.Column<bool>(type: "boolean", nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    WorkflowStatusAtCreation = table.Column<int>(type: "integer", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_case_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_case_comments_guarantee_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_case_documents",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_case_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_case_documents_guarantee_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_case_workflow_history",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPhase = table.Column<int>(type: "integer", nullable: false),
                    ToPhase = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_case_workflow_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_case_workflow_history_guarantee_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guarantee_renewal_cases",
                schema: "Guarantee",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseNumber = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApplicantUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ParentGuaranteeCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RenewalKind = table.Column<int>(type: "integer", nullable: false),
                    CurrentStatus = table.Column<int>(type: "integer", nullable: false),
                    WorkflowInstanceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guarantee_renewal_cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guarantee_renewal_cases_guarantee_cases_ParentGuaranteeCase~",
                        column: x => x.ParentGuaranteeCaseId,
                        principalSchema: "Guarantee",
                        principalTable: "guarantee_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "financial_worksheets",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Iban = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentSchedule = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_financial_worksheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_financial_worksheets_investment_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Investment",
                        principalTable: "investment_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_records",
                schema: "Investment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReceiptS3Key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    Notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_payment_records_investment_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Investment",
                        principalTable: "investment_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_approval_details",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DebtToAssetRatio = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    CurrentRatio = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    ProfitabilityRatioPercent = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    CreditLimitWithCheck = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    IsCreditLineActive = table.Column<bool>(type: "boolean", nullable: true),
                    RemainingCreditAfterGrant = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    FacilityType = table.Column<int>(type: "integer", nullable: true),
                    ContractSubject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BrokerageAndRelatedContract = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ApprovedAmountInWords = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    RepaymentMonths = table.Column<int>(type: "integer", nullable: true),
                    GracePeriodMonths = table.Column<int>(type: "integer", nullable: true),
                    AnnualProfitRatePercent = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    DailyPenaltyRatePercent = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    CollateralDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    GuarantorsDescription = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    OtherNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ExpectedTotalProfit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RepaymentCheckAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_approval_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_approval_details_loan_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Loan",
                        principalTable: "loan_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_case_applications",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    RequestedAmountInWords = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FacilitySubject = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OfferedGuarantees = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    ApplicantCategory = table.Column<int>(type: "integer", nullable: false),
                    ApplicantCategoryOther = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    RepresentativePosition = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_case_applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_case_applications_loan_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Loan",
                        principalTable: "loan_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_case_comments",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    IsRevisionRequest = table.Column<bool>(type: "boolean", nullable: false),
                    IsInternal = table.Column<bool>(type: "boolean", nullable: false),
                    WorkflowStatusAtCreation = table.Column<int>(type: "integer", nullable: true),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_case_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_case_comments_loan_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Loan",
                        principalTable: "loan_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_case_documents",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    S3Key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    MimeType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_case_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_case_documents_loan_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Loan",
                        principalTable: "loan_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_case_workflow_history",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPhase = table.Column<int>(type: "integer", nullable: false),
                    ToPhase = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: false),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    ChangedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_case_workflow_history", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_case_workflow_history_loan_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Loan",
                        principalTable: "loan_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_installments",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    InstallmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PrincipalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ProfitAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FundShareOfPrincipal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FundShareOfProfit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    FundShareOfTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsGracePeriod = table.Column<bool>(type: "boolean", nullable: false),
                    IsPaid = table.Column<bool>(type: "boolean", nullable: false),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReminderSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_installments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_installments_loan_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Loan",
                        principalTable: "loan_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "loan_payments",
                schema: "Loan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    TransactionNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReceiptS3Key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    StageNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loan_payments_loan_cases_CaseId",
                        column: x => x.CaseId,
                        principalSchema: "Loan",
                        principalTable: "loan_cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_case_comment_attachments_CommentId",
                schema: "Investment",
                table: "case_comment_attachments",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_case_comment_attachments_InvestmentCaseCommentId",
                schema: "Investment",
                table: "case_comment_attachments",
                column: "InvestmentCaseCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_case_comments_CaseId_Phase_CreatedAt",
                schema: "Investment",
                table: "case_comments",
                columns: new[] { "CaseId", "Phase", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_case_comments_ParentId",
                schema: "Investment",
                table: "case_comments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_case_data_entry_1_BusinessStage",
                schema: "Investment",
                table: "case_data_entry_1",
                column: "BusinessStage");

            migrationBuilder.CreateIndex(
                name: "IX_case_data_entry_1_CaseId",
                schema: "Investment",
                table: "case_data_entry_1",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_data_entry_1_ContactEmail",
                schema: "Investment",
                table: "case_data_entry_1",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_case_data_entry_1_RequestedAmount",
                schema: "Investment",
                table: "case_data_entry_1",
                column: "RequestedAmount");

            migrationBuilder.CreateIndex(
                name: "IX_case_data_entry_2_CaseId",
                schema: "Investment",
                table: "case_data_entry_2",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_documents_CaseId_DocumentType_Version",
                schema: "Investment",
                table: "case_documents",
                columns: new[] { "CaseId", "DocumentType", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_case_documents_S3Key",
                schema: "Investment",
                table: "case_documents",
                column: "S3Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_evaluation_items_EvaluationId_Title",
                schema: "Investment",
                table: "case_evaluation_items",
                columns: new[] { "EvaluationId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_evaluations_CaseId_Phase_ReviewerUserId",
                schema: "Investment",
                table: "case_evaluations",
                columns: new[] { "CaseId", "Phase", "ReviewerUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_case_revisions_CaseId_Phase_RevisionNumber",
                schema: "Investment",
                table: "case_revisions",
                columns: new[] { "CaseId", "Phase", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_case_revisions_CaseId_SubmittedAt",
                schema: "Investment",
                table: "case_revisions",
                columns: new[] { "CaseId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_case_valuations_CaseId_Type_CreatedAt",
                schema: "Investment",
                table: "case_valuations",
                columns: new[] { "CaseId", "Type", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_case_workflow_history_CaseId_CreatedAt",
                schema: "Investment",
                table: "case_workflow_history",
                columns: new[] { "CaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Company_Name",
                schema: "Identity",
                table: "Company",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Company_OwnerUserId",
                schema: "Identity",
                table: "Company",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Company_RegistrationNumber",
                schema: "Identity",
                table: "Company",
                column: "RegistrationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_stats_snapshots_ComputedAtUtc",
                schema: "Analytics",
                table: "dashboard_stats_snapshots",
                column: "ComputedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_stats_snapshots_SnapshotKey",
                schema: "Analytics",
                table: "dashboard_stats_snapshots",
                column: "SnapshotKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_dashboard_stats_snapshots_SnapshotType",
                schema: "Analytics",
                table: "dashboard_stats_snapshots",
                column: "SnapshotType");

            migrationBuilder.CreateIndex(
                name: "IX_financial_worksheets_ApprovedAmount",
                schema: "Investment",
                table: "financial_worksheets",
                column: "ApprovedAmount");

            migrationBuilder.CreateIndex(
                name: "IX_financial_worksheets_CaseId",
                schema: "Investment",
                table: "financial_worksheets",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fund_credit_limits_ModuleType_PeriodStart_ExpiresAt",
                schema: "Fund",
                table: "fund_credit_limits",
                columns: new[] { "ModuleType", "PeriodStart", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_amendment_history_records_GuaranteeCaseId_Created~",
                schema: "Guarantee",
                table: "guarantee_amendment_history_records",
                columns: new[] { "GuaranteeCaseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_amendment_history_records_GuaranteeCaseId_Status",
                schema: "Guarantee",
                table: "guarantee_amendment_history_records",
                columns: new[] { "GuaranteeCaseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_applicant_credit_profiles_ApplicantUserId",
                schema: "Guarantee",
                table: "guarantee_applicant_credit_profiles",
                column: "ApplicantUserId",
                unique: true,
                filter: "\"CompanyId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_applicant_credit_profiles_CompanyId",
                schema: "Guarantee",
                table: "guarantee_applicant_credit_profiles",
                column: "CompanyId",
                unique: true,
                filter: "\"CompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_approval_forms_CaseId",
                schema: "Guarantee",
                table: "guarantee_approval_forms",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_case_applications_BeneficiaryNationalId",
                schema: "Guarantee",
                table: "guarantee_case_applications",
                column: "BeneficiaryNationalId");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_case_applications_CaseId",
                schema: "Guarantee",
                table: "guarantee_case_applications",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_case_applications_GuaranteeType",
                schema: "Guarantee",
                table: "guarantee_case_applications",
                column: "GuaranteeType");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_case_applications_RequestedGuaranteeAmount",
                schema: "Guarantee",
                table: "guarantee_case_applications",
                column: "RequestedGuaranteeAmount");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_case_comments_CaseId",
                schema: "Guarantee",
                table: "guarantee_case_comments",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_case_documents_CaseId_DocumentType_Version",
                schema: "Guarantee",
                table: "guarantee_case_documents",
                columns: new[] { "CaseId", "DocumentType", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_case_workflow_history_CaseId",
                schema: "Guarantee",
                table: "guarantee_case_workflow_history",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_ApplicantUserId",
                schema: "Guarantee",
                table: "guarantee_cases",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_ApplicantUserId_CreatedAt",
                schema: "Guarantee",
                table: "guarantee_cases",
                columns: new[] { "ApplicantUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_CaseNumber",
                schema: "Guarantee",
                table: "guarantee_cases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_CompanyId",
                schema: "Guarantee",
                table: "guarantee_cases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_CompanyId_CreatedAt",
                schema: "Guarantee",
                table: "guarantee_cases",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_CreatedAt",
                schema: "Guarantee",
                table: "guarantee_cases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_CurrentPhase_CreatedAt",
                schema: "Guarantee",
                table: "guarantee_cases",
                columns: new[] { "CurrentPhase", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_CurrentStatus",
                schema: "Guarantee",
                table: "guarantee_cases",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_cases_CurrentStatus_CreatedAt",
                schema: "Guarantee",
                table: "guarantee_cases",
                columns: new[] { "CurrentStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_renewal_cases_CaseNumber",
                schema: "Guarantee",
                table: "guarantee_renewal_cases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guarantee_renewal_cases_ParentGuaranteeCaseId",
                schema: "Guarantee",
                table: "guarantee_renewal_cases",
                column: "ParentGuaranteeCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_ApplicantType_CreatedAt",
                schema: "Investment",
                table: "investment_cases",
                columns: new[] { "ApplicantType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_ApplicantUserId",
                schema: "Investment",
                table: "investment_cases",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_ApplicantUserId_CreatedAt",
                schema: "Investment",
                table: "investment_cases",
                columns: new[] { "ApplicantUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_CaseNumber",
                schema: "Investment",
                table: "investment_cases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_CompanyId",
                schema: "Investment",
                table: "investment_cases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_CompanyId_CreatedAt",
                schema: "Investment",
                table: "investment_cases",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_CreatedAt",
                schema: "Investment",
                table: "investment_cases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_CurrentPhase_CreatedAt",
                schema: "Investment",
                table: "investment_cases",
                columns: new[] { "CurrentPhase", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_CurrentStatus",
                schema: "Investment",
                table: "investment_cases",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_investment_cases_CurrentStatus_CreatedAt",
                schema: "Investment",
                table: "investment_cases",
                columns: new[] { "CurrentStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_approval_details_ApprovedAmount",
                schema: "Loan",
                table: "loan_approval_details",
                column: "ApprovedAmount");

            migrationBuilder.CreateIndex(
                name: "IX_loan_approval_details_CaseId",
                schema: "Loan",
                table: "loan_approval_details",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_approval_details_FacilityType",
                schema: "Loan",
                table: "loan_approval_details",
                column: "FacilityType");

            migrationBuilder.CreateIndex(
                name: "IX_loan_approval_details_IsCreditLineActive",
                schema: "Loan",
                table: "loan_approval_details",
                column: "IsCreditLineActive");

            migrationBuilder.CreateIndex(
                name: "IX_loan_approval_details_RepaymentMonths",
                schema: "Loan",
                table: "loan_approval_details",
                column: "RepaymentMonths");

            migrationBuilder.CreateIndex(
                name: "IX_loan_case_applications_ApplicantCategory",
                schema: "Loan",
                table: "loan_case_applications",
                column: "ApplicantCategory");

            migrationBuilder.CreateIndex(
                name: "IX_loan_case_applications_CaseId",
                schema: "Loan",
                table: "loan_case_applications",
                column: "CaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_case_applications_RequestedAmount",
                schema: "Loan",
                table: "loan_case_applications",
                column: "RequestedAmount");

            migrationBuilder.CreateIndex(
                name: "IX_loan_case_comments_CaseId",
                schema: "Loan",
                table: "loan_case_comments",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_case_documents_CaseId_DocumentType_Version",
                schema: "Loan",
                table: "loan_case_documents",
                columns: new[] { "CaseId", "DocumentType", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_case_workflow_history_CaseId",
                schema: "Loan",
                table: "loan_case_workflow_history",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_case_workflow_history_CorrelationId",
                schema: "Loan",
                table: "loan_case_workflow_history",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_ApplicantType_CreatedAt",
                schema: "Loan",
                table: "loan_cases",
                columns: new[] { "ApplicantType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_ApplicantUserId",
                schema: "Loan",
                table: "loan_cases",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_ApplicantUserId_CreatedAt",
                schema: "Loan",
                table: "loan_cases",
                columns: new[] { "ApplicantUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_CaseNumber",
                schema: "Loan",
                table: "loan_cases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_CompanyId",
                schema: "Loan",
                table: "loan_cases",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_CompanyId_CreatedAt",
                schema: "Loan",
                table: "loan_cases",
                columns: new[] { "CompanyId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_CreatedAt",
                schema: "Loan",
                table: "loan_cases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_CurrentPhase_CreatedAt",
                schema: "Loan",
                table: "loan_cases",
                columns: new[] { "CurrentPhase", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_CurrentStatus",
                schema: "Loan",
                table: "loan_cases",
                column: "CurrentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_loan_cases_CurrentStatus_CreatedAt",
                schema: "Loan",
                table: "loan_cases",
                columns: new[] { "CurrentStatus", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_installments_CaseId_IsPaid",
                schema: "Loan",
                table: "loan_installments",
                columns: new[] { "CaseId", "IsPaid" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_installments_CaseId_RowNumber",
                schema: "Loan",
                table: "loan_installments",
                columns: new[] { "CaseId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loan_installments_InstallmentDate_IsPaid_IsGracePeriod",
                schema: "Loan",
                table: "loan_installments",
                columns: new[] { "InstallmentDate", "IsPaid", "IsGracePeriod" });

            migrationBuilder.CreateIndex(
                name: "IX_loan_payments_CaseId_StageNumber",
                schema: "Loan",
                table: "loan_payments",
                columns: new[] { "CaseId", "StageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_CaseId_PaymentDate",
                schema: "Investment",
                table: "payment_records",
                columns: new[] { "CaseId", "PaymentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_records_TransactionNumber",
                schema: "Investment",
                table: "payment_records",
                column: "TransactionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_LastCommandCorrelationId",
                schema: "Process",
                table: "process_instances",
                column: "LastCommandCorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_Module_CaseId",
                schema: "Process",
                table: "process_instances",
                columns: new[] { "Module", "CaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_Module_Status",
                schema: "Process",
                table: "process_instances",
                columns: new[] { "Module", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_process_instances_WorkflowInstanceId",
                schema: "Process",
                table: "process_instances",
                column: "WorkflowInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "Identity",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_SessionId",
                schema: "Identity",
                table: "RefreshTokens",
                columns: new[] { "UserId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_User_CompanyId",
                schema: "Identity",
                table: "User",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_User_Email",
                schema: "Identity",
                table: "User",
                column: "Email",
                unique: true,
                filter: "\"Email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_User_NationalCode",
                schema: "Identity",
                table: "User",
                column: "NationalCode");

            migrationBuilder.CreateIndex(
                name: "IX_User_PhoneNumber",
                schema: "Identity",
                table: "User",
                column: "PhoneNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_SessionId",
                schema: "Identity",
                table: "UserSessions",
                column: "SessionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_RevokedAt",
                schema: "Identity",
                table: "UserSessions",
                columns: new[] { "UserId", "RevokedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_case_comment_attachments_case_comments_InvestmentCaseCommen~",
                schema: "Investment",
                table: "case_comment_attachments",
                column: "InvestmentCaseCommentId",
                principalSchema: "Investment",
                principalTable: "case_comments",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_case_comments_investment_cases_CaseId",
                schema: "Investment",
                table: "case_comments",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_data_entry_1_investment_cases_CaseId",
                schema: "Investment",
                table: "case_data_entry_1",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_data_entry_2_investment_cases_CaseId",
                schema: "Investment",
                table: "case_data_entry_2",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_documents_investment_cases_CaseId",
                schema: "Investment",
                table: "case_documents",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_evaluation_items_case_evaluations_EvaluationId",
                schema: "Investment",
                table: "case_evaluation_items",
                column: "EvaluationId",
                principalSchema: "Investment",
                principalTable: "case_evaluations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_evaluations_investment_cases_CaseId",
                schema: "Investment",
                table: "case_evaluations",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_revisions_investment_cases_CaseId",
                schema: "Investment",
                table: "case_revisions",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_valuations_investment_cases_CaseId",
                schema: "Investment",
                table: "case_valuations",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_case_workflow_history_investment_cases_CaseId",
                schema: "Investment",
                table: "case_workflow_history",
                column: "CaseId",
                principalSchema: "Investment",
                principalTable: "investment_cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Company_User_OwnerUserId",
                schema: "Identity",
                table: "Company",
                column: "OwnerUserId",
                principalSchema: "Identity",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Company_User_OwnerUserId",
                schema: "Identity",
                table: "Company");

            migrationBuilder.DropTable(
                name: "case_comment_attachments",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_data_entry_1",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_data_entry_2",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_documents",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_evaluation_items",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_revisions",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_valuations",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_workflow_history",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "dashboard_stats_snapshots",
                schema: "Analytics");

            migrationBuilder.DropTable(
                name: "financial_worksheets",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "fund_credit_limits",
                schema: "Fund");

            migrationBuilder.DropTable(
                name: "guarantee_amendment_history_records",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "guarantee_applicant_credit_profiles",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "guarantee_approval_forms",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "guarantee_case_applications",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "guarantee_case_comments",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "guarantee_case_documents",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "guarantee_case_workflow_history",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "guarantee_renewal_cases",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "loan_approval_details",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "loan_case_applications",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "loan_case_comments",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "loan_case_documents",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "loan_case_workflow_history",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "loan_installments",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "loan_payments",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "payment_records",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "process_instances",
                schema: "Process");

            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "UserSessions",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "case_comments",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "case_evaluations",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "guarantee_cases",
                schema: "Guarantee");

            migrationBuilder.DropTable(
                name: "loan_cases",
                schema: "Loan");

            migrationBuilder.DropTable(
                name: "investment_cases",
                schema: "Investment");

            migrationBuilder.DropTable(
                name: "User",
                schema: "Identity");

            migrationBuilder.DropTable(
                name: "Company",
                schema: "Identity");
        }
    }
}
