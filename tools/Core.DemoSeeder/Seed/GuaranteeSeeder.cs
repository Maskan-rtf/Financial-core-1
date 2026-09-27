using Core.Application.Common;
using Core.Domain.Entities.Guarantee;
using Core.Domain.Entities.Workflow;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.DemoSeeder.Seed;

internal static class GuaranteeSeeder
{
    public static void Seed(DemoDbContext db, IdentitySeeder.DemoActors a, SchemaFeatures features)
    {
        var day = DateTimeOffset.UtcNow.Date;
        var seq = 1;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1) Draft
        var draft = Create(
            CaseNumberFormat.Build("GC", day, seq++),
            a.ApplicantNavaco.Id.ToString(),
            ApplicantType.Company,
            a.Navaco.Id,
            "ضمانت‌نامه شرکت در مناقصه — پروژه پالایشگاه اصفهان");
        draft.UpsertApplication(
            GuaranteeType.Tender,
            "شرکت در مناقصه تأمین تجهیزات ابزار دقیق پالایشگاه",
            true,
            "شرکت پالایش نفت اصفهان",
            "10100123456",
            BeneficiaryCompanyType.Governmental,
            ApplicantCategory.KnowledgeBased,
            null,
            ApplicantLegalForm.KnowledgeBased,
            "MNQ-1404-118",
            85_000_000_000m,
            "هشتاد و پنج میلیارد ریال",
            15m,
            "اصفهان",
            4_250_000_000m,
            90,
            today,
            today.AddDays(90),
            "سفته شرکتی به مبلغ ۵ میلیارد ریال و چک مدیران",
            "تضمین شرکت در مناقصه عمومی");
        db.GuaranteeCases.Add(draft);

        // 2) Credit review
        var creditReview = Create(
            CaseNumberFormat.Build("GC", day, seq++),
            a.ApplicantParsa.Id.ToString(),
            ApplicantType.Company,
            a.ParsaTech.Id,
            "ضمانت‌نامه حسن انجام کار — قرارداد وزارت ارتباطات");
        FillApplication(creditReview,
            GuaranteeType.PerformanceBond,
            "حسن انجام تعهدات قرارداد پشتیبانی نرم‌افزاری",
            "وزارت ارتباطات و فناوری اطلاعات",
            "14000123456",
            BeneficiaryCompanyType.Governmental,
            12_000_000_000m,
            today,
            today.AddDays(365),
            "تهران");
        Advance(creditReview, [
            (GuaranteeCaseStatus.DataEntry, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, GuaranteeWorkflowAction.Submit, "ارسال درخواست"),
            (GuaranteeCaseStatus.CreditReview, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert, GuaranteeWorkflowAction.Approve, "در صف بررسی اعتبار")
        ]);
        creditReview.AddDiscussionComment(GuaranteeCasePhase.Application, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert,
            "گردش حساب بانکی شش‌ماهه و استعلام اعتباری دریافت شد. در حال تحلیل نسبت‌های مالی هستیم.", false, true);
        creditReview.AddDocument("demo/guarantee/parsa/credit-inquiry.pdf", "استعلام-اعتباری.pdf", "application/pdf", 420_000, 1, GuaranteeDocumentType.CreditInquiryResult, a.CreditExpert.Id.ToString());
        db.GuaranteeCases.Add(creditReview);
        if (features.HasProcessInstances)
            db.ProcessInstances.Add(new ProcessInstance(CaseModuleType.Guarantee, creditReview.Id, $"demo-wf-gc-{creditReview.Id:N}"[..32]));

