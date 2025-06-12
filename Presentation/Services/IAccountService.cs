using Presentation.Models;

namespace Presentation.Services;

public interface IAccountService
{
    Task AddClaimByEmailAsync(string email, string typeName, string value, string typeRole, string typeImage);
    Task<AuthResult> SignInAsync(SignInFormData formData);
    Task<AuthResult> SignOutAsync();
    Task<AuthResult> SignUpAsync(SignUpFormData formData);
}