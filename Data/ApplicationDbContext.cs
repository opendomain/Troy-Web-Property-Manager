using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        public virtual DbSet<Applicant> Applicants { get; set; }

        public virtual DbSet<ApplicantInformation> ApplicantInformation { get; set; }

        public virtual DbSet<ApplicationStatusHistory> ApplicationStatusHistories { get; set; }

        public virtual DbSet<Lease> Leases { get; set; }

        public virtual DbSet<Property> Properties { get; set; }

        public virtual DbSet<RentalApplication> RentalApplications { get; set; }

        public virtual DbSet<Residence> Residences { get; set; }

        public virtual DbSet<Status> Statuses { get; set; }

        public virtual DbSet<Unit> Units { get; set; }

        public virtual DbSet<UnitType> UnitTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configures the Identity (AspNet*) tables
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<ApplicantInformation>(entity =>
            {
                entity.ToTable("ApplicantInformation");

                // One row per application: the application's id is also this table's key.
                entity.HasKey(e => e.RentalApplicationId);
                entity.Property(e => e.RentalApplicationId).HasColumnName("RentalApplicationID").ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(50);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(50);
                entity.Property(e => e.CurrentAddress).HasMaxLength(50);

                entity.HasOne(d => d.RentalApplication).WithOne(p => p.ApplicantInformation)
                    .HasForeignKey<ApplicantInformation>(d => d.RentalApplicationId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ApplicantInformation_RentalApplications");
            });

            modelBuilder.Entity<Applicant>(entity =>
            {
                entity.ToTable("Applicant");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.CurrentAddress).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(50);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.UserId).HasMaxLength(450);

                // One applicant profile per user
                entity.HasIndex(e => e.UserId).IsUnique();

                entity.HasOne<IdentityUser>().WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_Applicant_AspNetUsers");
            });

            modelBuilder.Entity<ApplicationStatusHistory>(entity =>
            {
                entity.ToTable("ApplicationStatusHistory");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ChangedDate).HasColumnType("datetime");
                entity.Property(e => e.Comment).HasMaxLength(500);
                entity.Property(e => e.ChangedByUser).HasMaxLength(450);
                entity.Property(e => e.RentalApplicationId).HasColumnName("RentalApplicationID");

                entity.HasOne(d => d.RentalApplication).WithMany(p => p.ApplicationStatusHistories)
                    .HasForeignKey(d => d.RentalApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_ApplicationStatusHistory_RentApplications");
            });

            modelBuilder.Entity<Lease>(entity =>
            {
                entity.ToTable("Lease");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.EndDate).HasColumnType("datetime");
                entity.Property(e => e.RentalApplicationId).HasColumnName("RentalApplicationID");
                entity.Property(e => e.StartDate).HasColumnType("datetime");
                entity.Property(e => e.UnitId).HasColumnName("UnitID");

                entity.HasOne(d => d.RentalApplication).WithMany(p => p.Leases)
                    .HasForeignKey(d => d.RentalApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Lease_RentApplications");

                entity.HasOne(d => d.Unit).WithMany(p => p.Leases)
                    .HasForeignKey(d => d.UnitId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Lease_Unit");
            });

            modelBuilder.Entity<Property>(entity =>
            {
                entity.ToTable("Property");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Address).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(50);
            });

            modelBuilder.Entity<RentalApplication>(entity =>
            {
                entity.ToTable("RentalApplications");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ApplicantId).HasColumnName("ApplicantID");
                entity.Property(e => e.Created).HasColumnType("datetime");
                entity.Property(e => e.Submitted).HasColumnType("datetime");
                entity.Property(e => e.UnitId).HasColumnName("UnitID");

                // Every update checks the status it read is still current, so concurrent status changes
                // (e.g. a withdraw racing an approval) fail instead of silently overwriting each other.
                entity.Property(e => e.Status).IsConcurrencyToken();

                // At most one open (Draft, Submitted or Returned) application per applicant and unit.
                entity.HasIndex(e => new { e.ApplicantId, e.UnitId }, "IX_RentalApplications_OpenPerApplicantUnit")
                    .IsUnique()
                    .HasFilter("[Status] IN (1, 2, 3)");
                // Kept explicitly: the filtered index above only covers open applications, so it can't serve the FK lookups.
                entity.HasIndex(e => e.ApplicantId, "IX_RentalApplications_ApplicantID");

                entity.HasOne(d => d.Applicant).WithMany(p => p.RentalApplications)
                    .HasForeignKey(d => d.ApplicantId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Applicant");

                entity.HasOne(d => d.StatusNavigation).WithMany(p => p.RentalApplications)
                    .HasForeignKey(d => d.Status)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Status");

                entity.HasOne(d => d.Unit).WithMany(p => p.RentalApplications)
                    .HasForeignKey(d => d.UnitId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Unit");
            });

            modelBuilder.Entity<Residence>(entity =>
            {
                entity.ToTable("Residence");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Address).HasMaxLength(50);
                entity.Property(e => e.MoveOutDate).HasColumnType("date");
                entity.Property(e => e.LandlordName).HasMaxLength(50);
                entity.Property(e => e.LandlordPhone).HasMaxLength(50);
                entity.Property(e => e.RentalApplicationId).HasColumnName("RentalApplicationID");
                entity.Property(e => e.MoveInDate).HasColumnType("date");

                entity.HasOne(d => d.RentalApplication).WithMany(p => p.Residences)
                    .HasForeignKey(d => d.RentalApplicationId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Residence_RentApplications");
            });

            modelBuilder.Entity<Status>(entity =>
            {
                entity.ToTable("Status");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasMaxLength(50);
            });

            modelBuilder.Entity<Unit>(entity =>
            {
                entity.ToTable("Unit");

                entity.Property(e => e.Id).HasColumnName("id");
                // DB defaults are kept for raw SQL inserts; EF always sends the value, so 0 bedrooms is stored as-is
                // instead of being replaced by the default of 1.
                entity.Property(e => e.Bedrooms).HasDefaultValue(1, "DF_Unit_Bedrooms").ValueGeneratedNever();
                entity.Property(e => e.MonthlyRent).HasPrecision(10, 2).HasDefaultValue(0m, "DF_Unit_Rent").ValueGeneratedNever();
                entity.Property(e => e.UnitNumber).HasMaxLength(50);
                entity.Property(e => e.UnitTypeId).HasColumnName("UnitTypeID");

                // Unit numbers are unique within a property.
                entity.HasIndex(e => new { e.PropertyId, e.UnitNumber }).IsUnique();

                entity.HasOne(d => d.Property).WithMany(p => p.Units)
                    .HasForeignKey(d => d.PropertyId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Unit_Property");

                entity.HasOne(d => d.UnitType).WithMany(p => p.Units)
                    .HasForeignKey(d => d.UnitTypeId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_Unit_UnitType");
            });

            modelBuilder.Entity<UnitType>(entity =>
            {
                entity.ToTable("UnitType");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.IsActive).HasDefaultValue(false, "DF_UnitType_Active").ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(50);
            });
        }
    }
}
