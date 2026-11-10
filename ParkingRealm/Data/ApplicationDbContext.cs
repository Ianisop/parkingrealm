using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ParkingRealm.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public const int NameMaxLength = 100;
    public DbSet<ParkingSpace> ParkingSpaces => Set<ParkingSpace>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(user =>
        {
            user.Property(u => u.FirstName).IsRequired().HasMaxLength(NameMaxLength);
            user.Property(u => u.LastName).IsRequired().HasMaxLength(NameMaxLength);

            // Identity's default EmailIndex is not unique; enforce it in the database too.
            user.HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        });

        builder.Entity<ParkingSpace>(space =>
        {
            space.HasIndex(s => new { s.Row, s.Column }).IsUnique();
        });

        builder.Entity<Vehicle>(vehicle =>
        {
            vehicle.Property(v => v.RegistrationNumber).HasMaxLength(32).IsRequired();
            vehicle.Property(v => v.NormalizedRegistrationNumber).HasMaxLength(32).IsRequired();
            vehicle.HasIndex(v => new { v.OwnerId, v.NormalizedRegistrationNumber }).IsUnique();
            vehicle.HasAlternateKey(v => new { v.Id, v.OwnerId });
            vehicle.HasOne(v => v.Owner).WithMany().HasForeignKey(v => v.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Reservation>(reservation =>
        {
            reservation.Property(r => r.Status).HasConversion<string>().HasMaxLength(16);
            reservation.ToTable(table => table.HasCheckConstraint("CK_Reservations_Interval", "[End] > [Start]"));
            reservation.HasIndex(r => new { r.ParkingSpaceId, r.Status, r.Start, r.End });
            reservation.HasIndex(r => new { r.OwnerId, r.Start });
            reservation.HasOne(r => r.Owner).WithMany().HasForeignKey(r => r.OwnerId).OnDelete(DeleteBehavior.Restrict);
            reservation.HasOne(r => r.Vehicle).WithMany()
                .HasForeignKey(r => new { r.VehicleId, r.OwnerId })
                .HasPrincipalKey(v => new { v.Id, v.OwnerId })
                .OnDelete(DeleteBehavior.Restrict);
            reservation.HasOne(r => r.ParkingSpace).WithMany().HasForeignKey(r => r.ParkingSpaceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
