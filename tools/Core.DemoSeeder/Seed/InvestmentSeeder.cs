using Core.Application.Common;
using Core.Domain.Entities.Investment;
using Core.Domain.Entities.Workflow;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.DemoSeeder.Seed;

internal static class InvestmentSeeder
{
    public static void Seed(DemoDbContext db, IdentitySeeder.DemoActors a, SchemaFeatures features)
    {
        var day = DateTimeOffset.UtcNow.Date;
        var seq = 1;

        // 1) Draft — تازه ثبت‌شده
        var draft = CreateCase(
            CaseNumberFormat.Build("IC", day, seq++),
            a.ApplicantNavaco.Id.ToString(),
            ApplicantType.Company,
            a.Navaco.Id,
            "جذب سرمایه برای خط تولید حسگرهای صنعتی");
        draft.UpsertApplicantProfile("کامران یزدانی", BusinessStage.HasPrototype, "kamran.yazdani@navaco.ir", 25_000_000_000m);
        draft.UpsertAttractionBasis("توسعه محصول دانش‌بنیان در زنجیره ارزش صنعت نفت و گاز با تمرکز بر بومی‌سازی حسگرهای فشار و دما.");
        draft.AddDocument("demo/investment/navaco/pitch-deck-v1.pdf", "PitchDeck-Navaco.pdf", "application/pdf", 2_450_000, 1, DocumentType.PitchDeck, a.ApplicantNavaco.Id.ToString());
        db.InvestmentCases.Add(draft);

        // 2) Review Data Entry 1 — در صف کارشناس سرمایه‌گذاری
        var review1 = CreateCase(
            CaseNumberFormat.Build("IC", day, seq++),
            a.ApplicantParsa.Id.ToString(),
            ApplicantType.Company,
            a.ParsaTech.Id,
            "توسعه پلتفرم تحلیل داده صنعتی پارساتک");
        review1.UpsertApplicantProfile("لیلا طاهری", BusinessStage.HasPrototype, "leila.taheri@parsatech.ir", 18_000_000_000m);
        review1.UpsertAttractionBasis("گسترش سهم بازار SaaS در حوزه نگهداری پیش‌بینانه کارخانجات و جذب سرمایه‌گذار استراتژیک.");
        review1.AddDocument("demo/investment/parsa/registration.pdf", "آگهی-تاسیس-پارساتک.pdf", "application/pdf", 890_000, 1, DocumentType.CompanyRegistration, a.ApplicantParsa.Id.ToString());
        review1.AddDocument("demo/investment/parsa/financials.pdf", "صورت‌های-مالی-۳سال.pdf", "application/pdf", 1_200_000, 1, DocumentType.FinancialStatements, a.ApplicantParsa.Id.ToString());
        Advance(review1, [
            (CaseStatus.DataEntry1, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, "ارسال فرم ورود اطلاعات ۱"),
            (CaseStatus.ReviewDataEntry1, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, "آماده بررسی کارشناس سرمایه‌گذاری")
        ]);
        review1.AddDiscussionComment(CasePhase.Application, a.InvestmentExpert.Id.ToString(), UserRoleClaims.InvestmentExpert,
            "لطفاً فهرست مشتریان فعال و قراردادهای فروش سال جاری را نیز بارگذاری کنید.", false, true);
        AddEvaluation(review1, CasePhase.Application, a.InvestmentExpert, UserRoleClaims.InvestmentExpert,
            "بررسی اولیه مدارک ورود اطلاعات ۱", EvaluationItemTitle.DataEntry1Checklist, approved: true);
        db.InvestmentCases.Add(review1);
        if (features.HasProcessInstances)
            db.ProcessInstances.Add(new ProcessInstance(CaseModuleType.Investment, review1.Id, $"demo-wf-inv-{review1.Id:N}"[..32]));

        // 3) Initial valuation — مرحله ارزش‌گذاری
        var valuation = CreateCase(
            CaseNumberFormat.Build("IC", day, seq++),
            a.ApplicantSepanta.Id.ToString(),
            ApplicantType.Company,
            a.SepantaEnergy.Id,
            "تأمین مالی توسعه مزرعه خورشیدی سپنتا — فاز ۲");
        valuation.UpsertApplicantProfile("مجید قنبری", BusinessStage.HasPrototype, "majid.ghanbari@sepanta-energy.ir", 120_000_000_000m);
        valuation.UpsertAttractionBasis("افزایش ظرفیت تولید برق تجدیدپذیر و جذب سرمایه‌گذار نهادی برای فاز دوم پروژه.");
        Advance(valuation, [
            (CaseStatus.DataEntry1, a.ApplicantSepanta.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, null),
            (CaseStatus.ReviewDataEntry1, a.InvestmentExpert.Id.ToString(), UserRoleClaims.InvestmentExpert, WorkflowAction.Approve, "مدارک کامل است"),
            (CaseStatus.DataEntry2, a.ApplicantSepanta.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, null),
            (CaseStatus.ReviewDataEntry2, a.InvestmentExpert.Id.ToString(), UserRoleClaims.InvestmentExpert, WorkflowAction.Approve, "ارزیابی فنی/مالی قابل قبول"),
            (CaseStatus.InitialValuation, a.InvestmentManager.Id.ToString(), UserRoleClaims.InvestmentManager, WorkflowAction.Approve, "ورود به ارزش‌گذاری اولیه")
        ]);
        valuation.AddValuation(ValuationType.Primary, 95_000_000_000m, "ارزش‌گذاری اولیه بر اساس DCF و مقایسه با معاملات مشابه انرژی تجدیدپذیر.", a.InvestmentManager.Id.ToString());
        valuation.AddDiscussionComment(CasePhase.Valuation, a.InvestmentManager.Id.ToString(), UserRoleClaims.InvestmentManager,
            "فرض رشد درآمد سالانه ۱۲٪ و نرخ تنزیل ۱۸٪ در مدل اعمال شده است.", false, true);
        db.InvestmentCases.Add(valuation);

        // 4) Waiting CEO approval — نزدیک پایان
        var ceoPending = CreateCase(
            CaseNumberFormat.Build("IC", day, seq++),
            a.ApplicantParsa.Id.ToString(),
            ApplicantType.Company,
            a.ParsaTech.Id,
            "افزایش سرمایه برای توسعه محصول هوش مصنوعی کیفیت‌سنجی");
        ceoPending.UpsertApplicantProfile("لیلا طاهری", BusinessStage.HasPrototype, "leila.taheri@parsatech.ir", 32_000_000_000m);
        ceoPending.UpsertAttractionBasis("تکمیل محصول Vision-QC و ورود به بازار خودروسازان داخلی.");
        ceoPending.UpsertFinancialWorksheet(
            "بانک ملت",
            "IR120170000000123456789001",
            28_000_000_000m,
            "پرداخت در دو قسط مساوی طی ۶۰ روز پس از امضای قرارداد نهایی",
            "کارمزد صندوق طبق مصوبه هیئت‌مدیره محاسبه می‌شود.");
        Advance(ceoPending, BuildPathTo(CaseStatus.WaitingCeoApproval, a));
        ceoPending.AddValuation(ValuationType.Primary, 30_000_000_000m, "ارزش‌گذاری اولیه", a.InvestmentManager.Id.ToString());
        ceoPending.AddValuation(ValuationType.Secondary, 28_500_000_000m, "بازنگری پس از مذاکره با متقاضی", a.InvestmentManager.Id.ToString());
        ceoPending.AddDocument("demo/investment/parsa/signed-contract.pdf", "قرارداد-امضاشده.pdf", "application/pdf", 1_100_000, 1, DocumentType.SignedContract, a.LegalExpert.Id.ToString());
        db.InvestmentCases.Add(ceoPending);

        // 5) Completed — پرونده موفق
        var completed = CreateCase(
            CaseNumberFormat.Build("IC", day.AddDays(-40), 1),
            a.ApplicantNavaco.Id.ToString(),
            ApplicantType.Company,
            a.Navaco.Id,
            "جذب سرمایه برای تجاری‌سازی ربات‌های بازرسی خطوط لوله");
        completed.UpsertApplicantProfile("کامران یزدانی", BusinessStage.HasPrototype, "kamran.yazdani@navaco.ir", 40_000_000_000m);
        completed.UpsertAttractionBasis("تجاری‌سازی فناوری رباتیک بازرسی و عقد قرارداد با شرکت‌های انتقال گاز.");
        completed.UpsertFinancialWorksheet(
            "بانک تجارت",
            "IR550180000000987654321001",
            35_000_000_000m,
            "پرداخت یکجا پس از تأیید مدیرعامل",
            "پرونده نمونه دمو — تکمیل‌شده");
        Advance(completed, BuildPathTo(CaseStatus.WaitingPayment, a));
        completed.AddValuation(ValuationType.Primary, 38_000_000_000m, null, a.InvestmentManager.Id.ToString());
        completed.AddValuation(ValuationType.Secondary, 35_000_000_000m, "مبلغ نهایی مصوب کمیته سرمایه‌گذاری", a.InvestmentManager.Id.ToString());
        completed.AddPayment(
            20_000_000_000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-20)),
            "TRX-INV-1404-88421",
            "demo/investment/navaco/receipt-1.pdf",
            "قسط اول",
            PaymentMethod.BankTransfer,
            PaymentStatus.Completed,
            a.FinancialExpert.Id.ToString());
        completed.AddPayment(
            15_000_000_000m,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)),
            "TRX-INV-1404-90112",
            "demo/investment/navaco/receipt-2.pdf",
            "قسط دوم — تسویه کامل",
            PaymentMethod.BankTransfer,
            PaymentStatus.Completed,
            a.FinancialExpert.Id.ToString());
        // AddPayment may auto-complete when WaitingPayment + full amount
        if (completed.CurrentStatus != CaseStatus.Completed)
            completed.TransitionTo(CaseStatus.Completed, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, WorkflowAction.CompletePayment, null, "تسویه کامل و مختومه");
        completed.AddDiscussionComment(CasePhase.Closing, a.Ceo.Id.ToString(), UserRoleClaims.Ceo,
            "پرونده با موفقیت مختومه شد. گزارش عملکرد سه‌ماهه از متقاضی دریافت شود.", false, false);
        var revision = completed.CreateNewRevision(CasePhase.Application, a.ApplicantNavaco.Id.ToString(), DateTimeOffset.UtcNow.AddDays(-55));
        revision.MarkReviewed(a.InvestmentExpert.Id.ToString(), ReviewResult.Approved, DateTimeOffset.UtcNow.AddDays(-50));
        db.InvestmentCases.Add(completed);

        // 6) Individual applicant — mid pipeline
        var individual = CreateCase(
            CaseNumberFormat.Build("IC", day, seq++),
            a.ApplicantIndividual.Id.ToString(),
            ApplicantType.Individual,
            companyId: null,
            "حمایت از استارتاپ سلامت دیجیتال — پلتفرم پیگیری درمان");
        individual.UpsertApplicantProfile("فرناز مرادی", BusinessStage.Idea, "farnaz.moradi@gmail.com", 4_500_000_000m);
        individual.UpsertAttractionBasis("توسعه MVP سلامت دیجیتال و جذب سرمایه‌گذار فرشته برای ورود به بازار کلینیک‌ها.");
        Advance(individual, [
            (CaseStatus.DataEntry1, a.ApplicantIndividual.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, "ارسال اولیه"),
            (CaseStatus.ReviewDataEntry1, a.InvestmentExpert.Id.ToString(), UserRoleClaims.InvestmentExpert, WorkflowAction.RequestRevision, "نیاز به تکمیل بیزینس‌پلن"),
            (CaseStatus.DataEntry1, a.InvestmentExpert.Id.ToString(), UserRoleClaims.InvestmentExpert, WorkflowAction.RequestRevision, "بازگشت برای تکمیل"),
            (CaseStatus.DataEntry1, a.ApplicantIndividual.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, "ارسال مجدد پس از اصلاح"),
            (CaseStatus.ReviewDataEntry1, a.ApplicantIndividual.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, null)
        ]);
        individual.RequestRevision(
            CaseStatus.DataEntry1,
            a.InvestmentExpert.Id.ToString(),
            UserRoleClaims.InvestmentExpert,
            WorkflowAction.RequestRevision,
            Guid.NewGuid(),
            "لطفاً مدل درآمدی و اندازه بازار هدف را دقیق‌تر تشریح کنید.",
            isInternal: false);
        db.InvestmentCases.Add(individual);
    }

    private static InvestmentCase CreateCase(
        string caseNumber,
        string applicantUserId,
        ApplicantType type,
        Guid? companyId,
        string title)
    {
        var entity = new InvestmentCase(caseNumber, applicantUserId, type);
        entity.SetTitle(title);
        if (companyId.HasValue)
            entity.AssignCompany(companyId.Value);
        return entity;
    }

    private static void Advance(
        InvestmentCase entity,
        IEnumerable<(CaseStatus Status, string UserId, string Role, WorkflowAction Action, string? Comment)> steps)
    {
        foreach (var step in steps)
            entity.TransitionTo(step.Status, step.UserId, step.Role, step.Action, Guid.NewGuid(), step.Comment);
    }

    private static List<(CaseStatus Status, string UserId, string Role, WorkflowAction Action, string? Comment)> BuildPathTo(
        CaseStatus target,
        IdentitySeeder.DemoActors a)
    {
        var path = new List<(CaseStatus, string, string, WorkflowAction, string?)>
        {
            (CaseStatus.DataEntry1, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, "شروع پرونده"),
            (CaseStatus.ReviewDataEntry1, a.InvestmentExpert.Id.ToString(), UserRoleClaims.InvestmentExpert, WorkflowAction.Approve, "تأیید ورود اطلاعات ۱"),
            (CaseStatus.DataEntry2, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Submit, null),
            (CaseStatus.ReviewDataEntry2, a.InvestmentExpert.Id.ToString(), UserRoleClaims.InvestmentExpert, WorkflowAction.Approve, "تأیید ورود اطلاعات ۲"),
            (CaseStatus.InitialValuation, a.InvestmentManager.Id.ToString(), UserRoleClaims.InvestmentManager, WorkflowAction.Approve, null),
            (CaseStatus.SecondaryValuation, a.InvestmentManager.Id.ToString(), UserRoleClaims.InvestmentManager, WorkflowAction.Approve, "ارزش‌گذاری ثانویه"),
            (CaseStatus.WaitingPreliminaryContract, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, WorkflowAction.Approve, null),
            (CaseStatus.WaitingUserReviewPreliminaryContract, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, WorkflowAction.Approve, "تأیید پیش‌نویس توسط متقاضی"),
            (CaseStatus.ContractDrafting, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, WorkflowAction.Approve, null),
            (CaseStatus.WaitingContractSignature, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, WorkflowAction.Approve, null),
            (CaseStatus.WaitingSignedContractUpload, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, WorkflowAction.Approve, "قرارداد امضا شده بارگذاری شد"),
            (CaseStatus.WaitingFinancialWorksheet, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, WorkflowAction.Approve, null),
            (CaseStatus.FinancialWorksheetReview, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, WorkflowAction.Approve, "کاربرگ مالی تأیید شد"),
            (CaseStatus.WaitingCeoApproval, a.Ceo.Id.ToString(), UserRoleClaims.Ceo, WorkflowAction.Approve, "در انتظار تأیید مدیرعامل"),
            (CaseStatus.WaitingPayment, a.Ceo.Id.ToString(), UserRoleClaims.Ceo, WorkflowAction.Approve, "ابلاغ پرداخت")
        };

        var index = path.FindIndex(x => x.Item1 == target);
        return index < 0 ? path : path.Take(index + 1).ToList();
    }

    private static void AddEvaluation(
        InvestmentCase entity,
        CasePhase phase,
        Core.Domain.Identity.Entities.User reviewer,
        string role,
        string notes,
        IReadOnlyList<string> titles,
        bool approved)
    {
        var evaluation = new InvestmentCaseEvaluation(entity.Id, phase, reviewer.Id.ToString(), role, notes);
        evaluation.SetItems(titles.Select(t => new InvestmentCaseEvaluationItem(evaluation.Id, t, approved, approved ? "مورد تأیید" : "نیاز به تکمیل")));
        entity.Evaluations.Add(evaluation);
    }
}
