using Microsoft.EntityFrameworkCore;
using Warden.Web.Models;

namespace Warden.Web.Data;

public class WardenDbContext : DbContext
{
    public WardenDbContext(DbContextOptions<WardenDbContext> options) : base(options)
    {
    }

    public DbSet<WebUser> WebUsers => Set<WebUser>();
    public DbSet<WebRole> WebRoles => Set<WebRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserRoleAssignment> UserRoleAssignments => Set<UserRoleAssignment>();

    public DbSet<PlayerProfile> PlayerProfiles => Set<PlayerProfile>();
    public DbSet<Ban> Bans => Set<Ban>();
    public DbSet<Mute> Mutes => Set<Mute>();
    public DbSet<Warning> Warnings => Set<Warning>();
    public DbSet<KickRecord> Kicks => Set<KickRecord>();

    public DbSet<GameServer> GameServers => Set<GameServer>();
    public DbSet<ServerPlayerSession> ServerPlayerSessions => Set<ServerPlayerSession>();

    public DbSet<Plugin> Plugins => Set<Plugin>();
    public DbSet<PluginAssignment> PluginAssignments => Set<PluginAssignment>();

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WebUser>()
            .HasIndex(u => u.DiscordUserId)
            .IsUnique();

        modelBuilder.Entity<WebRole>()
            .HasIndex(r => r.Name)
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.WebRole)
            .WithMany(r => r.Permissions)
            .HasForeignKey(rp => rp.WebRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RolePermission>()
            .HasIndex(rp => new { rp.WebRoleId, rp.PermissionKey })
            .IsUnique();

        modelBuilder.Entity<UserRoleAssignment>()
            .HasOne(ura => ura.WebUser)
            .WithMany(u => u.RoleAssignments)
            .HasForeignKey(ura => ura.WebUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRoleAssignment>()
            .HasOne(ura => ura.WebRole)
            .WithMany(r => r.UserAssignments)
            .HasForeignKey(ura => ura.WebRoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserRoleAssignment>()
            .HasIndex(ura => new { ura.WebUserId, ura.WebRoleId })
            .IsUnique();

        // Moderation records reference PlayerProfile but must survive even if we ever
        // pruned a profile, so we do NOT cascade-delete; the FK is nullable-friendly.
        modelBuilder.Entity<Ban>()
            .HasOne(b => b.Player)
            .WithMany(p => p.Bans)
            .HasForeignKey(b => b.PlayerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Mute>()
            .HasOne(m => m.Player)
            .WithMany(p => p.Mutes)
            .HasForeignKey(m => m.PlayerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Warning>()
            .HasOne(w => w.Player)
            .WithMany(p => p.Warnings)
            .HasForeignKey(w => w.PlayerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KickRecord>()
            .HasOne(k => k.Player)
            .WithMany(p => p.Kicks)
            .HasForeignKey(k => k.PlayerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Ban>().HasIndex(b => b.PlayerUserId);
        modelBuilder.Entity<Mute>().HasIndex(m => m.PlayerUserId);
        modelBuilder.Entity<Warning>().HasIndex(w => w.PlayerUserId);
        modelBuilder.Entity<KickRecord>().HasIndex(k => k.PlayerUserId);

        modelBuilder.Entity<GameServer>()
            .HasIndex(s => s.ApiKeyHash)
            .IsUnique();

        modelBuilder.Entity<ServerPlayerSession>()
            .HasOne(s => s.GameServer)
            .WithMany(gs => gs.PlayerSessions)
            .HasForeignKey(s => s.GameServerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ServerPlayerSession>()
            .HasIndex(s => new { s.GameServerId, s.PlayerUserId })
            .IsUnique();

        modelBuilder.Entity<PluginAssignment>()
            .HasOne(pa => pa.Plugin)
            .WithMany(p => p.Assignments)
            .HasForeignKey(pa => pa.PluginId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PluginAssignment>()
            .HasOne(pa => pa.GameServer)
            .WithMany(gs => gs.PluginAssignments)
            .HasForeignKey(pa => pa.GameServerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PluginAssignment>()
            .HasIndex(pa => new { pa.PluginId, pa.GameServerId })
            .IsUnique();
    }
}
