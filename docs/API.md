# Warden API reference

Two families of endpoints exist:

- **Server-to-server APIs** (`/api/v1/moderation/*`, `/api/v1/servers/*`) — called by the
  in-game plugin. Authenticated with a per-server API key.
- **The website itself** — regular cookie-authenticated Razor pages, not covered here.

All request/response bodies are JSON. All timestamps are UTC, ISO-8601.

## Authentication

Every server API call must include the server's API key in a header:

```
X-Server-Api-Key: wsk_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

Keys are created (and re-generated) from the **Servers** page in the website. The
plaintext key is shown exactly once — store it in your plugin's config immediately.
There is no way to retrieve a lost key; regenerate instead (this invalidates the old one).

A request with a missing, malformed, or unrecognized key gets `401 Unauthorized`.

---

## Moderation API

Base path: `/api/v1/moderation`

Only a player's **user ID** and **nickname** are ever stored (both strings) — there is no
other player data model. Reasons, timestamps, and who issued an action are stored on the
moderation record itself, not on the player.

### `GET /api/v1/moderation/status/{userId}`

Check whether a player is currently banned and/or muted. Call this when a player joins,
and optionally on an interval while they're connected.

**Response `200`:**
```json
{
  "isBanned": true,
  "banReason": "Cheating (fly hack)",
  "banExpiresAt": null,
  "bannedBy": "Someone",
  "isMuted": false,
  "muteReason": null,
  "muteExpiresAt": null,
  "mutedBy": null
}
```
`banExpiresAt`/`muteExpiresAt` are `null` for a permanent ban/mute, and for players with
no active ban/mute both `isBanned`/`isMuted` are `false`.

### `POST /api/v1/moderation/ban`

```json
{
  "userId": "76561198000000000",
  "nickname": "Kikobuf",
  "reason": "Cheating (fly hack)",
  "issuedBy": "Someone",
  "durationSeconds": null
}
```
`durationSeconds` omitted or `null` means a **permanent** ban. `issuedBy` is a free-text
identifier (in-game admin name/ID) — the plugin decides who's allowed to do this using
its own in-game permission system, not Warden's website permissions.

**Response `200`:** the created ban record (`id`, `reason`, `issuedBy`, `issuedAt`,
`expiresAt`, `isActive`).

### `POST /api/v1/moderation/unban`

```json
{ "userId": "76561198000000000", "revokedBy": "Someone", "reason": "Appeal accepted" }
```
**Response:** `204 No Content` on success, `404` if there was no active ban to revoke.

### `POST /api/v1/moderation/mute` / `POST /api/v1/moderation/unmute`

Identical shape to ban/unban (`IssueMuteRequest` / `RevokeRequest`).

### `POST /api/v1/moderation/warn`

```json
{ "userId": "...", "nickname": "Kikobuf", "reason": "Spamming chat", "issuedBy": "Someone" }
```
Warnings are purely historical — Warden does not "enforce" them. **Response `200`:** the
created warning record.

### `POST /api/v1/moderation/kick`

```json
{ "userId": "...", "nickname": "Kikobuf", "reason": "AFK", "issuedBy": "Someone" }
```
Also purely historical — **the plugin performs the actual kick itself**; this endpoint
only records that it happened, for the player's history. **Response `200`:** the created
kick record.

### `GET /api/v1/moderation/history/{userId}`

Full moderation history for a player: every ban, mute, warning, and kick ever recorded,
newest first, plus their latest known nickname and first/last-seen timestamps.

**Response `200`:**
```json
{
  "userId": "76561198000000000",
  "latestNickname": "Kikobuf",
  "firstSeenAt": "2026-01-04T18:03:00Z",
  "lastSeenAt": "2026-07-09T22:11:00Z",
  "bans": [ { "id": 4, "reason": "...", "issuedBy": "...", "issuedAt": "...", "expiresAt": null, "revokedAt": null, "revokedBy": null, "isActive": true } ],
  "mutes": [ ... ],
  "warnings": [ ... ],
  "kicks": [ ... ]
}
```
`404` if the player has never been seen (no heartbeat, no moderation record).

---

## Server management API

Base path: `/api/v1/servers`

### `POST /api/v1/servers/heartbeat`

Send this roughly **every 30 seconds** from each game server.

```json
{
  "tps": 19.8,
  "players": [
    { "userId": "76561198000000000", "nickname": "Kikobuf" },
    { "userId": "76561198000000001", "nickname": "Someone" }
  ]
}
```
The player list **replaces** what Warden thinks is currently connected to that server —
send the complete current list every time, not a diff. Also updates each player's
`latestNickname`/`lastSeenAt` in their profile.

**Response `200`:** `{ "serverName": "MX Park EU", "receivedAt": "2026-07-10T02:00:00Z" }`

A server with no heartbeat for longer than `Warden:OfflineThresholdSeconds` (default 90s,
see `appsettings.json`) shows as **offline** in the dashboard.

### `GET /api/v1/servers/plugins`

Call this **on server startup** to discover which plugins apply to this server — every
plugin explicitly assigned to it, plus every plugin marked "global" (delivered to all
servers).

**Response `200`:**
```json
[
  {
    "id": 3,
    "name": "Warden Enforcer",
    "version": "1.2.0",
    "fileName": "WardenEnforcer.dll",
    "sha256Hash": "9F86D0...",
    "fileSizeBytes": 184320,
    "downloadUrl": "https://your-domain/api/v1/servers/plugins/3/download"
  }
]
```
Compare `sha256Hash` against a locally cached copy to skip re-downloading unchanged
plugins.

### `GET /api/v1/servers/plugins/{pluginId}/download`

Streams the plugin's raw DLL bytes (`application/octet-stream`). Only servers the plugin
is assigned to (or if it's global, any server) may download it — `403 Forbidden`
otherwise, `404` if the plugin doesn't exist.

---

## Error format

Failures generally return a JSON body of the form `{ "message": "..." }` alongside the
appropriate HTTP status code (`400` invalid input, `401` bad/missing API key, `403`
forbidden, `404` not found).

## A minimal C# plugin client sketch

```csharp
using var http = new HttpClient { BaseAddress = new Uri("https://your-domain/") };
http.DefaultRequestHeaders.Add("X-Server-Api-Key", config.WardenApiKey);

// On player join:
var status = await http.GetFromJsonAsync<ModerationStatusResponse>(
    $"api/v1/moderation/status/{playerUserId}");
if (status!.IsBanned) { /* deny join, show status.BanReason */ }

// Every 30 seconds:
await http.PostAsJsonAsync("api/v1/servers/heartbeat", new
{
    tps = server.Tps,
    players = server.Players.Select(p => new { userId = p.UserId, nickname = p.Nickname }),
});

// On startup:
var plugins = await http.GetFromJsonAsync<List<PluginManifestEntry>>("api/v1/servers/plugins");
foreach (var p in plugins!)
{
    if (LocalHashMatches(p.Name, p.Sha256Hash)) continue;
    var bytes = await http.GetByteArrayAsync(p.DownloadUrl);
    File.WriteAllBytes(Path.Combine(PluginsDir, p.FileName), bytes);
}
```