using Core.Application.Abstractions;
using Core.Application.Authorization;
using Core.Application.DTOs;
using Core.Application.Kanban;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Enums;
using Core.Domain.Identity.Entities;
using MapsterMapper;

namespace Core.Application.Mappers;

public interface ICaseDtoMapper
{
    CompanyDto? MapCompany(Company? company);
    ApplicantContactDto? MapApplicantContact(User user);
    CasePaymentsDto MapPayments(InvestmentCase entity);
    CaseDocumentTypeVersionsDto MapDocumentTypeVersions(
        DocumentType documentType,
        IReadOnlyList<CaseDocumentDto> versions);
    CaseDocumentDto MapDocument(InvestmentCaseDocument document, string? uploadedByFullName = null);
    InvestmentCaseDto MapCase(
        InvestmentCase entity,
        DateTimeOffset now,
        bool isInternalView,
        Company? companyOverride = null,
        ApplicantContactDto? applicantContact = null,
        string? applicantFullName = null,
        string? applicantPhoneNumber = null);
    InvestmentCaseDto MapFromListProjection(
        InvestmentCaseListProjection projection,
        DateTimeOffset now,
        bool isInternalView,
        CaseFinancialWorksheetDto? financialWorksheet = null,
        IReadOnlyList<CaseValuationDto>? valuations = null);
    InvestmentCaseDto MapFromDetailProjection(
        InvestmentCaseListProjection projection,
        DateTimeOffset now,
        bool isInternalView,
        ApplicantContactDto? applicantContact = null,
        CaseFinancialWorksheetDto? financialWorksheet = null,
        IReadOnlyList<CaseValuationDto>? valuations = null);
    CaseCommentDto MapComment(
        InvestmentCaseComment comment,
        string? senderFullName = null,
        IReadOnlyList<InvestmentCaseWorkflowHistory>? workflowHistory = null);
    CaseWorkflowHistoryDto MapHistory(InvestmentCaseWorkflowHistory history, string? changedByFullName = null);
    CaseEvaluationDto MapEvaluation(InvestmentCaseEvaluation evaluation);
}