        // 3) Legal / waiting draft contract
        var legal = Create(
            CaseNumberFormat.Build("GC", day, seq++),
            a.ApplicantSepanta.Id.ToString(),
            ApplicantType.Company,
            a.SepantaEnergy.Id,
            "ضمانت‌نامه پیش‌پرداخت — قرارداد احداث نیروگاه");
        FillApplication(legal,
            GuaranteeType.AdvancePayment,
            "پیش‌پرداخت قرارداد احداث نیروگاه خورشیدی ۲۰ مگاواتی",
            "ساتبا",
            "14000987654",
            BeneficiaryCompanyType.PublicNonGovernmental,
            28_000_000_000m,
            today.AddDays(-10),
            today.AddDays(350),
            "اصفهان");
        legal.UpsertApprovalForm(
            60_000_000_000m,
            18_000_000_000m,
            8_500_000_000m,
            33_500_000_000m,
            GuaranteeType.AdvancePayment,
            28_000_000_000m,
            "بیست و هشت میلیارد ریال",
            "پیش‌پرداخت احداث نیروگاه",
            "ساتبا",
            today.AddDays(-5),
            today.AddDays(350),
            355,
            10m,
            2_800_000_000m,
            1.5m,
            420_000_000m,
            "وثیقه ملکی و سفته مدیران",
            "آقای مجید قنبری — ضامن حقیقی",
            "سقف اعتبار متقاضی پس از صدور کافی است.");
        Advance(legal, BuildPathTo(GuaranteeCaseStatus.WaitingDraftContract, a));
        legal.AddDocument("demo/guarantee/sepanta/draft-request.pdf", "درخواست-صدور.pdf", "application/pdf", 310_000, 1, GuaranteeDocumentType.GuaranteeIssuanceRequestForm, a.ApplicantSepanta.Id.ToString());
        db.GuaranteeCases.Add(legal);

        // 4) Completed guarantee + renewal
        var completed = Create(
            CaseNumberFormat.Build("GC", day.AddDays(-120), 1),
            a.ApplicantNavaco.Id.ToString(),
            ApplicantType.Company,
            a.Navaco.Id,
            "ضمانت‌نامه تعهد پرداخت — قرارداد تأمین قطعات");
        FillApplication(completed,
            GuaranteeType.PaymentCommitment,
            "تعهد پرداخت قرارداد تأمین قطعات ابزار دقیق",
            "شرکت ملی گاز ایران",
            "10100777111",
            BeneficiaryCompanyType.Governmental,
            15_000_000_000m,
            today.AddDays(-120),
            today.AddDays(60),
            "تهران");
        completed.UpsertApprovalForm(
            80_000_000_000m,
            25_000_000_000m,
            10_000_000_000m,
            45_000_000_000m,
            GuaranteeType.PaymentCommitment,
            15_000_000_000m,
            "پانزده میلیارد ریال",
            "تعهد پرداخت تأمین قطعات",
            "شرکت ملی گاز ایران",
            today.AddDays(-100),
            today.AddDays(60),
            160,
            10m,
            1_500_000_000m,
            1.2m,
            180_000_000m,
            "چک و سفته شرکتی",
            "مدیران شرکت ناوکو",
            null);
        Advance(completed, BuildPathTo(GuaranteeCaseStatus.Completed, a));
        completed.AddDocument("demo/guarantee/navaco/instrument.pdf", "ضمانت‌نامه-صادره.pdf", "application/pdf", 550_000, 1, GuaranteeDocumentType.GuaranteeInstrument, a.FinancialExpert.Id.ToString());
        completed.AddDocument("demo/guarantee/navaco/issuance-receipt.pdf", "رسید-صدور.pdf", "application/pdf", 180_000, 1, GuaranteeDocumentType.IssuanceReceipt, a.FinancialExpert.Id.ToString());
        db.GuaranteeCases.Add(completed);

        var renewal = new GuaranteeRenewalCase(
            CaseNumberFormat.Build("GR", day, 1),
            a.ApplicantNavaco.Id.ToString(),
            completed.Id,
            RenewalKind.Extension,
            today.AddDays(180),
            null);
        renewal.TransitionTo(GuaranteeRenewalStatus.CeoReview, a.ApplicantNavaco.Id.ToString());
        db.GuaranteeRenewalCases.Add(renewal);

