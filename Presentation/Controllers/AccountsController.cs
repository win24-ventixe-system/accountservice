using Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Presentation.Services;
using System.Security.Claims;
using Presentation.Helpers;


namespace Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AccountsController(IAccountService accountService, UserManager<UserEntity> userManager, SignInManager<UserEntity> signInManager, GenerateJwtToken generateJwtToken, IVerificationService verificationService) : ControllerBase
{
    private readonly IAccountService _accountService = accountService;
    private readonly UserManager<UserEntity> _userManager = userManager;
    private readonly SignInManager<UserEntity> _signInManager = signInManager;
    private readonly GenerateJwtToken _generateJwtToken = generateJwtToken;
    private readonly IVerificationService _verificationService = verificationService;




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
            LastName = model.LastName,
            EmailConfirmed = false // Set to false initially, will be confirmed by verification
        };
        //Prepare and call the account service to create the user
        var result = await _userManager.CreateAsync(userEntity, model.Password);


        if (result.Succeeded)
        {

            await _userManager.AddToRoleAsync(userEntity, "User"); // Add to role
                                                                   // Auto-sign-in after local signup

            var sendResult = await _verificationService.SenderVerificationCodeAsync(new SendVerificationCodeRequest
            {
                Email = userEntity.Email! // Pass  user's email to service
            });

            if (!sendResult.Succeeded)
            {
                await _userManager.DeleteAsync(userEntity);
                return StatusCode(500, new { error = "User created, but failed to send verification email. Please try again." });
            }

            await _signInManager.SignInAsync(userEntity, isPersistent: false);

            // Generate and return a token here too to auto-login

            var claims = await _userManager.GetClaimsAsync(userEntity); // Get claims for the new user
            var jwtToken = _generateJwtToken.CreateJwtToken(claims);
            return Ok(new { message = "User signed up and logged in", token = jwtToken, redirect = "/" });
        }


        // If we can't sign in automatically for some reason, redirect to sign in page
        return BadRequest(new { message = "Sign-up failed", errors = result.Errors.Select(e => e.Description).ToArray() });
    }




    #endregion

    #region SignIn


    [HttpPost("signin")]
    public async Task<IActionResult> SignIn([FromBody] SignInFormData model)
    {

        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var result = await _accountService.SignInAsync(model);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email!);

            if (user == null)
            {
                return Unauthorized(new { message = "User not found after successful sign-in attempt." });
            }
            // Prepare claims for the JWT
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email!),
                // Add roles:
                // var roles = await _userManager.GetRolesAsync(user);
                // foreach (var role in roles)
                // {
                //     claims.Add(new Claim(ClaimTypes.Role, role));
                // }
            };

            // NEW: Generate the JWT token string
            var jwtToken = _generateJwtToken.CreateJwtToken(claims);

            return Ok(new
            {
                message = "Signed in successfully",
                token = jwtToken,
                user = new
                {
                    user!.Id,
                    user.Email,
                    user.FirstName,
                    user.LastName
                },
                redirectUrl = "/"
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
            // For external logins, return a JWT for consistency
            var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
            var claims = await _userManager.GetClaimsAsync(user!);
            var jwtToken = _generateJwtToken.CreateJwtToken(claims); // Generate token for external user

            return Ok(new { message = "Signed in successfully via Google", token = jwtToken, redirectUrl = returnUrl });

        }
        else
        {
            string email = info.Principal.FindFirstValue(ClaimTypes.Email)!;
            string username = $"ext_{info.LoginProvider.ToLower()}_{email}";
            string? firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName); // Can be null
            string? lastName = info.Principal.FindFirstValue(ClaimTypes.Surname);   // Can be null
            string? userImage = info.Principal.FindFirstValue("picture");



            var user = new UserEntity { UserName = username, Email = email, FirstName = firstName, LastName = lastName };

            var identityResult = await _userManager.CreateAsync(user);
            if (identityResult.Succeeded)
            {
                await _userManager.AddLoginAsync(user, info);
                await _signInManager.SignInAsync(user, isPersistent: false);

                // Generate token for newly created external user
                var claims = await _userManager.GetClaimsAsync(user);
                var jwtToken = _generateJwtToken.CreateJwtToken(claims);
                return Ok(new { message = "User signed up and logged in via Google", token = jwtToken, redirectUrl = returnUrl });
            }
            foreach (var error in identityResult.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return RedirectToAction("SignIn");
        }
    }

    #endregion

    [HttpPost("signout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("SignIn", "Auth");
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send(SendVerificationCodeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Error = "Recipient email address is required" });

        var result = await _verificationService.SenderVerificationCodeAsync(request);
        return result.Succeeded ? Ok(result) : StatusCode(500, result);
    }

    [HttpPost("verify-email")]
    public IActionResult Verifiy(VerifyVerificationCodeRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { Error = "Invalid or expired code." });

        var result = _verificationService.VerifyVerificationCode(request);
        return result.Succeeded ? Ok(result) : StatusCode(500, result);
    }
}


