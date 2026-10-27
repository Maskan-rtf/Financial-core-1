using System.ComponentModel.DataAnnotations.Schema;
using Core.Domain.Enums;

namespace Core.Domain.Identity.Entities;

[Table("Company", Schema = "Identity")]
public sealed class Company : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string EconomicCode { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string? NationalId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Province { get; set; }
    public string? PostalCode { get; set; }
    public CompanyLegalType? LegalType { get; set; }
    public CompanyTechnologyType? TechnologyType { get; set; }
    public TechnologyDomain? TechnologyDomain { get; set; }
    public string? RepresentativeName { get; set; }
    public string? RepresentativePhone { get; set; }
    public string? CeoName { get; set; }
    public string? CeoPhone { get; set; }

    public List<User> Users { get; set; } = [];
}

