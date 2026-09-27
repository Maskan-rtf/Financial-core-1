using Core.Application.Common;
using Core.Domain.Entities.Loan;
using Core.Domain.Enums;
using Core.Domain.Identity;

namespace Core.DemoSeeder.Seed;

internal static class LoanSeeder
{
    public static void Seed(DemoDbContext db, IdentitySeeder.DemoActors a)
    {
        var day = DateTimeOffset.UtcNow.Date;
        var seq = 1;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1) Draft
        var draft = Create(
            CaseNumberFormat.Build("LN", day, seq++),
            a.ApplicantSepanta.Id.ToString(),
            ApplicantType.Company,
            a.SepantaEnergy.Id,
            "تسهیلات سرمایه در گردش — تأمین تجهیزات نیروگاهی");
        draft.UpsertApplication(
            40_000_000_000m,
            "چهل میلیارد ریال",
            "خرید اینورتر و پنل برای تکمیل فاز اول نیروگاه",
            "سفته شرکتی، چک مدیران و وثیقه ملکی",
            ApplicantCategory.KnowledgeBased,
            null,
            "مدیرعامل");
        db.LoanCases.Add(draft);

        // 2) Pending credit review
        var credit = Create(
            CaseNumberFormat.Build("LN", day, seq++),
            a.ApplicantParsa.Id.ToString(),
            ApplicantType.Company,
            a.ParsaTech.Id,
            "تسهیلات خرید دین — تأمین مالی قراردادهای SaaS");
        credit.UpsertApplication(
            22_000_000_000m,
            "بیست و دو میلیارد ریال",
            "تأمین مالی مطالبات قراردادی مشتریان سازمانی",
            "ضمانت‌نامه صندوق و چک شرکتی",
            ApplicantCategory.Technologist | ApplicantCategory.Creative,
            null,
            "مدیر مالی");
        Advance(credit, [
            (LoanCaseStatus.DataEntry, a.ApplicantParsa.Id.ToString(), UserRoleClaims.Applicant, LoanWorkflowAction.Submit, "ارسال پرونده"),
            (LoanCaseStatus.PendingCreditReview, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert, LoanWorkflowAction.Approve, "در صف اعتبارسنجی")
        ]);
        credit.AddDiscussionComment(LoanCasePhase.Application, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert,
            "نسبت جاری و گردش مطالبات در حال بررسی است. لطفاً تفکیک سنی مطالبات را ارسال کنید.", false, true);
        credit.AddDocument("demo/loan/parsa/main-request.pdf", "فرم-درخواست-تسهیلات.pdf", "application/pdf", 380_000, 1, LoanDocumentType.MainLoanRequestForm, a.ApplicantParsa.Id.ToString());
        db.LoanCases.Add(credit);
        // Process instances are optional depending on target schema.

        // 3) Legal / applicant signature
        var legal = Create(
            CaseNumberFormat.Build("LN", day, seq++),
            a.ApplicantNavaco.Id.ToString(),
            ApplicantType.Company,
            a.Navaco.Id,
            "تسهیلات مرابحه — خرید ماشین‌آلات CNC");
        legal.UpsertApplication(
            55_000_000_000m,
            "پنجاه و پنج میلیارد ریال",
            "خرید دو دستگاه CNC پنج‌محوره برای توسعه خط تولید",
            "وثیقه ماشین‌آلات و سفته",
            ApplicantCategory.KnowledgeBased,
            null,
            "مدیرعامل");
        legal.UpsertApprovalDetail(
            0.42m,
            1.35m,
            18.5m,
            80_000_000_000m,
            true,
            25_000_000_000m,
            LoanFacilityType.InstallmentSale,
            "خرید ماشین‌آلات CNC",
            "قرارداد عاملیت بانک ملت",
            50_000_000_000m,
            "پنجاه میلیارد ریال",
            24,
            3,
            18m,
            0.07m,
            "ماشین‌آلات موضوع تسهیلات + سفته مدیران",
            "آقای کامران یزدانی و خانم سمیرا یزدانی",
            "پرداخت در سه مرحله پس از فاکتور فروشنده",
            9_000_000_000m,
            2_450_000_000m);
        Advance(legal, BuildPathTo(LoanCaseStatus.PendingApplicantSignature, a));
        legal.AddDocument("demo/loan/navaco/raw-contract.pdf", "پیش‌نویس-قرارداد.pdf", "application/pdf", 640_000, 1, LoanDocumentType.RawContract, a.LegalExpert.Id.ToString());
        db.LoanCases.Add(legal);

