using IBhayiPharmacyManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using System.Linq.Expressions;

namespace IBhayiPharmacyManagementSystem.Data
{
    public class AppDbContext : IdentityDbContext<Users>
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }
        // When true, SaveChanges/SaveChangesAsync will not convert deletes to soft-deletes.
        // This is intended for admin-only permanent deletion flows.
        public bool BypassSoftDelete { get; set; } = false;
        public DbSet<ActiveIngredients> ActiveIngredients { get; set; }

        public DbSet<Address> Addresses { get; set; }
        public DbSet<CustomerAllergy> CustomerAllergies { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<DosageForm> Dosages { get; set; }
        public DbSet<Medication> Medications { get; set; }
        public DbSet<MedicationIngredient> MedicationIngredients { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Pharmacist> Pharmacists { get; set; }
        public DbSet<Pharmacy> Pharmacies { get; set; }
        public DbSet<PharmacyManager> PharmacyManagers { get; set; }
        public DbSet<Prescription> Prescriptions { get; set; }
        public DbSet<PrescriptionLine> PrescriptionLines { get; set; }
        public DbSet<StockOrder> StockOrders { get; set; }
        public DbSet<StockOrderLine> StockOrderLines { get; set; }
        public DbSet<StockOrderItem> StockOrderItems { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<UnprocessedScript> UnprocessedScripts { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<PrescriptionRepeat> PrescriptionRepeats { get; set; }
        public DbSet<DispensedPrescription> DispensedPrescriptions { get; set; }
        public DbSet<DispensationRequest> DispensationRequests { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<Notification> Notifications { get; set; }


        public DbSet<MedicalInfo> MedicalInfos { get; set; }

        public DbSet<NotificationPharmacyManager> NotificationP { get; set; }

        public DbSet<CustomerActivity> CustomerActivities { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Set default delete behavior to Restrict
            foreach (var relationship in builder.Model.GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // Soft delete shadow properties and global query filters for all entities
            foreach (var entityType in builder.Model.GetEntityTypes())
            {
                // Skip owned types
                if (entityType.IsOwned()) continue;

                // Include PrescriptionLine in soft delete (removed exclusion)

                // Add shadow properties
                builder.Entity(entityType.ClrType).Property<bool>("IsDeleted").HasDefaultValue(false);
                builder.Entity(entityType.ClrType).Property<DateTime?>("DeletedAt");
                builder.Entity(entityType.ClrType).Property<string>("DeletedBy").HasMaxLength(256);

                // Apply global filter: IsDeleted == false
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var isDeletedProperty = Expression.Call(
                    typeof(EF), nameof(EF.Property), new[] { typeof(bool) }, parameter, Expression.Constant("IsDeleted"));
                var compare = Expression.Equal(isDeletedProperty, Expression.Constant(false));
                var lambda = Expression.Lambda(compare, parameter);
                builder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }

            // Configure Medication-DosageForm relationship
            builder.Entity<Medication>()
                .HasOne(m => m.DosageForm)
                .WithMany(d => d.Medications)
                .HasForeignKey(m => m.DosageFormId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Medication-Ingredient many-to-many relationship
            builder.Entity<MedicationIngredient>()
                .HasKey(mi => new { mi.MedicationId, mi.ActiveIngredientId });

            builder.Entity<MedicationIngredient>()
                .HasOne(mi => mi.Medication)
                .WithMany(m => m.ActiveIngredients)
                .HasForeignKey(mi => mi.MedicationId);

            builder.Entity<MedicationIngredient>()
                .HasOne(mi => mi.ActiveIngredient)
                .WithMany(ai => ai.MedicationIngredients)
                .HasForeignKey(mi => mi.ActiveIngredientId);

            // Add indexes for performance
            builder.Entity<Medication>()
                .HasIndex(m => m.Name)
                .HasFilter("\"IsDeleted\" = FALSE")
                .IsUnique();

            builder.Entity<ActiveIngredients>()
                .HasIndex(ai => ai.Name)
                .HasFilter("\"IsDeleted\" = FALSE")
                .IsUnique();

            builder.Entity<DosageForm>()
                .ToTable("DosageForms")
                .HasIndex(df => df.Type)
                .HasFilter("\"IsDeleted\" = FALSE")
                .IsUnique();

            // Add unique constraint for Doctor PracticeNumber
            builder.Entity<Doctor>()
                .HasIndex(d => d.PracticeNumber)
                .HasFilter("\"IsDeleted\" = FALSE")
                .IsUnique();

            builder.Entity<PrescriptionLine>()
                .HasOne(pl => pl.Prescription)
                .WithMany(p => p.PrescriptionLines)
                .HasForeignKey(pl => pl.PrescriptionId)
                .IsRequired(false); // Make the navigation optional to handle soft-deleted prescriptions

            // Configure PrescriptionLine-Medication relationship to handle global query filter
            builder.Entity<PrescriptionLine>()
                .HasOne(pl => pl.Medication)
                .WithMany()
                .HasForeignKey(pl => pl.MedicationId)
                .IsRequired(false); // Make the navigation optional to handle soft-deleted medications

            builder.Entity<UnprocessedScript>()
                .HasOne(u => u.Prescription)
                .WithOne()
                .HasForeignKey<Prescription>(p => p.UploadId)
                .IsRequired(false) // Allow null UploadId for imported prescriptions
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Prescription>()
                .HasMany(p => p.PrescriptionLines)
                .WithOne(pl => pl.Prescription)
                .HasForeignKey(pl => pl.PrescriptionId)
                .OnDelete(DeleteBehavior.Cascade);


            builder.Entity<Customer>()
                .HasMany(c => c.Allergies)
                .WithOne(a => a.Customer)
                .HasForeignKey(a => a.CustomerId);

            builder.Entity<Customer>()
                .HasMany(c => c.Prescriptions)
                .WithOne(p => p.Customer)
                .HasForeignKey(p => p.CustomerId);


            // Configure PrescriptionRepeat relationships
            // builder.Entity<PrescriptionRepeat>()
            //     .HasOne(pr => pr.PrescriptionLine)
            //     .WithOne(pl => pl.PrescriptionRepeat)
            //     .HasForeignKey<PrescriptionRepeat>(pr => pr.PrescriptionLineId)
            //     .OnDelete(DeleteBehavior.Cascade);

            // Configure DispensedPrescription relationships
            builder.Entity<DispensedPrescription>()
                .HasOne(dp => dp.PrescriptionLine)
                .WithMany()
                .HasForeignKey(dp => dp.PrescriptionLineId)
                .OnDelete(DeleteBehavior.Cascade); // Allow cascade delete when prescription line is deleted

            builder.Entity<DispensedPrescription>()
                .HasOne(dp => dp.Pharmacist)
                .WithMany()
                .HasForeignKey(dp => dp.PharmacistId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Pharmacy-Address relationship
            builder.Entity<Pharmacy>()
                .HasOne(p => p.Address)
                .WithMany() // Assuming an Address can be used by multiple pharmacies or has no inverse navigation
                .HasForeignKey(p => p.AddressId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting an Address if a Pharmacy is linked

            // Configure Pharmacy-Pharmacist (Responsible Pharmacist) relationship
            builder.Entity<Pharmacy>()
                .HasOne(p => p.Pharmacist)
                .WithMany() // Assuming a Pharmacist can be responsible for only one pharmacy at a time or has no inverse navigation
                .HasForeignKey(p => p.PharmacistId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent deleting a Pharmacist if they are a responsible pharmacist

            // Configure the identity tables if needed
            // (Add any custom identity configurations here)
            builder.Entity<DispensedPrescription>(entity =>
            {
                entity.Property(e => e.AmountDue)
                    .HasPrecision(18, 2); // Precision 18, Scale 2 (for currency)
            });

            builder.Entity<StockOrder>(entity =>
            {
                entity.Property(e => e.QuoteAmount)
                    .HasPrecision(18, 2);
            });

            builder.Entity<StockOrderItem>(entity =>
            {
                // No pricing properties to configure
            });
        }

        public override int SaveChanges()
        {
            if (!BypassSoftDelete)
            {
                ConvertDeletesToSoftDeletes();
            }
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!BypassSoftDelete)
            {
                ConvertDeletesToSoftDeletes();
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ConvertDeletesToSoftDeletes()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Deleted))
            {
                // Convert delete to soft-delete
                entry.State = EntityState.Modified;
                entry.Property("IsDeleted").CurrentValue = true;
                entry.Property("DeletedAt").CurrentValue = now;
                // Optional: set DeletedBy via a service/HttpContext accessor if available
            }
        }

    }
}