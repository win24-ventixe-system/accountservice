using Microsoft.AspNetCore.Identity;
using System.Security.Principal;

namespace Data.Entities;

// With identity: Id, Email, PasswordHash, UserName, etc.

// Compatibility with Identity framework(SignInManager, UserManager, etc.)

public class UserEntity : IdentityUser
{
    public string? UserImage { get; set; }

    [ProtectedPersonalData]
    public string? FirstName { get; set; }

    [ProtectedPersonalData]
    public string? LastName { get; set; }

    public string? Role { get; set; }


}
