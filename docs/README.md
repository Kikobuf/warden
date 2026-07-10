# Warden

Warden is a game-server administration website: a moderation system, a server fleet
dashboard, and a plugin distribution system, all behind Discord login and a
website-only role/permission system.

It is built with **ASP.NET Core 8 (C#)**, **Entity Framework Core + SQLite**, and
server-rendered **Razor views** styled with Bootstrap 5 in a dark theme.

## What's in this repo

```
Warden.sln
src/Warden.Web/
  Controllers/            MVC controllers (staff-facing website)
  Controllers/Api/        API controllers (called by game servers/plugins)
  Data/                   EF Core DbContext + seeding
  Models/                 Domain entities
  Models/Dtos/            API request/response shapes
  Services/               Auth handlers, moderation logic, plugin storage, permissions
  Views/                  Razor views
  wwwroot/                Static assets (CSS)
docs/
  API.md                  Full API reference for the game-server plugin author
  PERMISSIONS.md           How the website-only permission system works
  DEPLOYMENT.md            Setting up Discord OAuth, running, and deploying
```

## Feature overview

- **Moderation system** — bans, mutes, warnings, and kicks, keyed only by a player
  nickname and user ID (both strings), exactly as required. A server-authenticated API
  lets the game plugin check status and issue/revoke actions; a matching staff UI on the
  website does the same thing through the same service layer, so behavior never diverges.
- **Server management** — each game server registers with Warden and gets an API key.
  It POSTs a heartbeat (player list + TPS) roughly every 30 seconds; Warden tracks
  online/offline status and currently connected players per server.
- **Plugin management** — staff upload plugin DLLs through the website. Plugins can be
  assigned to specific servers or marked "global" (delivered to every server, including
  ones created later). Servers fetch their assigned plugin list and download the DLL
  bytes through an authenticated API, meant to be called on server startup.
- **Website permission system** — roles are fully custom (not hard-coded), each holding
  any combination of permission keys (see `docs/PERMISSIONS.md`). This system only
  controls access to *this website*; it has no bearing on in-game permissions, which the
  plugin decides for itself before calling the moderation API.
- **Audit log** — every moderation action, server change, plugin change, and role/user
  change is recorded with who did it and when, for staff accountability.

## Quick start

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Create a Discord application at <https://discord.com/developers/applications>, add a
   redirect URI of `https://localhost:5443/signin-discord` (and your real domain later),
   and note the Client ID/Secret.
3. Set secrets (don't commit real credentials to `appsettings.json`):
   ```bash
   cd src/Warden.Web
   dotnet user-secrets init
   dotnet user-secrets set "Discord:ClientId" "..."
   dotnet user-secrets set "Discord:ClientSecret" "..."
   ```
4. Put your own Discord user ID in `Warden:BootstrapAdminDiscordIds` in
   `appsettings.json` (or via user-secrets) so your first login gets the Owner role.
5. Run it:
   ```bash
   dotnet run
   ```
   The SQLite database and default roles ("Owner", "Staff") are created automatically on
   first run.
6. Sign in with Discord, then use **Roles** to build out real staff roles, and
   **Servers** to register your first game server and get its API key.

See `docs/DEPLOYMENT.md` for production deployment notes and `docs/API.md` for wiring up
the game-server plugin.

## Design notes

- **Player data minimalism.** Per the spec, `PlayerProfile` stores only a user ID and a
  nickname — nothing else. Every moderation record denormalizes the nickname at the time
  of the action so history reads correctly even after a player renames.
- **Server API keys are hashed, never stored in plaintext.** The plaintext key is shown
  exactly once, at creation or regeneration.
- **Plugin DLLs are stored outside `wwwroot`.** They can only be retrieved through the
  authenticated download endpoint — there is no static-file route that could leak them.
- **Shared service layer.** `ModerationService` is used identically by the API
  controllers (game plugin) and the MVC controllers (website), so a ban issued from
  either place behaves exactly the same way.
- **Website permissions are read fresh from the database on every request** rather than
  cached in the login cookie, so a permission change takes effect immediately without
  forcing the affected user to log out and back in.
