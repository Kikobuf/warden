using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Warden.Web.Data;
using Warden.Web.Models;
using Warden.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- EF Core / SQLite ----------------------------------------------------
builder.Services.AddDbContext<WardenDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("WardenDb")));

// ---- Application services --------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ModerationService>();
builder.Services.AddSingleton<PluginStorageService>();

// ---- Authentication: Cookies (web staff) + Discord OAuth + Server API keys --
var discordClientId = builder.Configuration["Discord:ClientId"] ?? "";
var discordClientSecret = builder.Configuration["Discord:ClientSecret"] ?? "";
var discordCallbackPath = builder.Configuration["Discord:CallbackPath"] ?? "/signin-discord";
var bootstrapAdminIds = builder.Configuration.GetSection("Warden:BootstrapAdminDiscordIds").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Discord";
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/denied";
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;
    })
    .AddOAuth("Discord", options =>
    {
        options.ClientId = discordClientId;
        options.ClientSecret = discordClientSecret;
        options.CallbackPath = discordCallbackPath;

        options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
        options.TokenEndpoint = "https://discord.com/api/oauth2/token";
        options.UserInformationEndpoint = "https://discord.com/api/users/@me";

        options.Scope.Add("identify");
        options.SaveTokens = true;

        options.ClaimActions.MapJsonKey("urn:discord:id", "id");
        options.ClaimActions.MapJsonKey("urn:discord:username", "username");
        options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");

        options.Events = new OAuthEvents
        {
            OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", context.AccessToken);

                using var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                using var user = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                context.RunClaimActions(user.RootElement);

                var discordId = user.RootElement.GetProperty("id").GetString() ?? "";
                context.Identity!.AddClaim(new Claim(WardenClaimTypes.DiscordUserId, discordId));
            },

            OnTicketReceived = async context =>
            {
                var principal = context.Principal!;
                var discordId = principal.FindFirst(WardenClaimTypes.DiscordUserId)!.Value;
                var username = principal.FindFirst("urn:discord:username")?.Value ?? "unknown";
                var avatar = principal.FindFirst("urn:discord:avatar")?.Value;

                var db = context.HttpContext.RequestServices.GetRequiredService<WardenDbContext>();

                var user = await db.WebUsers.FirstOrDefaultAsync(u => u.DiscordUserId == discordId);
                if (user is null)
                {
                    user = new WebUser
                    {
                        DiscordUserId = discordId,
                        Username = username,
                        AvatarHash = avatar,
                        CreatedAt = DateTime.UtcNow,
                        LastLoginAt = DateTime.UtcNow,
                    };
                    db.WebUsers.Add(user);
                }
                else
                {
                    user.Username = username;
                    user.AvatarHash = avatar;
                    user.LastLoginAt = DateTime.UtcNow;
                }

                await db.SaveChangesAsync();

                if (user.IsDisabled)
                {
                    context.Fail("This account has been disabled.");
                    return;
                }

                await DbSeeder.EnsureUserRolesAsync(db, user, bootstrapAdminIds);
            },
        };
    })
    .AddScheme<ServerApiKeyAuthenticationOptions, ServerApiKeyAuthenticationHandler>(
        ServerApiKeyDefaults.AuthenticationScheme, _ => { });

// ---- Authorization: one policy per permission key --------------------------
// Scoped (not Singleton) because it depends on the scoped WardenDbContext.
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthorization(options => options.AddAllPermissionPolicies());

// ---- MVC / Razor ------------------------------------------------------------
builder.Services.AddControllersWithViews();

// ---- File upload size limit for plugin DLLs (100 MB) -----------------------
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WardenDbContext>();
    await DbSeeder.SeedAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
