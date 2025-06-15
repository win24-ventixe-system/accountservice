using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Presentation.Helpers;

public class GenerateJwtToken(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;

    public string CreateJwtToken(IEnumerable<Claim> claims)
    {
        // JWT settings from appsettings.json
        var jwtKey = _configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key not found in configuration.");
        var jwtIssuer = _configuration["Jwt:Issuer"] ?? throw new ArgumentNullException("JWT Issuer not found in configuration.");
        var jwtAudience = _configuration["Jwt:Audience"] ?? throw new ArgumentNullException("JWT Audience not found in configuration.");
        var jwtExpireMinutesString = _configuration["Jwt:ExpireMinutes"] ?? "60";
        var jwtExpireMinutes = Convert.ToDouble(jwtExpireMinutesString);


        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(jwtExpireMinutes), // Use UtcNow for consistency
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

}