        // 5) Amendment in progress
        var amendment = Create(
            CaseNumberFormat.Build("GC", day.AddDays(-90), 2),
            a.ApplicantParsa.Id.ToString(),
            ApplicantType.Company,
            a.ParsaTech.Id,
            "ضمانت‌نامه حسن انجام کار — قرارداد شهرداری (در حال اصلاح)");
        FillApplication(amendment,
            GuaranteeType.PerformanceBond,
            "حسن انجام تعهدات قرارداد هوشمندسازی خدمات شهری",
            "شهرداری تهران",
            "14005556666",
            BeneficiaryCompanyType.Governmental,
            9_500_000_000m,
            today.AddDays(-90),
            today.AddDays(30),
            "تهران");
        Advance(amendment, BuildPathTo(GuaranteeCaseStatus.Completed, a));
        amendment.UpsertAmendment(
            AmendmentType.Extension,
            today.AddDays(120),
            null,
            "تمدید مدت اعتبار به دلیل تأخیر در تحویل فاز عملیاتی از سوی کارفرما");
        amendment.TransitionTo(GuaranteeCaseStatus.AmendmentDraft, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, GuaranteeWorkflowAction.BeginAmendment, Guid.NewGuid(), "شروع فرآیند اصلاحیه");
        amendment.TransitionTo(GuaranteeCaseStatus.AmendmentDataEntry, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, GuaranteeWorkflowAction.Submit, Guid.NewGuid(), null);
        amendment.TransitionTo(GuaranteeCaseStatus.AmendmentCeoApproval, a.CreditManager.Id.ToString(), UserRoleClaims.CreditManager, GuaranteeWorkflowAction.Approve, Guid.NewGuid(), "بدون نیاز به بازنگری اعتباری");

        var history = new GuaranteeAmendmentHistoryRecord(
            amendment.Id,
            AmendmentType.Extension,
            """{"validityTo":"previous"}""",
            """{"validityTo":"extended-120-days"}""",
            "تمدید به درخواست کارفرما",
            a.ApplicantParsa.Id.ToString(),
            DateTimeOffset.UtcNow.AddDays(-2));
        amendment.AmendmentHistoryRecords.Add(history);
        db.GuaranteeCases.Add(amendment);