        // 4) Repayment phase with installments
        var repayment = Create(
            CaseNumberFormat.Build("LN", day.AddDays(-200), 1),
            a.ApplicantNavaco.Id.ToString(),
            ApplicantType.Company,
            a.Navaco.Id,
            "تسهیلات مشارکت مدنی — توسعه کارگاه تحقیق و توسعه");
        repayment.UpsertApplication(
            30_000_000_000m,
            "سی میلیارد ریال",
            "توسعه کارگاه R&D و تجهیز آزمایشگاه تست",
            "ضمانت‌نامه حسن انجام کار و چک",
            ApplicantCategory.KnowledgeBased,
            null,
            "مدیرعامل");
        repayment.UpsertApprovalDetail(
            0.38m,
            1.48m,
            21m,
            80_000_000_000m,
            true,
            50_000_000_000m,
            LoanFacilityType.Musharakah,
            "توسعه کارگاه تحقیق و توسعه",
            null,
            28_000_000_000m,
            "بیست و هشت میلیارد ریال",
            12,
            2,
            16m,
            0.06m,
            "وثیقه ملکی کارگاه",
            "مدیران شرکت ناوکو",
            null,
            3_360_000_000m,
            2_613_000_000m);
        Advance(repayment, BuildPathTo(LoanCaseStatus.RepaymentPhase, a));

        var installments = BuildInstallments(repayment.Id, principal: 28_000_000_000m, months: 12, grace: 2, annualRate: 0.16m, start: today.AddMonths(-8));
        for (var i = 0; i < 4; i++)
            installments[i].MarkPaid(DateTimeOffset.UtcNow.AddMonths(-8 + i).AddDays(2));
        repayment.ReplaceInstallments(installments);

