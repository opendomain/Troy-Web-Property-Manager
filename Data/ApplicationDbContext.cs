using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Models;

namespace Troy_Web_Property_Manager.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext(options)
    {
        public virtual DbSet<Applicant> Applicants { get; set; }

        public virtual DbSet<ApplicationStatusHistory> ApplicationStatusHistories { get; set; }

        public virtual DbSet<Lease> Leases { get; set; }

        public virtual DbSet<Property> Properties { get; set; }

        public virtual DbSet<RentApplication> RentApplications { get; set; }

        public virtual DbSet<Residence> Residences { get; set; }

        public virtual DbSet<Status> Statuses { get; set; }

        public virtual DbSet<Unit> Units { get; set; }

        public virtual DbSet<UnitType> UnitTypes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configures the Identity (AspNet*) tables
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Applicant>(entity =>
            {
                entity.ToTable("Applicant");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.CurrentAddress).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(50);
                entity.Property(e => e.Phone).HasMaxLength(50);
            });

            modelBuilder.Entity<ApplicationStatusHistory>(entity =>
            {
                entity.ToTable("ApplicationStatusHistory");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ChangedDate).HasColumnType("datetime");
                entity.Property(e => e.Comment).HasMaxLength(50);
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

            modelBuilder.Entity<RentApplication>(entity =>
            {
                entity.ToTable("RentApplications");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.ApplicantId).HasColumnName("ApplicantID");
                entity.Property(e => e.Created).HasColumnType("datetime");
                entity.Property(e => e.Submitted).HasColumnType("datetime");
                entity.Property(e => e.UnitId).HasColumnName("UnitID");

                entity.HasOne(d => d.Applicant).WithMany(p => p.RentApplications)
                    .HasForeignKey(d => d.ApplicantId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Applicant");

                entity.HasOne(d => d.StatusNavigation).WithMany(p => p.RentApplications)
                    .HasForeignKey(d => d.Status)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Status");

                entity.HasOne(d => d.Unit).WithMany(p => p.RentApplications)
                    .HasForeignKey(d => d.UnitId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("FK_RentApplications_Unit");
            });

            modelBuilder.Entity<Residence>(entity =>
            {
                entity.ToTable("Residence");

                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Address).HasMaxLength(50);
                entity.Property(e => e.EndDate).HasColumnType("datetime");
                entity.Property(e => e.LandlordName).HasMaxLength(50);
                entity.Property(e => e.LandlordPhone).HasMaxLength(50);
                entity.Property(e => e.RentalApplicationId).HasColumnName("RentalApplicationID");
                entity.Property(e => e.StartDate).HasColumnType("datetime");

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
                // DB defaults are kept for raw SQL inserts; EF always sends the value so 0 bedrooms / 0 rent are stored as-is
                entity.Property(e => e.Bedrooms).HasDefaultValue(1, "DF_Unit_Bedrooms").ValueGeneratedNever();
                entity.Property(e => e.Rent).HasDefaultValue(0f, "DF_Unit_Rent").ValueGeneratedNever();
                entity.Property(e => e.UnitNumber).HasMaxLength(50);
                entity.Property(e => e.UnitTypeId).HasColumnName("UnitTypeID");

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
                entity.Property(e => e.Active).HasDefaultValue(false, "DF_UnitType_Active").ValueGeneratedNever();
                entity.Property(e => e.Name).HasMaxLength(50);
            });
        }
    }
}