        // 6) Individual — rejected sample for realism
        var rejected = Create(
            CaseNumberFormat.Build("GC", day.AddDays(-15), 3),
            a.ApplicantIndividual.Id.ToString(),
            ApplicantType.Individual,
            null,
            "ضمانت‌نامه شرکت در مناقصه — متقاضی حقیقی");
        FillApplication(rejected,
            GuaranteeType.Tender,
            "شرکت در مناقصه خدمات مشاوره فناوری",
            "سازمان برنامه و بودجه",
            "14001231231",
            BeneficiaryCompanyType.Governmental,
            2_000_000_000m,
            today.AddDays(-15),
            today.AddDays(60),
            "تهران");
        Advance(rejected, [
            (GuaranteeCaseStatus.DataEntry, a.ApplicantIndividual.Id.ToString(), UserRoleClaims.Applicant, GuaranteeWorkflowAction.Submit, null),
            (GuaranteeCaseStatus.CreditReview, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert, GuaranteeWorkflowAction.Approve, null),
            (GuaranteeCaseStatus.Rejected, a.CreditManager.Id.ToString(), UserRoleClaims.CreditManager, GuaranteeWorkflowAction.Reject, "سقف اعتبار متقاضی برای مبلغ درخواستی کافی نیست")
        ]);
        rejected.AddDiscussionComment(GuaranteeCasePhase.Closing, a.CreditManager.Id.ToString(), UserRoleClaims.CreditManager,
            "پیشنهاد می‌شود پس از افزایش سقف اعتبار یا کاهش مبلغ ضمانت، مجدداً اقدام شود.", false, false);
        db.GuaranteeCases.Add(rejected);
    }

    private static GuaranteeCase Create(
        string caseNumber,
        string applicantUserId,
        ApplicantType type,
        Guid? companyId,
        string title)
    {
        var entity = new GuaranteeCase(caseNumber, applicantUserId, type);
        entity.SetTitle(title);
        if (companyId.HasValue)
            entity.AssignCompany(companyId.Value);
        return entity;
    }

    private static void FillApplication(
        GuaranteeCase entity,
        GuaranteeType type,
        string subject,
        string beneficiary,
        string beneficiaryNationalId,
        BeneficiaryCompanyType beneficiaryType,
        decimal amount,
        DateOnly from,
        DateOnly to,
        string province)
    {
        entity.UpsertApplication(
            type,
            subject,
            true,
            beneficiary,
            beneficiaryNationalId,
            beneficiaryType,
            ApplicantCategory.KnowledgeBased | ApplicantCategory.Technologist,
            null,
            ApplicantLegalForm.Private,
            $"CNT-{from:yyyy}-{amount / 1_000_000_000m:0}",
            amount * 4,
            null,
            10m,
            province,
            amount,
            to.DayNumber - from.DayNumber,
            from,
            to,
            "وثایق متعارف صندوق مطابق آیین‌نامه",
            subject);
    }

    private static void Advance(
        GuaranteeCase entity,
        IEnumerable<(GuaranteeCaseStatus Status, string UserId, string Role, GuaranteeWorkflowAction Action, string? Comment)> steps)
    {
        foreach (var step in steps)
            entity.TransitionTo(step.Status, step.UserId, step.Role, step.Action, Guid.NewGuid(), step.Comment);
    }

    private static List<(GuaranteeCaseStatus, string, string, GuaranteeWorkflowAction, string?)> BuildPathTo(
        GuaranteeCaseStatus target,
        IdentitySeeder.DemoActors a)
    {
        var path = new List<(GuaranteeCaseStatus, string, string, GuaranteeWorkflowAction, string?)>
        {
            (GuaranteeCaseStatus.DataEntry, a.ApplicantNavaco.Id.ToString(), UserRoleClaims.Applicant, GuaranteeWorkflowAction.Submit, "ارسال درخواست"),
            (GuaranteeCaseStatus.CreditReview, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert, GuaranteeWorkflowAction.Approve, "بررسی اعتبار انجام شد"),
            (GuaranteeCaseStatus.ApprovalFormEntry, a.CreditManager.Id.ToString(), UserRoleClaims.CreditManager, GuaranteeWorkflowAction.Approve, "تکمیل فرم مصوبه"),
            (GuaranteeCaseStatus.CeoApprovalInitial, a.Ceo.Id.ToString(), UserRoleClaims.Ceo, GuaranteeWorkflowAction.Approve, "تأیید اولیه مدیرعامل"),
            (GuaranteeCaseStatus.WaitingDraftContract, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, GuaranteeWorkflowAction.UploadDraftContract, "ارسال پیش‌نویس قرارداد"),
            (GuaranteeCaseStatus.WaitingSignedContractAndAttachments, a.ApplicantNavaco.Id.ToString(), UserRoleClaims.Applicant, GuaranteeWorkflowAction.SubmitSignedPackage, "بارگذاری مدارک امضا شده"),
            (GuaranteeCaseStatus.FinancialAttachmentReview, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, GuaranteeWorkflowAction.ApproveAttachments, "تأیید پیوست‌های مالی"),
            (GuaranteeCaseStatus.WaitingFinalContract, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, GuaranteeWorkflowAction.UploadFinalContract, "قرارداد نهایی"),
            (GuaranteeCaseStatus.CeoApprovalFinal, a.Ceo.Id.ToString(), UserRoleClaims.Ceo, GuaranteeWorkflowAction.Approve, "تأیید نهایی مدیرعامل"),
            (GuaranteeCaseStatus.WaitingIssuanceDocuments, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, GuaranteeWorkflowAction.UploadIssuanceDocuments, "آماده‌سازی اسناد صدور"),
            (GuaranteeCaseStatus.Completed, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, GuaranteeWorkflowAction.Approve, "صدور ضمانت‌نامه و مختومه")
        };

        var index = path.FindIndex(x => x.Item1 == target);
        return index < 0 ? path : path.Take(index + 1).ToList();
    }
}
