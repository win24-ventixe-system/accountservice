using Data.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Presentation.Data;
using Presentation.Services;
using System;
using Microsoft.OpenApi.Models; // Required for OpenApiInfo

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// --- FIX 1 & 2: Remove AddOpenApi(), use standard SwaggerGen for API docs ---
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Account Service API", Version = "v1" });
    
});


// Configure CORS policy - Define it once, use it once.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy => // Named policy
    {
        policy.AllowAnyOrigin() 
              .AllowAnyHeader()
              .AllowAnyMethod();
       
    });
});

builder.Services.AddScoped<IAccountService, AccountService>();

// --- FIX 4: Ensure correct connection string name ---
builder.Services.AddDbContext<DataContext>(x => x.UseSqlServer(builder.Configuration.GetConnectionString("SqlConnection"))); 

builder.Services.AddIdentity<UserEntity, IdentityRole>(x =>
{
    x.SignIn.RequireConfirmedAccount = false;
    x.User.RequireUniqueEmail = true;
    x.Password.RequiredLength = 8;
    x.Password.RequireDigit = false;
    x.Password.RequireNonAlphanumeric = false;
    x.Password.RequireUppercase = false;
    x.Password.RequireLowercase = false;
})
.AddEntityFrameworkStores<DataContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(x =>
{
    x.LoginPath = "/auth/signin";
    x.AccessDeniedPath = "/auth/denied";
    x.Cookie.HttpOnly = true;
    x.Cookie.IsEssential = true;
    x.ExpireTimeSpan = TimeSpan.FromHours(1);
    x.SlidingExpiration = true;
    x.Cookie.SameSite = SameSiteMode.None; // Be cautious with SameSiteMode.None; requires SecurePolicy.Always
    x.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.AddAuthentication(x =>
{
    x.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie()
    .AddGoogle(x =>
    {
        x.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        x.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        x.CallbackPath = "/signin-google";
    });

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => !context.Request.Cookies.ContainsKey("cookieConsent");
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
});

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Account Service API");
    c.RoutePrefix = "swagger"; 
});



app.UseHttpsRedirection();
app.UseStaticFiles(); 
app.UseCookiePolicy(); 

app.UseRouting(); 

app.UseCors("AllowAll"); 

app.UseAuthentication();
app.UseAuthorization();

// ROLE Seeding (This block runs on app startup and is generally okay)
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roleNames = { "Admin", "User" };

    foreach (var roleName in roleNames)
    {
        var roleExists = await roleManager.RoleExistsAsync(roleName);
        if (!roleExists)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
        }
    }

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<UserEntity>>();
    //var user = new UserEntity { UserName = "admin@domain.com", Email = "admin@domain.com" };
    var user = new UserEntity { UserName = "admin@domain.com", Email = "admin@domain.com", UserImage = "/images/logo_img.svg" };


    var userExists = await userManager.Users.AnyAsync(x => x.Email == user.Email);
    if (!userExists)
    {
        var result = await userManager.CreateAsync(user, "ChangeMe123!");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, "Admin");
    }
}

// Map API endpoints
app.MapControllers();


app.Run();