using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace OKRPerformanceManagement.Web.Services
{
    public class SeedDataService
    {
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public SeedDataService(ApplicationDbContext context, RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _roleManager = roleManager;
            _userManager = userManager;
        }

        public async Task SeedDataAsync()
        {
            await SeedIdentityRolesAsync();
            await SeedRolesAsync();
            await SeedOKRTemplatesAsync();
            await SeedDefaultUsersAsync();
        }

        public async Task ReSeedOKRTemplatesAsync()
        {
            // Clear existing OKR templates and related data
            var existingTemplates = await _context.OKRTemplates.ToListAsync();
            foreach (var template in existingTemplates)
            {
                var objectives = await _context.OKRTemplateObjectives.Where(o => o.OKRTemplateId == template.Id).ToListAsync();
                foreach (var objective in objectives)
                {
                    var keyResults = await _context.OKRTemplateKeyResults.Where(kr => kr.OKRTemplateObjectiveId == objective.Id).ToListAsync();
                    _context.OKRTemplateKeyResults.RemoveRange(keyResults);
                }
                _context.OKRTemplateObjectives.RemoveRange(objectives);
            }
            _context.OKRTemplates.RemoveRange(existingTemplates);
            await _context.SaveChangesAsync();

            // Re-seed OKR templates
            await SeedOKRTemplatesAsync();
        }

        private async Task SeedIdentityRolesAsync()
        {
            string[] roles = { "Administration", "Support_Systems Engineer", "Snr and Technical Team Leads", "Manager", "Consultant", "Admin" };

            foreach (string role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private async Task SeedRolesAsync()
        {
            if (!_context.EmployeeRoles.Any())
            {
                var roles = new List<EmployeeRole>
                {
                    new EmployeeRole
                    {
                        Name = "Administration",
                        Description = "Administration or Junior Support Engineer",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    },
                    new EmployeeRole
                    {
                        Name = "Support_Systems Engineer",
                        Description = "Support/Systems Engineer",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    },
                    new EmployeeRole
                    {
                        Name = "Snr and Technical Team Leads",
                        Description = "Senior and Technical Team Leads",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    },
                    new EmployeeRole
                    {
                        Name = "Manager",
                        Description = "Manager",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    },
                    new EmployeeRole
                    {
                        Name = "Consultant",
                        Description = "Consultant",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    }
                };

                _context.EmployeeRoles.AddRange(roles);
                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedOKRTemplatesAsync()
        {
            if (!_context.OKRTemplates.Any())
            {
                var administrationRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Administration");
                var supportRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Support_Systems Engineer");
                var seniorRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Snr and Technical Team Leads");
                var managerRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Manager");
                var consultantRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Consultant");

                // Create basic templates without detailed OKR data
                var templates = new List<OKRTemplate>();

                if (administrationRole != null)
                {
                    templates.Add(new OKRTemplate
                    {
                        Name = "Administration or Jnr Support Engineer OKR Template",
                        Role = "Administration",
                        Description = "OKR template for Administration or Junior Support Engineer role",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        RoleId = administrationRole.Id
                    });
                }

                if (supportRole != null)
                {
                    templates.Add(new OKRTemplate
                    {
                        Name = "Support Systems Engineer OKR Template",
                        Role = "Support_Systems Engineer",
                        Description = "OKR template for Support/Systems Engineer role",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        RoleId = supportRole.Id
                    });
                }

                if (seniorRole != null)
                {
                    templates.Add(new OKRTemplate
                    {
                        Name = "Snr and Technical Team Leads OKR Template",
                        Role = "Snr and Technical Team Leads",
                        Description = "OKR template for Senior and Technical Team Leads role",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        RoleId = seniorRole.Id
                    });
                }

                if (managerRole != null)
                {
                    templates.Add(new OKRTemplate
                    {
                        Name = "Managers OKR Template",
                        Role = "Manager",
                        Description = "OKR template for Manager role",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        RoleId = managerRole.Id
                    });
                }

                if (consultantRole != null)
                {
                    templates.Add(new OKRTemplate
                    {
                        Name = "Consultant OKR Template",
                        Role = "Consultant",
                        Description = "OKR template for Consultant role",
                        IsActive = true,
                        CreatedDate = DateTime.Now,
                        RoleId = consultantRole.Id
                    });
                }

                _context.OKRTemplates.AddRange(templates);
                await _context.SaveChangesAsync();
            }
        }

        private async Task SeedDefaultUsersAsync()
        {
            // Create default admin user if it doesn't exist
            var adminUser = await _userManager.FindByEmailAsync("admin@okr.com");
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = "admin@okr.com",
                    Email = "admin@okr.com",
                    EmailConfirmed = true,
                    FirstName = "System",
                    LastName = "Administrator"
                };

                var result = await _userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(adminUser, "Admin");
                    
                    // Create employee record
                    var employee = new Employee
                    {
                        UserId = adminUser.Id,
                        FirstName = "System",
                        LastName = "Administrator",
                        Email = "admin@okr.com",
                        Role = "Admin",
                        Position = "System Administrator",
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
        }
            }

            // Create default manager user if it doesn't exist
            var managerUser = await _userManager.FindByEmailAsync("manager@okr.com");
            if (managerUser == null)
            {
                managerUser = new ApplicationUser
                {
                    UserName = "manager@okr.com",
                    Email = "manager@okr.com",
                    EmailConfirmed = true,
                    FirstName = "Test",
                    LastName = "Manager"
                };

                var result = await _userManager.CreateAsync(managerUser, "Manager123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(managerUser, "Manager");
                    
                    // Create employee record
                    var employee = new Employee
                    {
                        UserId = managerUser.Id,
                        FirstName = "Test",
                        LastName = "Manager",
                        Email = "manager@okr.com",
                        Role = "Manager",
                        Position = "Team Manager",
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
        }
            }
        }
    }
}