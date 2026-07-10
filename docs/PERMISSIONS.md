# Website permission system

This document covers the **website-only** permission system: who can do what on the
Warden site itself. It has nothing to do with in-game permissions — the game plugin
decides who can run in-game commands (ban/mute/etc.) using its own logic, then calls the
Moderation API with the server's API key, not a per-staff-member web credential.

## Model

```
WebUser  ──< UserRoleAssignment >──  WebRole  ──< RolePermission
```

- A **WebUser** is created automatically the first time someone signs in with Discord.
- A **WebRole** is a named bundle of permissions (e.g. "Moderator", "Server Admin"). Roles
  are entirely custom — create as many as you want, named however you want, with whatever
  permission combination makes sense for your team.
- A **RolePermission** is one permission key granted to one role (see the full list
  below). A user's *effective* permissions are the union of every permission across every
  role they hold.

Two roles are built in and cannot be deleted:

- **Owner** — implicitly has *every* permission, including ones added in the future. Use
  this sparingly; it's meant for the person(s) ultimately responsible for the site.
- **Staff** (the default role) — automatically granted to every user on login. It starts
  with zero permissions, so a brand-new login can see they're signed in but can't do
  anything until an Owner grants them real roles. This means the site is "safe by
  default" — nobody accidentally gets access they shouldn't.

The very first Owner is bootstrapped via `Warden:BootstrapAdminDiscordIds` in
`appsettings.json` — list your own Discord user ID there before your first login. After
that, manage everyone else from the **Users** and **Roles** pages.

## Full permission list

| Key                     | Grants                                                          |
|--------------------------|------------------------------------------------------------------|
| `dashboard.view`         | View the dashboard                                               |
| `servers.view`           | View servers and their live status                               |
| `servers.manage`         | Create/edit/delete servers, regenerate API keys                  |
| `plugins.view`           | View installed plugins                                           |
| `plugins.manage`         | Upload, delete, and assign plugins to servers                     |
| `moderation.view`        | View player moderation history                                   |
| `moderation.ban`         | Issue bans                                                        |
| `moderation.mute`        | Issue mutes                                                       |
| `moderation.warn`        | Issue warnings                                                    |
| `moderation.kick`        | Log kicks (the plugin still performs the actual kick)             |
| `moderation.revoke`      | Revoke (unban/unmute) existing actions                            |
| `roles.manage`           | Create/edit/delete roles and their permissions                    |
| `users.manage`           | Assign roles to web users, disable accounts                       |
| `auditlog.view`          | View the audit log                                                |

This list lives in code at `Models/Permissions.cs` — add a new constant there (and a
`PermissionDefinition` entry in the relevant group) if you add a new protected feature,
and it will automatically show up as a checkbox on the Roles page.

## How enforcement works

Every permission key becomes an ASP.NET Core **authorization policy** of the same name
(wired up once in `Program.cs` via `AddAllPermissionPolicies()`). Controllers/actions are
then annotated with `[Authorize(Policy = Permissions.ModerationBan)]`, etc.

`PermissionAuthorizationHandler` checks the requirement by loading the signed-in user's
roles (and each role's permissions) **fresh from the database on every request** — it is
deliberately not cached in the login cookie. That means:

- Revoking a permission takes effect on the user's very next request — no need to force a
  re-login.
- It costs one extra (indexed, cheap) database query per authorization check, which is a
  reasonable trade for a small admin team; if you ever needed to scale this to a very
  large staff roster, moving the permission set into signed claims refreshed periodically
  would be the next optimization.

Razor views also call `CurrentUserService.GetEffectivePermissionsAsync()` to decide which
buttons/nav links to show — this is a **UI convenience only**; the real enforcement is
always the `[Authorize(Policy = ...)]` attribute on the controller action. Never rely on
hiding a button as the only protection for an action.

## Disabling an account

Toggling a user to "Disabled" on the **Users** page blocks their next login attempt
(`OnTicketReceived` checks `IsDisabled` and fails the sign-in) — it does not end an
already-active session early. If you need to cut off an active session immediately,
combine disabling the account with rotating your cookie's data-protection keys, or wait
for their session to expire (14 days by default, see `Program.cs`).