public sealed class CaseDtoMapper(
    IMapper mapper,
    ICaseAuthorizationService authorizationService,
    ICompanyDtoMapper companyDtoMapper) : ICaseDtoMapper
{
    public CompanyDto? MapCompany(Company? company)
        => companyDtoMapper.Map(company);

    public ApplicantContactDto? MapApplicantContact(User user)
        => new(
            $"{user.FirstName} {user.LastName}".Trim(),
            user.Email?.Trim() ?? "",
            user.PhoneNumber);

    public CasePaymentsDto MapPayments(InvestmentCase entity)
    {
        var approved = entity.FinancialWorksheet?.ApprovedAmount;
        var payments = entity.Payments
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedAt)
            .Select(p => new PaymentRecordDto(
                p.Id,
                p.Amount,
                p.PaymentDate,
                p.TransactionNumber,
                p.ReceiptS3Key,
                p.Notes,
                p.Method,
                p.Status,
                p.CreatedAt,
                p.CreatedByUserId))
            .ToList();

        var totalRecorded = payments
            .Where(p => p.Status != PaymentStatus.Cancelled)
            .Sum(p => p.Amount);

        var totalConfirmed = payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .Sum(p => p.Amount);

        var remaining = approved is > 0
            ? Math.Max(0m, approved.Value - totalConfirmed)
            : 0m;

        return new CasePaymentsDto(
            payments,
            new CasePaymentsSummaryDto(approved, totalRecorded, totalConfirmed, remaining));
    }

    public CaseDocumentTypeVersionsDto MapDocumentTypeVersions(
        DocumentType documentType,
        IReadOnlyList<CaseDocumentDto> versions)
        => new(documentType, versions.FirstOrDefault(), versions);

    public CaseDocumentDto MapDocument(InvestmentCaseDocument document, string? uploadedByFullName = null)
    {
        if (authorizationService.IsInternalUser)
        {
            var mapped = mapper.Map<CaseDocumentInternalDto>(document);
            return mapped with { UploadedByFullName = uploadedByFullName };
        }

        return mapper.Map<CaseDocumentApplicantDto>(document);
    }

    public InvestmentCaseDto MapCase(
        InvestmentCase entity,
        DateTimeOffset now,
        bool isInternalView,
        Company? companyOverride = null,
        ApplicantContactDto? applicantContact = null,
        string? applicantFullName = null,
        string? applicantPhoneNumber = null)
    {
        var company = MapCompany(companyOverride ?? entity.ApplicantCompany);

        if (isInternalView)
        {
            return new InvestmentCaseInternalDto(
                entity.Id,
                entity.CaseNumber,
                entity.Title,
                entity.ApplicantUserId,
                applicantFullName,
                applicantPhoneNumber,
                entity.ApplicantType,
                entity.CurrentPhase,
                entity.CurrentStatus,
                entity.WorkflowInstanceId,
                entity.CreatedAt,
                entity.UpdatedAt ?? now,
                entity.CompletedAt,
                company,
                MapDataEntry1(entity.ApplicantProfile),
                MapDataEntry2(entity.AttractionBasis),
                MapFinancialWorksheet(entity.FinancialWorksheet),
                MapValuations(entity.Valuations));
        }

        var applicant = mapper.Map<InvestmentCaseApplicantDto>(entity);
        return applicant with
        {
            UpdatedAt = applicant.UpdatedAt ?? now,
            Company = company,
            Applicant = applicantContact,
            ApplicantProfile = MapDataEntry1(entity.ApplicantProfile),
            AttractionBasis = MapDataEntry2(entity.AttractionBasis),
            FinancialWorksheet = MapFinancialWorksheet(entity.FinancialWorksheet),
            Valuations = MapValuations(entity.Valuations)
        };
    }

    public InvestmentCaseDto MapFromDetailProjection(
        InvestmentCaseListProjection projection,
        DateTimeOffset now,
        bool isInternalView,
        ApplicantContactDto? applicantContact = null,
        CaseFinancialWorksheetDto? financialWorksheet = null,
        IReadOnlyList<CaseValuationDto>? valuations = null)
    {
        if (!isInternalView)
        {
            var applicantDto = (InvestmentCaseApplicantDto)MapFromListProjection(
                projection, now, isInternalView: false, financialWorksheet, valuations);
            return applicantDto with { Applicant = applicantContact };
        }

        return MapFromListProjection(projection, now, isInternalView: true, financialWorksheet, valuations);
    }

    public InvestmentCaseDto MapFromListProjection(
        InvestmentCaseListProjection projection,
        DateTimeOffset now,
        bool isInternalView,
        CaseFinancialWorksheetDto? financialWorksheet = null,
        IReadOnlyList<CaseValuationDto>? valuations = null)
    {
        var company = companyDtoMapper.MapFlat(
            projection.CompanyId,
            projection.CompanyName,
            projection.CompanyEconomicCode,
            projection.CompanyRegistrationNumber,
            projection.CompanyNationalId,
            projection.CompanyPhoneNumber,
            projection.CompanyAddress,
            projection.CompanyCity,
            projection.CompanyProvince,
            projection.CompanyPostalCode);

        var applicantProfile = string.IsNullOrWhiteSpace(projection.RepresentativeFullName)
            || projection.BusinessStage is null
            || string.IsNullOrWhiteSpace(projection.ContactEmail)
            || projection.RequestedAmount is null
            ? null
            : new DataEntry1Dto(
                projection.RepresentativeFullName,
                projection.BusinessStage.Value,
                projection.ContactEmail,
                projection.RequestedAmount.Value);

        var attractionBasis = string.IsNullOrWhiteSpace(projection.InvestmentAttractionBasis)
            ? null
            : new DataEntry2Dto(projection.InvestmentAttractionBasis);

        if (isInternalView)
        {
            return new InvestmentCaseInternalDto(
                projection.Id,
                projection.CaseNumber,
                projection.Title,
                projection.ApplicantUserId,
                projection.ApplicantFullName,
                projection.ApplicantPhoneNumber,
                projection.ApplicantType,
                projection.CurrentPhase,
                projection.CurrentStatus,
                projection.WorkflowInstanceId,
                projection.CreatedAt,
                projection.UpdatedAt ?? now,
                projection.CompletedAt,
                company,
                applicantProfile,
                attractionBasis,
                financialWorksheet,
                valuations);
        }

        return new InvestmentCaseApplicantDto(
            projection.Id,
            projection.CaseNumber,
            projection.Title,
            projection.ApplicantType,
            projection.CurrentPhase,
            projection.CurrentStatus,
            projection.CreatedAt,
            projection.UpdatedAt ?? now,
            projection.CompletedAt,
            company,
            ApplicantProfile: applicantProfile,
            AttractionBasis: attractionBasis,
            FinancialWorksheet: financialWorksheet,
            Valuations: valuations);
    }

    private static CaseFinancialWorksheetDto? MapFinancialWorksheet(InvestmentCaseFinancialWorksheet? worksheet)
        => worksheet is null
            ? null
            : new CaseFinancialWorksheetDto(
                worksheet.BankName,
                worksheet.Iban,
                worksheet.ApprovedAmount,
                worksheet.PaymentSchedule,
                worksheet.Notes);

    private static IReadOnlyList<CaseValuationDto>? MapValuations(IEnumerable<InvestmentCaseValuation>? valuations)
    {
        if (valuations is null)
            return null;

        var items = valuations
            .OrderBy(v => v.CreatedAt)
            .Select(v => new CaseValuationDto(
                v.Type,
                v.Amount,
                string.IsNullOrWhiteSpace(v.Notes) ? null : v.Notes,
                v.CreatedAt))
            .ToList();

        return items.Count == 0 ? null : items;
    }

    private static DataEntry1Dto? MapDataEntry1(InvestmentCaseApplicantProfile? dataEntry)
        => dataEntry is null
            ? null
            : new DataEntry1Dto(
                dataEntry.RepresentativeFullName,
                dataEntry.BusinessStage,
                dataEntry.ContactEmail,
                dataEntry.RequestedAmount);

    private static DataEntry2Dto? MapDataEntry2(InvestmentCaseAttractionBasis? dataEntry)
        => dataEntry is null
            ? null
            : new DataEntry2Dto(dataEntry.InvestmentAttractionBasis);

    public CaseCommentDto MapComment(
        InvestmentCaseComment comment,
        string? senderFullName = null,
        IReadOnlyList<InvestmentCaseWorkflowHistory>? workflowHistory = null)
    {
        var attachments = comment.Attachments.Select(MapCommentAttachment).ToArray();
        var (workflowStatus, workflowStatusLabel) = ResolveInvestmentWorkflowStatus(comment, workflowHistory);

        if (authorizationService.IsInternalUser)
        {
            return new CaseCommentInternalDto(
                comment.Id,
                comment.CaseId,
                comment.Phase,
                comment.SenderUserId,
                senderFullName,
                comment.SenderRole,
                comment.Message,
                comment.IsRevisionRequest,
                comment.IsInternal,
                comment.ParentId,
                attachments,
                comment.CreatedAt,
                workflowStatus,
                workflowStatusLabel);
        }

        return new CaseCommentApplicantDto(
            comment.Id,
            comment.CaseId,
            comment.Phase,
            comment.Message,
            comment.IsRevisionRequest,
            comment.ParentId,
            attachments,
            comment.CreatedAt,
            workflowStatus,
            workflowStatusLabel);
    }

    private static (CaseStatus? Status, string? Label) ResolveInvestmentWorkflowStatus(
        InvestmentCaseComment comment,
        IReadOnlyList<InvestmentCaseWorkflowHistory>? workflowHistory)
    {
        var snapshots = workflowHistory?
            .Select(h => new WorkflowHistorySnapshot<CaseStatus>(h.CreatedAt, h.FromStatus, h.ToStatus, h.Comment))
            .ToArray();

        var resolved = CaseCommentWorkflowStatusResolver.ResolveFromHistory(
            comment.WorkflowStatusAtCreation.HasValue ? (int)comment.WorkflowStatusAtCreation.Value : null,
            comment.CreatedAt,
            comment.Message,
            comment.IsRevisionRequest,
            snapshots);

        if (!resolved.HasValue)
            return (null, null);

        var status = (CaseStatus)resolved.Value;
        return (status, CaseKanbanRules.GetStatusTitle(status));
    }

    private CaseCommentAttachmentDto MapCommentAttachment(InvestmentCaseCommentAttachment attachment)
        => authorizationService.IsInternalUser
            ? new CaseCommentAttachmentInternalDto(attachment.Id, attachment.S3Key, attachment.FileName)
            : new CaseCommentAttachmentApplicantDto(attachment.Id, attachment.FileName);

    public CaseWorkflowHistoryDto MapHistory(InvestmentCaseWorkflowHistory history, string? changedByFullName = null)
    {
        if (authorizationService.IsInternalUser)
        {
            var mapped = mapper.Map<CaseWorkflowHistoryInternalDto>(history);
            return mapped with { ChangedByFullName = changedByFullName };
        }

        return mapper.Map<CaseWorkflowHistoryApplicantDto>(history);
    }

    public CaseEvaluationDto MapEvaluation(InvestmentCaseEvaluation evaluation)
        => mapper.Map<CaseEvaluationDto>(evaluation);
}
