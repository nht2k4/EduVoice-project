using System.Security.Claims;
using AIVES.Business;
using AIVES.Business.Interfaces;
using AIVES.Business.Models;
using AIVES.WebRazor.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection trong appsettings.json");

builder.Services.AddRazorPages(options =>
{
    // Mặc định mọi trang phải đăng nhập; các trang dưới đây là ngoại lệ
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
    options.Conventions.AllowAnonymousToPage("/Auth/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Error");

    options.Conventions.AuthorizeFolder("/Accounts", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Subjects", "AdminOnly");
    options.Conventions.AuthorizeFolder("/MySubjects", "LecturerOnly");
});

// Toàn bộ Business + DataAccess được đăng ký qua 1 dòng này
builder.Services.AddBusinessLayer(connectionString, builder.Configuration.GetSection("Ai").Get<AiOptions>());

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Auth/Login";
        options.LogoutPath = "/Auth/Logout";
        options.AccessDeniedPath = "/Auth/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // Mỗi request kiểm tra lại: tài khoản bị khóa hoặc bị đổi vai trò thì đăng xuất ngay
        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.GetUserId();
            if (userId is null)
            {
                context.RejectPrincipal();
                return;
            }

            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
            var activeRole = await authService.GetActiveRoleAsync(userId.Value);
            var cookieRole = context.Principal!.FindFirst(ClaimTypes.Role)?.Value;

            if (activeRole is null || activeRole != cookieRole)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    options.AddPolicy("LecturerOnly", p => p.RequireRole(Roles.Lecturer));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
