using main.Components;
using main.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.SignalR;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<DeclarationStore>();
builder.Services.AddSingleton<UserAccountStore>();
builder.Services.AddSingleton<AgentStore>();

builder.Services.Configure<HubOptions>(options =>
{
     // 40 MB Maximal message size is needed to support the largest video files that can be uploaded
    options.MaximumReceiveMessageSize = 40 * 1024 * 1024;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.DashboardAccess, policy => policy.RequireRole(Roles.DashboardAccess));
});

builder.Services.AddCascadingAuthenticationState();
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Rewrite error status codes to the dedicated not-found page
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Middleware order is significant
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Serve static assets from the build manifest and map the interactive Blazor Components Hub
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();