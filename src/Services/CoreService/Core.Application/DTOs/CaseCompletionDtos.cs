using Core.Domain.Enums;

namespace Core.Application.DTOs;

public sealed record CaseFinancialWorksheetDto(
    string BankName,
    string Iban,
    decimal ApprovedAmount,
    string PaymentSchedule,
    string? Notes);

public sealed record CaseValuationDto(
    ValuationType Type,
    decimal Amount,
    string? Notes,
    DateTimeOffset CreatedAt);
