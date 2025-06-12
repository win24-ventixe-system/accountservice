using Data.Entities;
using Microsoft.AspNetCore.Identity;
using Presentation.Models;
using System.Security.Claims;

namespace Presentation.Services;

public class AccountService(UserManager<UserEntity> userManager, RoleManager<IdentityRole> roleManager, SignInManager<UserEntity> signInManager) : IAccountService
{
    private readonly SignInManager<UserEntity> _signInManager = signInManager;
    private readonly UserManager<UserEntity> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;



    public async Task<AuthResult> SignInAsync(SignInFormData formData)
    {
        if (formData == null)
            return new AuthResult { Succeeded = false, StatusCode = 400, Error = "Not all required fields are supplied." };

        var user = await _userManager.FindByEmailAsync(formData.Email!);
        if (user == null)
        {
            return new AuthResult { Succeeded = false, StatusCode = 401, Error = "Invalid Email or password." };
        }

        var result = await _signInManager.PasswordSignInAsync(user, formData.Password!, formData.IsPersistent, lockoutOnFailure: false);


        if (!result.Succeeded)
        {
            string errorMessage = "Invalid Email or password.";
            if (result.IsLockedOut)
                errorMessage = "Account is locked out.";
            else if (result.IsNotAllowed)
                errorMessage = "Account is not allowed to sign in.";
            else if (result.RequiresTwoFactor)
                errorMessage = "Two-factor authentication required.";
            return new AuthResult { Succeeded = false, StatusCode = 401, Error = errorMessage };
        }
        if (result.Succeeded)
        {

            
            var roles = await _userManager.GetRolesAsync(user);

            string displayName = $"{user.UserName}";
            string displayRole = string.Join(", ", roles);
            string displayImage = "";

            // Call AddClaimByEmailAsync to add the claims
            await AddClaimByEmailAsync(user.Email!, "DisplayName", displayName, "", "");
            await AddClaimByEmailAsync(user.Email!, "DisplayRole", displayRole, "", "");
            if (!string.IsNullOrEmpty(displayImage))
            {
                await AddClaimByEmailAsync(user.Email!, "image", displayImage, "", "");
            }

        }


        return new AuthResult { Succeeded = true, StatusCode = 200 };

    }
    // Generic helper for adding/updating claims

    public async Task AddClaimByEmailAsync(string email, string typeName, string value, string typeRole, string typeImage)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return;

        var claims = await _userManager.GetClaimsAsync(user);

            // Add DisplayName claim 
            if (!claims.Any(x => x.Type == typeName && x.Value == value))
            {
                await _userManager.AddClaimsAsync(user, new List<Claim> { new Claim(typeName, value) });
            }
            // Add Image claim 
            //if (typeName.ToLower() == "image" && !claims.Any(x => x.Type == typeName && x.Value == value)) // Check typeName for "image"

            //{
            //    await _userManager.AddClaimsAsync(user, new List<Claim> { new Claim(typeImage, value) });
            //}

            // Add Role claim 
            if (typeName.ToLower() == "displayrole" && !claims.Any(x => x.Type == "DisplayRole")) 
            {
                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Any())
                {
                    var displayRole = string.Join(", ", roles);

                    await _userManager.AddClaimsAsync(user, new List<Claim> { new Claim("DisplayRole", displayRole) });
                }
            }
        

    }
    public async Task<AuthResult> SignUpAsync(SignUpFormData formData)
    {
        if (formData == null)
            return new AuthResult { Succeeded = false, StatusCode = 400, Error = "Not all required fields are supplied." };
        var email = formData.Email.Trim().ToLower();

        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
            return new AuthResult { Succeeded = false, StatusCode = 400, Error = "An account with this email already exists." };

        var userEntity = new UserEntity
        {
            UserName = formData.Email,
            Email = formData.Email,

        };

        var result = await _userManager.CreateAsync(userEntity, formData.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(userEntity, "User");
            return new AuthResult { Succeeded = true, StatusCode = 201 };
        }
        return new AuthResult { Succeeded = false, StatusCode = 500, Error = "User could not be created. Please try again later."};
    }


    public async Task<AuthResult> SignOutAsync()
    {
        await _signInManager.SignOutAsync();
        return new AuthResult { Succeeded = true, StatusCode = 200 };
    }


}
