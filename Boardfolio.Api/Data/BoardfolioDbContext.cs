using Boardfolio.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Boardfolio.Api.Data;

public class BoardfolioDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public BoardfolioDbContext(DbContextOptions<BoardfolioDbContext> options) : base(options) { }
    protected BoardfolioDbContext(DbContextOptions options) : base(options) { }
    public DbSet<KeyboardBuild> Builds => Set<KeyboardBuild>();
    public DbSet<KeyboardSwitch> KeyboardSwitches => Set<KeyboardSwitch>();
    public DbSet<CustomSwitch> CustomSwitches => Set<CustomSwitch>();
    public DbSet<PrivateOwnerCosts> OwnerCosts => Set<PrivateOwnerCosts>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");

        builder.Entity<KeyboardSwitch>().HasData(
            new KeyboardSwitch { Id = Guid.Parse("675a1063-7bbd-4559-b452-0d56f8867bc1"), Name = "MX Red", Manufacturer = "Cherry", Type = SwitchType.Linear },
            new KeyboardSwitch { Id = Guid.Parse("675a1063-7bbd-4559-b452-0d56f8867bc2"), Name = "MX Brown", Manufacturer = "Cherry", Type = SwitchType.Tactile },
            new KeyboardSwitch { Id = Guid.Parse("675a1063-7bbd-4559-b452-0d56f8867bc3"), Name = "MX Blue", Manufacturer = "Cherry", Type = SwitchType.Clicky });
        builder.Entity<KeyboardBuild>(build =>
        {
            build.HasIndex(x => x.OwnerUserId);
            build.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            build.Property(x => x.Name).HasMaxLength(100).IsRequired();
            build.Property(x => x.KeyboardModel).HasMaxLength(150).IsRequired();
            build.Property(x => x.Notes).HasMaxLength(2000);
            build.HasMany(x => x.Components).WithOne().HasForeignKey(x => x.KeyboardBuildId).OnDelete(DeleteBehavior.Cascade);
            build.HasMany(x => x.Media).WithOne().HasForeignKey(x => x.KeyboardBuildId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<BuildMedia>().Property(x => x.Url).HasMaxLength(2000).IsRequired();
        builder.Entity<BuildMedia>().Property(x => x.Caption).HasMaxLength(200);
        builder.Entity<KeyboardBuildComponent>(component =>
        {
            component.HasOne<KeyboardSwitch>().WithMany().HasForeignKey(x => x.KeyboardSwitchId).OnDelete(DeleteBehavior.Restrict);
            component.HasOne<CustomSwitch>().WithMany().HasForeignKey(x => x.CustomSwitchId).OnDelete(DeleteBehavior.Restrict);
            component.ToTable(t => t.HasCheckConstraint("CK_Component_SwitchSelection",
                "(KeyboardSwitchId IS NULL OR CustomSwitchId IS NULL) AND (Type = 5 OR (KeyboardSwitchId IS NULL AND CustomSwitchId IS NULL))"));
        });
        builder.Entity<CustomSwitch>().HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Entity<PrivateOwnerCosts>(costs =>
        {
            costs.Property(x => x.OwnerUserId).IsRequired();
            costs.HasIndex(x => new { x.OwnerUserId, x.KeyboardBuildId }).IsUnique();
            costs.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            // No cascading build/component relationship: expenses survive their removal.
            costs.OwnsMany(x => x.Entries, entry => entry.WithOwner().HasForeignKey("LedgerId"));
            costs.Ignore(x => x.TotalPaid);
        });
    }
}
