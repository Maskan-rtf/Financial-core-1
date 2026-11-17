using Core.Application.DTOs;
using Core.Application.Identity.DTOs.User;
using Core.Application.Requests;
using Core.Domain.Entities;
using Core.Domain.Identity.Entities;
using Mapster;

namespace Core.Application;

public sealed class ApplicationMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Company, CompanyDto>()
            .Map(dest => dest.HasLogo, src => !string.IsNullOrWhiteSpace(src.LogoS3Key));

        config.NewConfig<InvestmentCaseDocument, CaseDocumentApplicantDto>();
        config.NewConfig<InvestmentCaseDocument, CaseDocumentInternalDto>();

        config.NewConfig<InvestmentCaseApplicantProfile, DataEntry1Dto>();
        config.NewConfig<InvestmentCaseAttractionBasis, DataEntry2Dto>();

        config.NewConfig<InvestmentCase, InvestmentCaseApplicantDto>()
            .Map(dest => dest.Company, src => src.ApplicantCompany)
            .Map(dest => dest.ApplicantProfile, src => src.ApplicantProfile != null ? src.ApplicantProfile.Adapt<DataEntry1Dto>() : null)
            .Map(dest => dest.AttractionBasis, src => src.AttractionBasis != null ? src.AttractionBasis.Adapt<DataEntry2Dto>() : null);
        config.NewConfig<InvestmentCase, InvestmentCaseInternalDto>()
            .Map(dest => dest.Company, src => src.ApplicantCompany);

        config.NewConfig<InvestmentCaseCommentAttachment, CaseCommentAttachmentApplicantDto>();
        config.NewConfig<InvestmentCaseCommentAttachment, CaseCommentAttachmentInternalDto>();

        config.NewConfig<InvestmentCaseComment, CaseCommentApplicantDto>()
            .Ignore(dest => dest.Attachments);
        config.NewConfig<InvestmentCaseComment, CaseCommentInternalDto>()
            .Ignore(dest => dest.Attachments);

        config.NewConfig<InvestmentCaseWorkflowHistory, CaseWorkflowHistoryApplicantDto>();
        config.NewConfig<InvestmentCaseWorkflowHistory, CaseWorkflowHistoryInternalDto>();

        config.NewConfig<InvestmentCaseEvaluationItem, CaseEvaluationItemDto>();
        config.NewConfig<InvestmentCaseEvaluation, CaseEvaluationDto>()
            .Map(dest => dest.Items, src => src.Items);

        config.NewConfig<User, UserDto>()
            .Map(dest => dest.RoleNumber, src => (int)src.Role)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt);
    }
}
