using Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Presentation.Services;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AccountsController(IAccountService accountService, UserManager<UserEntity> userManager, SignInManager<UserEntity> signInManager) : ControllerBase
{
    private readonly IAccountService _accountService = accountService;
    private readonly UserManager<UserEntity> _userManager = userManager;
    private readonly SignInManager<UserEntity> _signInManager = signInManager;




    #region Local SignUp
    

    [HttpPost("signup")]
    public async Task<IActionResult> SignUp([FromBody] SignUpFormData model)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var email = model.Email.Trim().ToLower();

        //Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "An account with this email already exists.");
            return BadRequest(ModelState);
        }
        var userEntity = new UserEntity
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName, 
            LastName = model.LastName    
        };
        //Prepare and call the account service to create the user
        var result = await _userManager.CreateAsync(userEntity, model.Password); 

       
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(userEntity, "User"); // Add to role
                                                                       // Auto-sign-in after local signup
                await _signInManager.SignInAsync(userEntity, isPersistent: false);
                return Ok(new { message = "User signed up and logged in", redirect = "/" });
            }


        // If we can't sign in automatically for some reason, redirect to sign in page
        return BadRequest(new { message = "Sign-up failed", errors = result.Errors.Select(e => e.Description).ToArray() });
    }




    #endregion

    #region SignIn


    [HttpPost("signin")]
    public async Task<IActionResult> SignIn([FromBody] SignInFormData model, string returnUrl = "/")
    {

        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var result = await _accountService.SignInAsync(model);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email!);


            return Ok(new
            {
                message = "Signed in successfully",
                user = new
                {
                    user!.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName
                },
                redirectUrl = returnUrl
            });
        }

        return Unauthorized(new
        {
            message = result.Error ?? "Incorrect email or password."
        });

    }
    #endregion

    #region External Authentication

    [HttpGet("ExternalSignIn")]
    public IActionResult ExternalSignIn(string provider, string returnUrl = null!)
    {
        if (string.IsNullOrEmpty(provider))
        {
            ModelState.AddModelError("", "Invalid Provider");
            return RedirectToAction("SignIn");
        }
        var redirectUrl = Url.Action("ExternalSignInCallBack", "Auth", new { returnUrl })!;
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);

    }
    [HttpGet("signin‑google")]
    public async Task<IActionResult> ExternalSignInCallback(string returnUrl = null!, string remoteError = null!)
    {
        returnUrl ??= Url.Content("~/");

        if (!string.IsNullOrEmpty(remoteError))
        {
            ModelState.AddModelError("", $"Error from external provider: {remoteError}");
           return RedirectToAction("SignIn");
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
            return RedirectToAction("SignIn");

        var signInResult = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (signInResult.Succeeded)
        {
            return LocalRedirect(returnUrl);
        }
        else
        {
            string email = info.Principal.FindFirstValue(ClaimTypes.Email)!;
            string username = $"ext_{info.LoginProvider.ToLower()}_{email}";
            string? firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName); // Can be null
            string? lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);   // Can be null
            string? userImage = info.Principal.FindFirstValue("picture"); // Common claim for Google profile picture



            var user = new UserEntity { UserName = username, Email = email, FirstName = firstName, LastName = lastName };

            var identityResult = await _userManager.CreateAsync(user);
            if (identityResult.Succeeded)
            {
                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }
            foreach (var error in identityResult.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return RedirectToAction("SignIn");
        }
    }

    #endregion

    [HttpGet("Signout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("SignIn", "Auth");
    }
}


