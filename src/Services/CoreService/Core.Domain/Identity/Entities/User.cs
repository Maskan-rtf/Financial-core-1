using System.ComponentModel.DataAnnotations.Schema;
using Core.Domain.Identity;

namespace Core.Domain.Identity.Entities;
[Table("User", Schema = "Identity")]
public class User : BaseEntity
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? NationalCode { get; set; }
    public string? PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsPhoneVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
   
}
