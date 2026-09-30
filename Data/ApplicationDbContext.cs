using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Data
{
    /// <summary>
    /// Our EF Core context - the Identity tables plus all the rental stuff (Technical 2.a, 3.a).
    /// </summary>
    /// <remarks>
    /// <para>It's code-first: this class and the entities in <c>Models</c> define the schema. Run
    /// <c>dotnet ef migrations add</c> to generate a migration into <c>Data/Migrations</c>, and
    /// <c>Program.CreateDatabase</c> applies anything pending on startup (Technical 2.b.i). No hand-made tables.</para>
    /// <para>Inheriting from <see cref="IdentityDbContext"/> puts the AspNet* tables in the same database, so we can
    /// have real foreign keys to users (Applicant.UserId → AspNetUsers.Id) and one transaction can cover both. The demo
    /// seeder depends on that.</para>
    /// <para>I went with the Fluent API here instead of attributes on the entities. The entity classes stay clean
    /// and all the table/column/index/constraint decisions are in one file. The names are spelled out so the schema
    /// is easy to read in SQL tools.</para>
    /// <para>Deletes: most relationships use <c>ClientSetNull</c> on a required FK, which ends up as NO ACTION in the
    /// database - so deleting a parent that still has children fails instead of cascading. We never want applications,
    /// leases or history to vanish because someone removed a unit or property, so the services check first and tell
    /// the user why they can't delete it.</para>
    /// <para>Anywhere two requests racing could break a rule, the database backs us up: unique indexes (one applicant
    /// profile per user, one open application per applicant and unit, unique unit numbers per property) and a
    /// concurrency token on the application status.</para>
    /// </remarks>
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
            // Sets up the Identity (AspNet*) tables
            base.OnModelCreating(modelBuilder);

            // Section 1 of an application (4.a.i), one-to-one with RentalApplication. It's its own table so the
            // section's data stays together and simply doesn't exist until they save it the first time. Each
            // application gets its own copy, so a submitted application keeps what was submitted even if the
            // applicant changes their details later.
            modelBuilder.Entity<ApplicantInformation>(entity =>
            {
                entity.ToTable("ApplicantInformation");

                // One row per application - the application's id is the key here too. That shared key is how EF does
                // a real 1:1, and it means there can't be two rows for the same application.
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

            // The applicant as a person, tied to their Identity login. Separate from AspNetUsers so we don't touch the
            // Identity schema. These details are just used to pre-fill section 1 on a new application.
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

                // Real FK to AspNetUsers, but no navigation property on IdentityUser. SetNull so that if the login
                // gets deleted, the applicant and their application history stick around.
                entity.HasOne<IdentityUser>().WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.SetNull)
                    .HasConstraintName("FK_Applicant_AspNetUsers");
            });

            // One row per status change (5.c: who, when, comment). A review is really just a status change, so
            // review outcomes and comments go here too - no separate "reviews" table.
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

            // Leases get created when an application is approved (2.d). They have their own table (instead of dates
            // on the unit) so a unit keeps its lease history and each lease points back to its application.
            // "Available" is worked out from these dates when we query - we never store it.
            modelBuilder.Entity<Lease>(entity =>
            {
                // EndDate is exclusive, so a lease has to end after it starts, not on the same day.
                entity.ToTable("Lease", t => t.HasCheckConstraint("CK_Lease_Term", "[EndDate] > [StartDate]"));

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

            // The application itself: one unit, one applicant, a status, and the two "section saved" flags (4.a, 4.b.ii).
            modelBuilder.Entity<RentalApplication>(entity =>
            {
                entity.ToTable("RentalApplications");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ApplicantId).HasColumnName("ApplicantID");
                entity.Property(e => e.Created).HasColumnType("datetime");
                entity.Property(e => e.Submitted).HasColumnType("datetime");
                entity.Property(e => e.UnitId).HasColumnName("UnitID");

                // Every update checks the status hasn't changed since we read it, so if two changes collide
                // (say, a withdraw and an approval at the same time) one fails instead of quietly overwriting the other.
                entity.Property(e => e.Status).IsConcurrencyToken();

                // At most one open (Draft, Submitted or Returned) application per applicant and unit.
                // It's a filtered unique index, so closed ones (Approved/Denied/Withdrawn) don't count and you can
                // apply again after withdrawing. This is also what saves us when someone double-clicks Apply.
                entity.HasIndex(e => new { e.ApplicantId, e.UnitId }, "IX_RentalApplications_OpenPerApplicantUnit")
                    .IsUnique()
                    .HasFilter("[Status] IN (1, 2, 3)");
                // Need this one too - the filtered index above only covers open applications, so it's no good for FK lookups.
                entity.HasIndex(e => e.ApplicantId, "IX_RentalApplications_ApplicantID");

                entity.HasOne(d => d.Applicant).WithMany(p => p.RentalApplications)
                    .HasForeignKey(d => d.ApplicantId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Applicant");

                // Status is an enum in code (the state machine needs the actual values) and also an FK to the Status
                // lookup table. The FK keeps junk values out, and the lookup means you see names instead of numbers
                // in SQL. Program.SeedLookups keeps the table in sync with the enum.
                entity.HasOne(d => d.StatusNavigation).WithMany(p => p.RentalApplications)
                    .HasForeignKey(d => d.Status)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Status");

                entity.HasOne(d => d.Unit).WithMany(p => p.RentalApplications)
                    .HasForeignKey(d => d.UnitId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Unit");
            });

            // Section 2 (4.a.ii): any number of prior residences per application. Move-in/out are plain "date" columns (DateOnly).
            modelBuilder.Entity<Residence>(entity =>
            {
                // Same rule as ResidenceViewModel.Validate, backed up by the database.
                entity.ToTable("Residence", t => t.HasCheckConstraint("CK_Residence_Dates", "[MoveOutDate] >= [MoveInDate]"));

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

            // Lookup for application statuses. The ids match the ApplicationStatus enum values (see Program.SeedLookups).
            modelBuilder.Entity<Status>(entity =>
            {
                entity.ToTable("Status");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasMaxLength(50);
            });

            // A unit of a property (2.b): unit number, bedrooms, monthly rent and unit type.
            modelBuilder.Entity<Unit>(entity =>
            {
                // Matches the Range(0, 10) on UnitFormViewModel.Bedrooms.
                entity.ToTable("Unit", t => t.HasCheckConstraint("CK_Unit_Bedrooms", "[Bedrooms] >= 0 AND [Bedrooms] <= 10"));

                entity.Property(e => e.Id).HasColumnName("id");
                // The DB default is only there for raw SQL inserts. EF always sends a value, so 0 bedrooms stays 0
                // and doesn't get swapped for the default of 1.
                entity.Property(e => e.Bedrooms).HasDefaultValue(1, "DF_Unit_Bedrooms").ValueGeneratedNever();
                // Money is decimal(10,2) - exact cents, no floating point.
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

            // Unit Type lookup with Active/Inactive values (2.c). It's a table rather than an enum because managers
            // need to turn types on and off without us changing code.
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