        repayment.AddPayment(
            28_000_000_000m,
            today.AddDays(-210),
            "TRX-LN-PAYOUT-8841",
            "demo/loan/navaco/disbursement.pdf",
            "پرداخت اصل تسهیلات به متقاضی",
            stageNumber: 1,
            a.FinancialExpert.Id.ToString());
        repayment.AddDocument("demo/loan/navaco/schedule.xlsx", "جدول-اقساط.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 95_000, 1, LoanDocumentType.InstallmentScheduleExport, a.FinancialExpert.Id.ToString());
        repayment.AddDiscussionComment(LoanCasePhase.Repayment, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert,
            "چهار قسط اول به‌موقع وصول شده است. یادآوری قسط بعدی هفت روز قبل ارسال می‌شود.", false, true);
        db.LoanCases.Add(repayment);

        // 5) Completed loan
        var completed = Create(
            CaseNumberFormat.Build("LN", day.AddDays(-400), 1),
            a.ApplicantParsa.Id.ToString(),
            ApplicantType.Company,
            a.ParsaTech.Id,
            "تسهیلات جعاله — ارتقای زیرساخت دیتاسنتر");
        completed.UpsertApplication(
            12_000_000_000m,
            "دوازده میلیارد ریال",
            "ارتقای سرورها و تجهیزات ذخیره‌سازی",
            "چک شرکتی",
            ApplicantCategory.Technologist,
            null,
            "مدیرعامل");
        completed.UpsertApprovalDetail(
            0.31m,
            1.62m,
            24m,
            45_000_000_000m,
            true,
            33_000_000_000m,
            LoanFacilityType.Jaala,
            "ارتقای زیرساخت دیتاسنتر",
            null,
            10_000_000_000m,
            "ده میلیارد ریال",
            6,
            0,
            15m,
            0.05m,
            "تجهیزات موضوع تسهیلات",
            "خانم لیلا طاهری",
            "تسویه کامل انجام شده",
            750_000_000m,
            1_791_666_667m);
        Advance(completed, BuildPathTo(LoanCaseStatus.Completed, a));
        var doneInstallments = BuildInstallments(completed.Id, 10_000_000_000m, 6, 0, 0.15m, today.AddMonths(-14));
        foreach (var item in doneInstallments)
            item.MarkPaid(new DateTimeOffset(item.InstallmentDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        completed.ReplaceInstallments(doneInstallments);
        db.LoanCases.Add(completed);

        // 6) Revision requested by credit
        var revision = Create(
            CaseNumberFormat.Build("LN", day, seq++),
            a.ApplicantIndividual.Id.ToString(),
            ApplicantType.Individual,
            null,
            "تسهیلات قرض‌الحسنه — توسعه کسب‌وکار شخصی سلامت دیجیتال");
        revision.UpsertApplication(
            1_500_000_000m,
            "یک میلیارد و پانصد میلیون ریال",
            "تأمین هزینه توسعه نسخه آزمایشی اپلیکیشن",
            "ضامن معتبر و چک",
            ApplicantCategory.Creative,
            null,
            "متقاضی حقیقی");
        Advance(revision, [
            (LoanCaseStatus.DataEntry, a.ApplicantIndividual.Id.ToString(), UserRoleClaims.Applicant, LoanWorkflowAction.Submit, null),
            (LoanCaseStatus.PendingCreditReview, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert, LoanWorkflowAction.Approve, null)
        ]);
        revision.RequestRevision(
            LoanCaseStatus.RevisionRequestedByCredit,
            a.CreditExpert.Id.ToString(),
            UserRoleClaims.CreditExpert,
            LoanWorkflowAction.RequestRevision,
            Guid.NewGuid(),
            "لطفاً مدارک درآمدی و طرح توجیهی سه‌صفحه‌ای را تکمیل کنید.",
            isInternal: false);
        db.LoanCases.Add(revision);
    }

    private static LoanCase Create(
        string caseNumber,
        string applicantUserId,
        ApplicantType type,
        Guid? companyId,
        string title)
    {
        var entity = new LoanCase(caseNumber, applicantUserId, type);
        entity.SetTitle(title);
        if (companyId.HasValue)
            entity.AssignCompany(companyId.Value);
        return entity;
    }

    private static void Advance(
        LoanCase entity,
        IEnumerable<(LoanCaseStatus Status, string UserId, string Role, LoanWorkflowAction Action, string? Comment)> steps)
    {
        foreach (var step in steps)
            entity.TransitionTo(step.Status, step.UserId, step.Role, step.Action, Guid.NewGuid(), step.Comment);
    }

    private static List<(LoanCaseStatus, string, string, LoanWorkflowAction, string?)> BuildPathTo(
        LoanCaseStatus target,
        IdentitySeeder.DemoActors a)
    {
        var path = new List<(LoanCaseStatus, string, string, LoanWorkflowAction, string?)>
        {
            (LoanCaseStatus.DataEntry, a.ApplicantNavaco.Id.ToString(), UserRoleClaims.Applicant, LoanWorkflowAction.Submit, "ارسال درخواست"),
            (LoanCaseStatus.PendingCreditReview, a.CreditExpert.Id.ToString(), UserRoleClaims.CreditExpert, LoanWorkflowAction.Approve, "بررسی اعتبار"),
            (LoanCaseStatus.PendingCeoInitialApproval, a.Ceo.Id.ToString(), UserRoleClaims.Ceo, LoanWorkflowAction.Approve, "تأیید اولیه مدیرعامل"),
            (LoanCaseStatus.PendingLegalRawContract, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, LoanWorkflowAction.UploadRawContract, "پیش‌نویس قرارداد"),
            (LoanCaseStatus.PendingApplicantSignature, a.ApplicantNavaco.Id.ToString(), UserRoleClaims.Applicant, LoanWorkflowAction.SubmitSignedPackage, "امضای متقاضی"),
            (LoanCaseStatus.PendingLegalFinalReview, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, LoanWorkflowAction.Approve, "بازبینی حقوقی"),
            (LoanCaseStatus.PendingFinancialReview, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, LoanWorkflowAction.SubmitInstallments, "بررسی مالی و جدول اقساط"),
            (LoanCaseStatus.PendingLegalFinalContract, a.LegalExpert.Id.ToString(), UserRoleClaims.LegalExpert, LoanWorkflowAction.UploadFinalContract, "قرارداد نهایی"),
            (LoanCaseStatus.PendingCeoFinalApproval, a.Ceo.Id.ToString(), UserRoleClaims.Ceo, LoanWorkflowAction.Approve, "تأیید نهایی مدیرعامل"),
            (LoanCaseStatus.ReadyForPayment, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, LoanWorkflowAction.Approve, "آماده‌پرداخت"),
            (LoanCaseStatus.RepaymentPhase, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, LoanWorkflowAction.RegisterPayment, "ورود به فاز بازپرداخت"),
            (LoanCaseStatus.Completed, a.FinancialExpert.Id.ToString(), UserRoleClaims.FinancialExpert, LoanWorkflowAction.Approve, "تسویه کامل و مختومه")
        };

        var index = path.FindIndex(x => x.Item1 == target);
        return index < 0 ? path : path.Take(index + 1).ToList();
    }

    private static List<LoanInstallment> BuildInstallments(
        Guid caseId,
        decimal principal,
        int months,
        int grace,
        decimal annualRate,
        DateOnly start)
    {
        var list = new List<LoanInstallment>();
        var totalProfit = Math.Round(principal * annualRate * (months / 12m), 0);
        var principalPer = Math.Round(principal / months, 0);
        var profitPer = Math.Round(totalProfit / months, 0);

        for (var i = 1; i <= months; i++)
        {
            var isGrace = i <= grace;
            var p = isGrace ? 0m : principalPer;
            var profit = profitPer;
            var total = p + profit;
            list.Add(new LoanInstallment(
                caseId,
                i,
                start.AddMonths(i - 1),
                p,
                profit,
                total,
                fundShareOfPrincipal: p,
                fundShareOfProfit: profit,
                fundShareOfTotal: total,
                isGracePeriod: isGrace));
        }

        return list;
    }
}
