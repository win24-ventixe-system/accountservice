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
    public async Task<IActionResult> SignUp([FromBody] SignUpFormData model, string returnUrl = "/")
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
        //Prepare and call the account service to create the user
        var result = await _accountService.SignUpAsync(model);

        if (result.Succeeded)
        {
            //Get the newly created user and sign them in automatically
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                // Confirm email and update
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);  // Update the user in the database


                // Automatically sign in the user
                await _signInManager.SignInAsync(user, isPersistent: false);
                return Ok(new { message = "User signed up and logged in", redirect = returnUrl });
            }

            // If we can't sign in automatically for some reason, redirect to sign in page
            return RedirectToAction("SignIn", "Auth");
        }

        // If sign-up failed, return errors
        return BadRequest(new { message = "Sign-up failed" });
    }
    

    #endregion

    #region SignIn
   

    [HttpPost("signin")]
    public async Task<IActionResult> SignIn([FromBody] SignInFormData model, string returnUrl = "/")
    {

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        //var signInFormData = new SignInFormData
        //{
        //    Email = model.Email,
        //    Password = model.Password,
        //    IsPersistent = model.IsPersistent
        //};


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

    [HttpGet]
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
            string firstName = string.Empty;
            string lastName = string.Empty;
            try
            {
                firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName)!;
                lastName = info.Principal.FindFirstValue(ClaimTypes.Surname)!;
            }
            catch { }

            string email = info.Principal.FindFirstValue(ClaimTypes.Email)!;
            string username = $"ext_{info.LoginProvider.ToLower()}_{email}";


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

    [HttpGet]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("SignIn", "Auth");
    }
}


