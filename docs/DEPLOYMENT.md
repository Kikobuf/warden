# Deployment guide

## 1. Create the Discord OAuth application

1. Go to <https://discord.com/developers/applications> → **New Application**.
2. Under **OAuth2 → General**, copy the **Client ID** and **Client Secret**.
3. Under **OAuth2 → Redirects**, add:
   - `https://localhost:5443/signin-discord` for local development
   - `https://your-real-domain.com/signin-discord` for production
   (The path must match `Discord:CallbackPath` in configuration — `/signin-discord` by
   default.)

## 2. Configure secrets

Never commit real Client ID/Secret values into `appsettings.json`. Use one of:

**Local development — user-secrets:**
```bash
cd src/Warden.Web
dotnet user-secrets init
dotnet user-secrets set "Discord:ClientId" "your-client-id"
dotnet user-secrets set "Discord:ClientSecret" "your-client-secret"
```

**Production — environment variables** (double underscore separates config sections):
```bash
export Discord__ClientId="your-client-id"
export Discord__ClientSecret="your-client-secret"
export Warden__BootstrapAdminDiscordIds__0="your-discord-user-id"
```

## 3. First run

```bash
cd src/Warden.Web
dotnet restore
dotnet run
```

On first run, Warden:
- Creates `App_Data/warden.db` (SQLite) if it doesn't exist.
- Seeds the built-in **Owner** and **Staff** roles.
- Grants **Owner** automatically to any Discord user ID listed under
  `Warden:BootstrapAdminDiscordIds` the first time they log in.

Sign in with Discord using that bootstrap account, then build out real roles from the
**Roles** page and assign them from the **Users** page — you don't need to keep using the
bootstrap list after that (it's idempotent and harmless to leave in place, but the real
source of truth becomes the Roles/Users pages).

## 4. Registering a game server

From the **Servers** page, click **Add server**, name it, and save — you'll be shown a
plaintext API key exactly once. Put it into your plugin's configuration and see
`docs/API.md` for the endpoints it should call (heartbeat every ~30s, moderation
check/actions, plugin manifest + download on startup).

## 5. Production deployment

Warden is a standard ASP.NET Core app — deploy it however you'd deploy any other:

- **Self-hosted / VPS:** `dotnet publish -c Release -o out`, run
  `dotnet Warden.Web.dll` behind a reverse proxy (nginx/Caddy) that terminates TLS and
  forwards to Kestrel. Make sure the reverse proxy forwards `X-Forwarded-For`/`Proto`
  headers and that `UseForwardedHeaders` is configured if you're behind one (not enabled
  by default in this scaffold — add it in `Program.cs` if needed).
- **Docker:** use the official `mcr.microsoft.com/dotnet/aspnet:8.0` runtime image as your
  base, copy the publish output in, and mount a volume for `App_Data/` so the SQLite
  database and uploaded plugin DLLs persist across container restarts/redeploys.
- **Windows Server / IIS:** publish with the `win-x64` runtime identifier and host under
  IIS with the ASP.NET Core Module, same as any other Kestrel-backed app.

Either way, make sure:
- `App_Data/` (both the SQLite file and `App_Data/PluginStorage/`) is on **persistent**
  storage, not an ephemeral container layer.
- HTTPS is terminated somewhere in the chain — Discord OAuth callback URLs must be HTTPS
  in production.
- The Discord OAuth redirect URI registered on Discord's side matches your real domain
  exactly (scheme, host, and path).

## 6. Schema changes going forward

This scaffold uses `Database.EnsureCreatedAsync()` for simplicity, which creates the
schema from the current model but does **not** support incremental upgrades. Before your
first production deployment with real data, switch to proper EF Core migrations so future
model changes can be applied without losing data:

```bash
cd src/Warden.Web
dotnet tool install --global dotnet-ef   # once
dotnet ef migrations add InitialCreate
```

Then replace the `DbSeeder.SeedAsync` call's `EnsureCreatedAsync()` with
`await db.Database.MigrateAsync();` and commit the generated `Migrations/` folder. Any
future schema change becomes `dotnet ef migrations add <Name>` + redeploy.

## 7. Backups

The entire application state lives in two places: `App_Data/warden.db` (SQLite database)
and `App_Data/PluginStorage/` (uploaded plugin DLLs). Back up both together, on the same
schedule — a database backup without the matching plugin files (or vice versa) can leave
plugin assignments pointing at files that no longer exist.
