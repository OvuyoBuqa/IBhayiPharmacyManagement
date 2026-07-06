using IBhayiPharmacyManagementSystem.Data;
using IBhayiPharmacyManagementSystem.Models;
using IBhayiPharmacyManagementSystem.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IBhayiPharmacyManagementSystem.Services
{
    public class SeedService
    {
        public static async Task SeedDatabase(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Users>>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<SeedService>>();

            try
            {
                // Ensure the database is ready
                logger.LogInformation("Ensuring the database is created.");
                await context.Database.EnsureCreatedAsync();

                // Add roles
                logger.LogInformation("Seeding roles.");
                await AddRoleAsync(roleManager, "Admin");
                await AddRoleAsync(roleManager, "Customer");
                await AddRoleAsync(roleManager, "Pharmacist");
                await AddRoleAsync(roleManager, "PharmacyManager");

                // Run data migrations
                logger.LogInformation("Running data migrations.");
                await UpdateDoctorInformationInUnprocessedScripts(context, logger);

                // Add admin user
                logger.LogInformation("Seeding admin user.");
                var adminEmail = "admin@gmail.com";
                if (await userManager.FindByEmailAsync(adminEmail) == null)
                {
                    var adminUser = new Users
                    {
                        FullName = "Admin",
                        UserName = adminEmail,
                        NormalizedUserName = adminEmail.ToUpper(),
                        Email = adminEmail,
                        NormalizedEmail = adminEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };

                    var result = await userManager.CreateAsync(adminUser, "Admin@123");
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning Admin role to the admin user.");
                        await userManager.AddToRoleAsync(adminUser, "Admin");
                    }
                    else
                    {
                        logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                }

                // Optionally add a default pharmacy manager
                logger.LogInformation("Seeding default pharmacy manager.");
                var managerEmail = "manager@gmail.com";
                if (await userManager.FindByEmailAsync(managerEmail) == null)
                {
                    var managerUser = new Users
                    {
                        FullName = "Pharmacy Manager",
                        UserName = managerEmail,
                        NormalizedUserName = managerEmail.ToUpper(),
                        Email = managerEmail,
                        NormalizedEmail = managerEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };

                    var result = await userManager.CreateAsync(managerUser, "Password@123");
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning PharmacyManager role to the manager user.");
                        await userManager.AddToRoleAsync(managerUser, "PharmacyManager");

                        // Create a corresponding PharmacyManager record
                        var defaultPharmacy = await context.Pharmacies.FirstOrDefaultAsync();
                        if (defaultPharmacy == null)
                        {
                            // If no pharmacy exists, create a default one
                            var defaultAddress = new Address
                            {
                                Street = "123 Main St",
                                Suburb = "Central",
                                City = "Cape Town",
                                Province = "Western Cape",
                                ZipCode = "8001",
                                Country = "South Africa"
                            };
                            context.Addresses.Add(defaultAddress);
                            await context.SaveChangesAsync();

                            var defaultPharmacist = await context.Pharmacists.FirstOrDefaultAsync();
                            if (defaultPharmacist == null)
                            {
                                var pharmacistUser = new Users
                                {
                                    FullName = "Default Pharmacist",
                                    UserName = "defaultpharmacist@gmail.com",
                                    NormalizedUserName = "DEFAULTPHARMACIST@GMAIL.COM",
                                    Email = "defaultpharmacist@gmail.com",
                                    NormalizedEmail = "DEFAULTPHARMACIST@GMAIL.COM",
                                    EmailConfirmed = true,
                                    SecurityStamp = Guid.NewGuid().ToString()
                                };
                                await userManager.CreateAsync(pharmacistUser, "Password@123");
                                await userManager.AddToRoleAsync(pharmacistUser, "Pharmacist");

                                defaultPharmacist = new Pharmacist
                                {
                                    UserId = pharmacistUser.Id,
                                    Name = "Default",
                                    Surname = "Pharmacist",
                                    IDNumber = "0000000000000",
                                    RegistrationNumber = "PH00000",
                                    CellPhone = "0000000000",
                                    Email = pharmacistUser.Email
                                };
                                context.Pharmacists.Add(defaultPharmacist);
                                await context.SaveChangesAsync();
                            }

                            defaultPharmacy = new Pharmacy
                            {
                                Name = "IBhayi Pharmacy",
                                HealthcareCouncilRegistrationNumber = "HCRN12345",
                                AddressId = defaultAddress.AddressId,
                                ContactNumber = "0211234567",
                                Email = "info@ibhayipharmacy.com",
                                WebsiteURL = "https://soit-iis.mandela.ac.za/GRP-04-08",
                                PharmacistId = defaultPharmacist.PharmacistId
                            };
                            context.Pharmacies.Add(defaultPharmacy);
                            await context.SaveChangesAsync();
                        }

                        var pharmacyManager = new PharmacyManager
                        {
                            UserId = managerUser.Id,
                            Name = "Pharmacy",
                            Surname = "Manager",
                            BranchName = "Main Branch",
                            ContactNumber = "0712345678",
                            Email = managerEmail,
                            PharmacyId = defaultPharmacy.PharmacyId
                        };
                        context.PharmacyManagers.Add(pharmacyManager);
                        await context.SaveChangesAsync();
                    }
                }

                // Optionally add a default pharmacist
                logger.LogInformation("Seeding default pharmacist.");
                var pharmacistEmail = "pharmacist@gmail.com";
                if (await userManager.FindByEmailAsync(pharmacistEmail) == null)
                {
                    var pharmacistUser = new Users
                    {
                        FullName = "Pharmacist",
                        UserName = pharmacistEmail,
                        NormalizedUserName = pharmacistEmail.ToUpper(),
                        Email = pharmacistEmail,
                        NormalizedEmail = pharmacistEmail.ToUpper(),
                        EmailConfirmed = true,
                        SecurityStamp = Guid.NewGuid().ToString()
                    };

                    var result = await userManager.CreateAsync(pharmacistUser, "Password@123");
                    if (result.Succeeded)
                    {
                        logger.LogInformation("Assigning Pharmacist role to the pharmacist user.");
                        await userManager.AddToRoleAsync(pharmacistUser, "Pharmacist");
                    }
                }

                // Sample customer removed - no longer seeding test data


                // Add a "Custom" active ingredient for custom allergies
                logger.LogInformation("Seeding custom active ingredient for custom allergies.");
                if (!await context.ActiveIngredients.AnyAsync(ai => ai.Name == "Custom"))
                {
                    var customIngredient = new ActiveIngredients
                    {
                        Name = "Custom",
                        Description = "Custom allergen not in the standard database",
                        Strength = "N/A"
                    };
                    context.ActiveIngredients.Add(customIngredient);
                    await context.SaveChangesAsync();
                    logger.LogInformation("Added custom active ingredient with ID: {Id}", customIngredient.ActiveIngredientId);
                }

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding the database.");
            }
        }


        private static async Task AddRoleAsync(RoleManager<IdentityRole> roleManager, string roleName)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    throw new Exception($"Failed to create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }

        private static async Task UpdateDoctorInformationInUnprocessedScripts(AppDbContext context, ILogger logger)
        {
            try
            {
                logger.LogInformation("Updating doctor information in UnprocessedScripts from Prescriptions...");

                // Get all UnprocessedScripts that don't have doctor information but have associated Prescriptions with doctor information
                var unprocessedScriptsToUpdate = await context.UnprocessedScripts
                    .Where(us => us.DoctorId == null && 
                                (us.Status == UnprocessedScript.PrescriptionStatus.Processing || 
                                 us.Status == UnprocessedScript.PrescriptionStatus.Completed))
                    .Include(us => us.Prescription)
                    .Where(us => us.Prescription != null && us.Prescription.DoctorId != null)
                    .ToListAsync();

                logger.LogInformation($"Found {unprocessedScriptsToUpdate.Count} UnprocessedScripts to update with doctor information.");

                foreach (var script in unprocessedScriptsToUpdate)
                {
                    if (script.Prescription?.DoctorId != null)
                    {
                        script.DoctorId = script.Prescription.DoctorId;
                        logger.LogInformation($"Updated UnprocessedScript {script.UnploadId} with DoctorId {script.DoctorId}");
                    }
                }

                if (unprocessedScriptsToUpdate.Any())
                {
                    await context.SaveChangesAsync();
                    logger.LogInformation($"Successfully updated {unprocessedScriptsToUpdate.Count} UnprocessedScripts with doctor information.");
                }
                else
                {
                    logger.LogInformation("No UnprocessedScripts needed updating with doctor information.");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating doctor information in UnprocessedScripts");
            }
        }
    }
}