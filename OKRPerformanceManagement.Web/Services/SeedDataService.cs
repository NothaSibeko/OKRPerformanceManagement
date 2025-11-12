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
            string[] roles = { "Administration", "Support_Systems Engineer", "Snr and Technical Team Leads", "Manager", "Consultant", "Admin", "HR" };

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
            // Get all roles
            var administrationRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Administration");
            var supportRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Support_Systems Engineer");
            var seniorRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Snr and Technical Team Leads");
            var managerRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Manager");
            var consultantRole = await _context.EmployeeRoles.FirstOrDefaultAsync(r => r.Name == "Consultant");

            // Update or Create Administration or Jnr Support Engineer Template
            if (administrationRole != null)
            {
                await UpdateOrCreateTemplateAsync("Administration", administrationRole, CreateAdministrationTemplateAsync);
            }

            // Update or Create Support_Systems Engineer Template
            if (supportRole != null)
            {
                await UpdateOrCreateTemplateAsync("Support_Systems Engineer", supportRole, CreateSupportEngineerTemplateAsync);
            }

            // Update or Create Snr and Technical Team Leads Template
            if (seniorRole != null)
            {
                await UpdateOrCreateTemplateAsync("Snr and Technical Team Leads", seniorRole, CreateSeniorTechLeadTemplateAsync);
            }

            // Update or Create Manager Template
            if (managerRole != null)
            {
                await UpdateOrCreateTemplateAsync("Manager", managerRole, CreateManagerTemplateAsync);
            }

            // Update or Create Consultant Template
            if (consultantRole != null)
            {
                await UpdateOrCreateTemplateAsync("Consultant", consultantRole, CreateConsultantTemplateAsync);
            }
        }

        private async Task UpdateOrCreateTemplateAsync(string roleName, EmployeeRole role, Func<EmployeeRole, Task> createTemplateFunc)
        {
            // Check if template exists
            var existingTemplate = await _context.OKRTemplates
                .FirstOrDefaultAsync(t => t.Role == roleName);

            if (existingTemplate != null)
            {
                // Template exists - delete its objectives and key results (safe to delete, they're not referenced)
                var objectives = await _context.OKRTemplateObjectives
                    .Where(o => o.OKRTemplateId == existingTemplate.Id)
                    .ToListAsync();
                
                foreach (var objective in objectives)
                {
                    var keyResults = await _context.OKRTemplateKeyResults
                        .Where(kr => kr.OKRTemplateObjectiveId == objective.Id)
                        .ToListAsync();
                    _context.OKRTemplateKeyResults.RemoveRange(keyResults);
                }
                _context.OKRTemplateObjectives.RemoveRange(objectives);
                await _context.SaveChangesAsync();

                // Update template properties
                existingTemplate.Name = GetTemplateName(roleName);
                existingTemplate.Description = GetTemplateDescription(roleName);
                existingTemplate.IsActive = true;
                existingTemplate.RoleId = role.Id;
                await _context.SaveChangesAsync();

                // Now recreate objectives and key results using the existing template
                await RecreateTemplateContentAsync(existingTemplate, role, createTemplateFunc);
            }
            else
            {
                // Template doesn't exist - create new one
                await createTemplateFunc(role);
            }
        }

        private string GetTemplateName(string roleName)
        {
            return roleName switch
            {
                "Administration" => "Administration or Jnr Support Engineer OKR Template",
                "Support_Systems Engineer" => "Support Systems Engineer OKR Template",
                "Snr and Technical Team Leads" => "Snr and Technical Team Leads OKR Template",
                "Manager" => "Managers OKR Template",
                "Consultant" => "Consultant OKR Template",
                _ => $"{roleName} OKR Template"
            };
        }

        private string GetTemplateDescription(string roleName)
        {
            return $"{GetTemplateName(roleName)} - FY 2025";
        }

        private async Task RecreateTemplateContentAsync(OKRTemplate template, EmployeeRole role, Func<EmployeeRole, Task> createTemplateFunc)
        {
            // Temporarily store the template ID
            var templateId = template.Id;
            
            // Create a temporary template to get the structure
            // We'll modify the create functions to accept an existing template ID
            // For now, let's use a different approach - call the create function but it will create a new template
            // So we need to modify the approach
            
            // Actually, better approach: extract the content creation logic
            // For simplicity, let's just delete the old template's content and call the create function
            // But the create function creates a new template, so we need to modify it
            
            // Simpler: Just call the appropriate create method but pass the existing template
            if (role.Name == "Administration")
            {
                await CreateAdministrationTemplateContentAsync(template);
            }
            else if (role.Name == "Support_Systems Engineer")
            {
                await CreateSupportEngineerTemplateContentAsync(template);
            }
            else if (role.Name == "Snr and Technical Team Leads")
            {
                await CreateSeniorTechLeadTemplateContentAsync(template);
            }
            else if (role.Name == "Manager")
            {
                await CreateManagerTemplateContentAsync(template);
            }
            else if (role.Name == "Consultant")
            {
                await CreateConsultantTemplateContentAsync(template);
            }
        }

        private async Task CreateAdministrationTemplateAsync(EmployeeRole role)
        {
            var template = new OKRTemplate
            {
                Name = "Administration or Jnr Support Engineer OKR Template",
                Role = "Administration",
                Description = "OKR template for Administration or Junior Support Engineer role - FY 2025",
                IsActive = true,
                CreatedDate = DateTime.Now,
                RoleId = role.Id
            };
            _context.OKRTemplates.Add(template);
            await _context.SaveChangesAsync();

            await CreateAdministrationTemplateContentAsync(template);
        }

        private async Task CreateAdministrationTemplateContentAsync(OKRTemplate template)
        {

            // Objective 1: Defend our existing installed base and recurring revenue (70%)
            var obj1 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Defend our existing installed base and recurring revenue with our existing client base",
                Weight = 70.00m,
                Description = "Maintain and protect existing client relationships and revenue streams",
                SortOrder = 1
            };
            _context.OKRTemplateObjectives.Add(obj1);
            await _context.SaveChangesAsync();

            // KR: Behaviour - Team Orientated (7.5%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Behaviour - Team Orientated",
                Target = "Team Orientated",
                Measure = "1 = Not a team player, various HR related issues\n2 = Passive in the environment with minimal team involvement\n3 = Consistently collaborates and shares updates/knowledge while being approachable and open to input.\n4 = Demonstrate changes suggested and implemented that have created a positive impact on the whole team/environment\n5 = Multiple changes proposed and implemented, having a direct impact on team optimization and possible financial benefit",
                Objectives = "Proven track record of being a team-oriented professional, willing to work closely with others to achieve goals. Willingness, Language and office ethics, Shares knowledge",
                MeasurementSource = "Communication between team members where you have demonstrated the ability to transfer knowledge and assist when required, HR records, BoE - Body of evidence",
                Weight = 7.50m,
                SortOrder = 1,
                Rating1Description = "Not a team player, various HR related issues",
                Rating2Description = "Passive in the environment with minimal team involvement",
                Rating3Description = "Consistently collaborates and shares updates/knowledge while being approachable and open to input.",
                Rating4Description = "Demonstrate changes suggested and implemented that have created a positive impact on the whole team/environment",
                Rating5Description = "Multiple changes proposed and implemented, having a direct impact on team optimization and possible financial benefit"
            });

            // KR: Leadership/Ownership (2.5%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Leadership/Ownership - Demonstrate leadership characteristics",
                Target = "Demonstrate leadership characteristics",
                Measure = "1 = Rarely takes initiative; occasionally identifies obvious issues but does not act on them\n2 = Occasionally takes responsibility but may not follow through to resolution.\n3 = Proactively seeks out and resolves potential problems with minimal guidance with positive client feedback.\n4 = Takes responsibility for complex issues, following up and ensuring thorough resolution that minimized business impact.\n5 = Showcase where you took ownership of issues across multiple technologies, which is over and above your current role requirements",
                Objectives = "Lead by example, Sacrifice own time to support business, Willingness, Honesty, Contribute to company values, Ownership",
                MeasurementSource = "Decision-Making, Accountability, Proactive Problem Solving, Team Influence, Delegation, Initiatives, RCA's, Award Nominations",
                Weight = 2.50m,
                SortOrder = 2,
                Rating1Description = "Rarely takes initiative; occasionally identifies obvious issues but does not act on them",
                Rating2Description = "Occasionally takes responsibility but may not follow through to resolution.",
                Rating3Description = "Proactively seeks out and resolves potential problems with minimal guidance with positive client feedback.",
                Rating4Description = "Takes responsibility for complex issues, following up and ensuring thorough resolution that minimized business impact.",
                Rating5Description = "Showcase where you took ownership of issues across multiple technologies, which is over and above your current role requirements"
            });

            // KR: Task Compliancy (25%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Task Compliancy",
                Target = "Plan 30% of time and complete on time",
                Measure = "1 = Frequently misses deadlines; tasks are often incomplete or improperly done. Struggles with planning, leading to inefficient task execution.\n2 = Completes tasks on time but frequently needs support or corrections (Less than 60 % of work completed on time).\n3 = Consistently completes tasks efficiently and on time. Delivers projects on time. (90% of planned work executed on time)\n4 = Exhibits outstanding time management, often completing tasks ahead of schedule without compromising quality. Demonstrates high reliability and consistently delivers work on time with positive feedback and recognition\n5 = Exemplifies dependability, consistently exceeding expectations in timely and reliable task completion. Demonstrates strong strategic planning skills, setting and achieving goals that support team and organizational success.",
                Objectives = "Have an engaged and value added team, Increase productivity and efficiency in the team, Focus on proactive work and analytics + reporting, Learn to plan and execute according to schedule, Delivering Projects on time",
                MeasurementSource = "Planning and execution list, Project involvement",
                Weight = 25.00m,
                SortOrder = 3,
                Rating1Description = "Frequently misses deadlines; tasks are often incomplete or improperly done. Struggles with planning, leading to inefficient task execution.",
                Rating2Description = "Completes tasks on time but frequently needs support or corrections (Less than 60 % of work completed on time).",
                Rating3Description = "Consistently completes tasks efficiently and on time. Delivers projects on time. (90% of planned work executed on time)",
                Rating4Description = "Exhibits outstanding time management, often completing tasks ahead of schedule without compromising quality. Demonstrates high reliability and consistently delivers work on time with positive feedback and recognition",
                Rating5Description = "Exemplifies dependability, consistently exceeding expectations in timely and reliable task completion. Demonstrates strong strategic planning skills, setting and achieving goals that support team and organizational success."
            });

            // KR: Contractual Compliance (25%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Contractual Compliance",
                Target = "Adhere to all client and ITIL processes to provide support to our end customers",
                Measure = "1 = Limited understanding of ITIL framework, requiring frequent guidance. Don't adhere to support requirements. Contract breach\n2 = Basic understanding of ITIL but inconsistently applies best practices resulting in support gap.\n3 = Consistently applies ITIL best practices effectively. Generally meets client expectations for compliance.\n4 = Demonstrate where a process/practice was changed to improve operations and service delivery with positive result\n5 = Due to your contract compliance and performance client gave recognition on multiple occasions.",
                Objectives = "Exceeding SLA compliancy, To be the differentiator at clients when choosing a preferred service provider, To set the bar for other service providers; they need to follow while we lead. Understand all the ITIL process and enforce compliancy",
                MeasurementSource = "SLA conformance and feedback; monthly reports, Client recognition, Not safety violations, Processes and procedures",
                Weight = 25.00m,
                SortOrder = 4,
                Rating1Description = "Limited understanding of ITIL framework, requiring frequent guidance. Don't adhere to support requirements. Contract breach",
                Rating2Description = "Basic understanding of ITIL but inconsistently applies best practices resulting in support gap.",
                Rating3Description = "Consistently applies ITIL best practices effectively. Generally meets client expectations for compliance.",
                Rating4Description = "Demonstrate where a process/practice was changed to improve operations and service delivery with positive result",
                Rating5Description = "Due to your contract compliance and performance client gave recognition on multiple occasions."
            });

            // KR: Documentation/Reporting and quality (5%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Documentation/Reporting and quality",
                Target = "Create quality processes/procedures and documentation",
                Measure = "1 = Frequently fails to create or update documentation as required, leading to knowledge gaps. Inaccurate reporting.\n2 = Occasionally reviews documentation but may miss occasional deadlines. Have to review documentation multiple times for the same quality issues.\n3 = Consistently reviews and updates documentation proactively, providing clear, professional, and well-structured writing/reporting.\n4 = Consistently provides accurate and detailed documentation, applies feedback and makes improvements.\n5 = Exemplary timeliness, with early or on-time completion of all documentation or monthly reports, actively supporting timely information flow without any updates or quality issues.",
                Objectives = "Percentage of documents rejected due to quality / compliance issues, Percentage of documents rejected by clients (for document control related reasons), Percentage of late documents, Average review time, Percentage of documents in the various statuses, Monthly Reporting/Health Review / Presentation as accurate and presentable",
                MeasurementSource = "Health Review Reporting, Technical Recovery Document, Works Instructions, Procedures, Presentations",
                Weight = 5.00m,
                SortOrder = 5,
                Rating1Description = "Frequently fails to create or update documentation as required, leading to knowledge gaps. Inaccurate reporting.",
                Rating2Description = "Occasionally reviews documentation but may miss occasional deadlines. Have to review documentation multiple times for the same quality issues.",
                Rating3Description = "Consistently reviews and updates documentation proactively, providing clear, professional, and well-structured writing/reporting.",
                Rating4Description = "Consistently provides accurate and detailed documentation, applies feedback and makes improvements.",
                Rating5Description = "Exemplary timeliness, with early or on-time completion of all documentation or monthly reports, actively supporting timely information flow without any updates or quality issues."
            });

            // KR: SHE Compliancy (5%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "SHE Compliancy",
                Target = "Have zero deviations/violations with regards to safety.",
                Measure = "1 = Had a SHE violation\n2 = Missed acceptance deadline of SHE policies and procedures.\n3 = 100% SHE Compliant and involved in monthly safety talks and presentations.\n4 = You are appointed in a SHE function within the BU and actively participating\n5 = You have identified and implemented an improvement related to SHE",
                Objectives = "Improve safety education, Ensure compliance with OHS guidelines, Foster proactive safety awareness, Safety Moment per quarter, Presented or compiled Safety presentation as part of monthly OHS presentations. 100% acknowledgment of OHS communications",
                MeasurementSource = "Incident reporting and tracking register, Safety Presentation, OHS Communication Acceptance, Safety Moments, Safety Role - Contribution",
                Weight = 5.00m,
                SortOrder = 6,
                Rating1Description = "Had a SHE violation",
                Rating2Description = "Missed acceptance deadline of SHE policies and procedures.",
                Rating3Description = "100% SHE Compliant and involved in monthly safety talks and presentations.",
                Rating4Description = "You are appointed in a SHE function within the BU and actively participating",
                Rating5Description = "You have identified and implemented an improvement related to SHE"
            });

            // Objective 2: Increase the share of wallet (15%)
            var obj2 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Increase the share of wallet from our existing customers with new products and solutions.",
                Weight = 15.00m,
                Description = "Expand revenue from existing customers through new products and solutions",
                SortOrder = 2
            };
            _context.OKRTemplateObjectives.Add(obj2);
            await _context.SaveChangesAsync();

            // KR: Continuous Service Improvements (0%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Continuous Service Improvements (not a request from the client)",
                Target = "CSI's that are recommend are implemented and add value to the customer/client with revenue generated",
                Measure = "1 = No CSI documented or implemented\n2 = CSI was documented but not approved for implementation\n3 = A CSI was documented and implemented\n4 = CSI's approved and implemented adding revenue over R 150 000\n5 = CSI's approved and implemented adding revenue over R 250 000",
                Objectives = "Create a culture where all team members are actively participating in CSI's/Innovation, Develop analytical skills, Develop presentation skills, Understanding cost benefit analysis",
                MeasurementSource = "CSI Register, Customer success stories",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No CSI documented or implemented",
                Rating2Description = "CSI was documented but not approved for implementation",
                Rating3Description = "A CSI was documented and implemented",
                Rating4Description = "CSI's approved and implemented adding revenue over R 150 000",
                Rating5Description = "CSI's approved and implemented adding revenue over R 250 000"
            });

            // KR: Improvements + Opportunities (5%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Improvements + Opportunities (Scope Increase)",
                Target = "The implementation of new opportunities and improvements",
                Measure = "1 = No new improvements or opportunities identified\n2 = Improvements or opportunities identified but not implemented\n3 = Multiple improvements identified and implemented or opportunities that have become projects\n4 = Improvement implemented with customer recognition, or projects/services that result in revenue of over R 150 000\n5 = More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000",
                Objectives = "Identify opportunities that become projects (revenue generating), Identify opportunities that add value to business (\"mini CSI\")",
                MeasurementSource = "Improvements and opportunities tracking register",
                Weight = 5.00m,
                SortOrder = 2,
                Rating1Description = "No new improvements or opportunities identified",
                Rating2Description = "Improvements or opportunities identified but not implemented",
                Rating3Description = "Multiple improvements identified and implemented or opportunities that have become projects",
                Rating4Description = "Improvement implemented with customer recognition, or projects/services that result in revenue of over R 150 000",
                Rating5Description = "More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000"
            });

            // KR: Revenue Increase (10%)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Revenue Increase",
                Target = "Additional revenue generated via project implementation and monthly service growth",
                Measure = "1 = Revenue decrease on service/contract and no additional project work\n2 = 0% revenue increase on contract/services\n3 = 10% of annual contract value increase with projects\n4 = 15% of annual contract value increase with projects\n5 = 20% of annual contract value increase with projects",
                Objectives = "All projects implemented to generate revenue and add value to the customer, Learn project management skills, Follow RFS/RFQ process (commercial process), Grow the business",
                MeasurementSource = "Project tracking list and expansion of services",
                Weight = 10.00m,
                SortOrder = 3,
                Rating1Description = "Revenue decrease on service/contract and no additional project work",
                Rating2Description = "0% revenue increase on contract/services",
                Rating3Description = "10% of annual contract value increase with projects",
                Rating4Description = "15% of annual contract value increase with projects",
                Rating5Description = "20% of annual contract value increase with projects"
            });

            // Objective 3: Grow new customers (0%)
            var obj3 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Grow new customers through use of commercial models, marketing and promotional campaigns, CXC, etc",
                Weight = 0.00m,
                Description = "Expand customer base through marketing and commercial initiatives",
                SortOrder = 3
            };
            _context.OKRTemplateObjectives.Add(obj3);
            await _context.SaveChangesAsync();

            // KR: CXC - Top 5 customers
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj3.Id,
                Name = "CXC - Top 5 customers for CSI3",
                Target = "Resolve customer problems by understanding their pains and gains",
                Measure = "1 = No new product or service proposed\n2 = Product/Solution in testing phase\n3 = 1 New product/solution added to CSI3 CXC\n4 = 1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3\n5 = 2 or more implementations at customers for products/solutions presented by CSI3",
                Objectives = "Expansion of the MES services for existing and new customers, Participate in marketing and promotional campaigns",
                MeasurementSource = "Frequent engagement with stakeholders with next steps driven and resolve, Record of implementations, Record of customers in the CXC and/or other events",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No new product or service proposed",
                Rating2Description = "Product/Solution in testing phase",
                Rating3Description = "1 New product/solution added to CSI3 CXC",
                Rating4Description = "1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3",
                Rating5Description = "2 or more implementations at customers for products/solutions presented by CSI3"
            });

            // Objective 4: Internationalise (0%)
            var obj4 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Internationalise our business coverage in new markets outside of South Africa.",
                Weight = 0.00m,
                Description = "Expand business operations internationally",
                SortOrder = 4
            };
            _context.OKRTemplateObjectives.Add(obj4);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj4.Id,
                Name = "Internationalise our business",
                Target = "Increase footprint of support for CSI3 outside South Africa",
                Measure = "1 = No new business outside SA\n2 = Engagements took place for opportunities, but no additional business\n3 = Obtained an international contract for support/project of more than R 250 000\n4 = Obtained an international contract for support/project of more than R 750 000\n5 = Obtained an international contract for support/project of more than R 1 000 000",
                Objectives = "To create additional revenue stream, To create opportunities for existing team members that might want to immigrate/travel oversea",
                MeasurementSource = "Monthly BU Review",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No new business outside SA",
                Rating2Description = "Engagements took place for opportunities, but no additional business",
                Rating3Description = "Obtained an international contract for support/project of more than R 250 000",
                Rating4Description = "Obtained an international contract for support/project of more than R 750 000",
                Rating5Description = "Obtained an international contract for support/project of more than R 1 000 000"
            });

            // Objective 5: Target new customers (0%)
            var obj5 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Target specific new identified customers.",
                Weight = 0.00m,
                Description = "Onboard new customers to expand business",
                SortOrder = 5
            };
            _context.OKRTemplateObjectives.Add(obj5);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj5.Id,
                Name = "Onboard new customers",
                Target = "Expansion of CSI3 business via new customer base",
                Measure = "",
                Objectives = "",
                MeasurementSource = "",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No new opportunities from new customers",
                Rating2Description = "Engagement and new opportunities list, but no revenue",
                Rating3Description = "1 New customer onboarded for a project/service",
                Rating4Description = "More than 1 customer onboarded for new projects/services",
                Rating5Description = "New customers onboarded with revenue of more than R 500 000"
            });

            // Objective 6: Accelerate growth (15%)
            var obj6 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Accelerate growth of our community through our partner management programme.",
                Weight = 15.00m,
                Description = "Develop team members through training and certification",
                SortOrder = 6
            };
            _context.OKRTemplateObjectives.Add(obj6);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj6.Id,
                Name = "Self Development and Growth",
                Target = "Courses and certification done to improve operational/technical/soft skills",
                Measure = "1 = No IDP in place\n2 = IDP in place but no courses or training completed\n3 = IDP in place and all plan courses completed\n4 = IDP in place and all plan courses completed, with additional training completed that was approved by line management\n5 = Degree/Diploma obtained and/or technical certification that generates additional revenue",
                Objectives = "Self development, Set the example for the team to grow, Course/Knowledge obtained to be applicable in the work place/career path",
                MeasurementSource = "CDF and employee document library + LinkedIn Learning, IDP + Skill Matrix",
                Weight = 15.00m,
                SortOrder = 1,
                Rating1Description = "No IDP in place",
                Rating2Description = "IDP in place but no courses or training completed",
                Rating3Description = "IDP in place and all plan courses completed",
                Rating4Description = "IDP in place and all plan courses completed, with additional training completed that was approved by line management",
                Rating5Description = "Degree/Diploma obtained and/or technical certification that generates additional revenue"
            });

            // Objective 7: Leverage CIE portfolio (0%)
            var obj7 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Leverage the full portfolio of CIE",
                Weight = 0.00m,
                Description = "Generate revenue from other CIE business units",
                SortOrder = 7
            };
            _context.OKRTemplateObjectives.Add(obj7);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj7.Id,
                Name = "CIE Portfolio revenue",
                Target = "Additional revenue generated from key CSI3 customers for other CIE BU's",
                Measure = "1 = No introduction to customers of other BU's\n2 = Introduction of other BU's done, but no additional revenue generated.\n3 = New contracts/services/projects for other BU's that generates revenue.\n4 = Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement\n5 = Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement",
                Objectives = "To understand service offering from other BU, To present CIE as a company; not having BU's work in silo's, Improve inter BU relationship, Work together as a team to improve revenue targets",
                MeasurementSource = "Financial Information, New contracts/projects in DI",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No introduction to customers of other BU's",
                Rating2Description = "Introduction of other BU's done, but no additional revenue generated.",
                Rating3Description = "New contracts/services/projects for other BU's that generates revenue.",
                Rating4Description = "Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement",
                Rating5Description = "Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement"
            });

            await _context.SaveChangesAsync();
        }

        private async Task CreateSupportEngineerTemplateAsync(EmployeeRole role)
        {
            var template = new OKRTemplate
            {
                Name = "Support Systems Engineer OKR Template",
                Role = "Support_Systems Engineer",
                Description = "OKR template for Support/Systems Engineer role - FY 2025",
                IsActive = true,
                CreatedDate = DateTime.Now,
                RoleId = role.Id
            };
            _context.OKRTemplates.Add(template);
            await _context.SaveChangesAsync();

            await CreateSupportEngineerTemplateContentAsync(template);
        }

        private async Task CreateSupportEngineerTemplateContentAsync(OKRTemplate template)
        {
            // Objective 1: Defend existing base (65%)
            var obj1 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Defend our existing installed base and recurring revenue with our existing client base",
                Weight = 65.00m,
                Description = "Maintain and protect existing client relationships and revenue streams",
                SortOrder = 1
            };
            _context.OKRTemplateObjectives.Add(obj1);
            await _context.SaveChangesAsync();

            // Add all Key Results for Objective 1 (same structure as Administration but different weights)
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Behaviour - Team Orientated",
                Target = "Team Orientated",
                Measure = "1 = Not a team player, various HR related issues\n2 = Passive in the environment with minimal team involvement\n3 = Consistently collaborates and shares updates/knowledge while being approachable and open to input.\n4 = Demonstrate changes suggested and implemented that have created a positive impact on the whole team/environment\n5 = Multiple changes proposed and implemented, having a direct impact on team optimization and possible financial benefit",
                Objectives = "Proven track record of being a team-oriented professional, willing to work closely with others to achieve goals. Willingness, Language and office ethics, Shares knowledge",
                MeasurementSource = "Communication between team members where you have demonstrated the ability to transfer knowledge and assist when required, HR records, BoE - Body of evidence",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "Not a team player, various HR related issues",
                Rating2Description = "Passive in the environment with minimal team involvement",
                Rating3Description = "Consistently collaborates and shares updates/knowledge while being approachable and open to input.",
                Rating4Description = "Demonstrate changes suggested and implemented that have created a positive impact on the whole team/environment",
                Rating5Description = "Multiple changes proposed and implemented, having a direct impact on team optimization and possible financial benefit"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Leadership/Ownership - Demonstrate leadership characteristics",
                Target = "Demonstrate leadership characteristics",
                Measure = "1 = Rarely takes initiative; occasionally identifies obvious issues but does not act on them\n2 = Occasionally takes responsibility but may not follow through to resolution.\n3 = Proactively seeks out and resolves potential problems with minimal guidance with positive client feedback.\n4 = Takes responsibility for complex issues, following up and ensuring thorough resolution that minimized business impact.\n5 = Showcase where you took ownership of issues across multiple technologies, which is over and above your current role requirements",
                Objectives = "Lead by example, Sacrifice own time to support business, Willingness, Honesty, Contribute to company values, Ownership",
                MeasurementSource = "Decision-Making, Accountability, Proactive Problem Solving, Team Influence, Delegation, Initiatives, RCA's, Award Nominations",
                Weight = 5.00m,
                SortOrder = 2,
                Rating1Description = "Rarely takes initiative; occasionally identifies obvious issues but does not act on them",
                Rating2Description = "Occasionally takes responsibility but may not follow through to resolution.",
                Rating3Description = "Proactively seeks out and resolves potential problems with minimal guidance with positive client feedback.",
                Rating4Description = "Takes responsibility for complex issues, following up and ensuring thorough resolution that minimized business impact.",
                Rating5Description = "Showcase where you took ownership of issues across multiple technologies, which is over and above your current role requirements"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Task Compliancy",
                Target = "Plan 30% of time and complete on time",
                Measure = "1 = Frequently misses deadlines; tasks are often incomplete or improperly done. Struggles with planning, leading to inefficient task execution.\n2 = Completes tasks on time but frequently needs support or corrections (Less than 60 % of work completed on time).\n3 = Consistently completes tasks efficiently and on time. Delivers projects on time. (90% of planned work executed on time)\n4 = Exhibits outstanding time management, often completing tasks ahead of schedule without compromising quality. Demonstrates high reliability and consistently delivers work on time with positive feedback and recognition\n5 = Exemplifies dependability, consistently exceeding expectations in timely and reliable task completion. Demonstrates strong strategic planning skills, setting and achieving goals that support team and organizational success.",
                Objectives = "Have an engaged and value added team, Increase productivity and efficiency in the team, Focus on proactive work and analytics + reporting, Learn to plan and execute according to schedule, Delivering Projects on time",
                MeasurementSource = "Planning and execution list, Project involvement",
                Weight = 20.00m,
                SortOrder = 3,
                Rating1Description = "Frequently misses deadlines; tasks are often incomplete or improperly done. Struggles with planning, leading to inefficient task execution.",
                Rating2Description = "Completes tasks on time but frequently needs support or corrections (Less than 60 % of work completed on time).",
                Rating3Description = "Consistently completes tasks efficiently and on time. Delivers projects on time. (90% of planned work executed on time)",
                Rating4Description = "Exhibits outstanding time management, often completing tasks ahead of schedule without compromising quality. Demonstrates high reliability and consistently delivers work on time with positive feedback and recognition",
                Rating5Description = "Exemplifies dependability, consistently exceeding expectations in timely and reliable task completion. Demonstrates strong strategic planning skills, setting and achieving goals that support team and organizational success."
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Contractual Compliance",
                Target = "Adhere to all client and ITIL processes to provide support to our end customers",
                Measure = "1 = Limited understanding of ITIL framework, requiring frequent guidance. Don't adhere to support requirements. Contract breach\n2 = Basic understanding of ITIL but inconsistently applies best practices resulting in support gap.\n3 = Consistently applies ITIL best practices effectively. Generally meets client expectations for compliance.\n4 = Demonstrate where a process/practice was changed to improve operations and service delivery with positive result\n5 = Due to your contract compliance and performance client gave recognition on multiple occasions.",
                Objectives = "Exceeding SLA compliancy, To be the differentiator at clients when choosing a preferred service provider, To set the bar for other service providers; they need to follow while we lead. Understand all the ITIL process and enforce compliancy",
                MeasurementSource = "SLA conformance and feedback; monthly reports, Client recognition, Not safety violations, Processes and procedures",
                Weight = 20.00m,
                SortOrder = 4,
                Rating1Description = "Limited understanding of ITIL framework, requiring frequent guidance. Don't adhere to support requirements. Contract breach",
                Rating2Description = "Basic understanding of ITIL but inconsistently applies best practices resulting in support gap.",
                Rating3Description = "Consistently applies ITIL best practices effectively. Generally meets client expectations for compliance.",
                Rating4Description = "Demonstrate where a process/practice was changed to improve operations and service delivery with positive result",
                Rating5Description = "Due to your contract compliance and performance client gave recognition on multiple occasions."
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "Documentation/Reporting and quality",
                Target = "Create quality processes/procedures and documentation",
                Measure = "1 = Frequently fails to create or update documentation as required, leading to knowledge gaps. Inaccurate reporting.\n2 = Occasionally reviews documentation but may miss occasional deadlines. Have to review documentation multiple times for the same quality issues.\n3 = Consistently reviews and updates documentation proactively, providing clear, professional, and well-structured writing/reporting.\n4 = Consistently provides accurate and detailed documentation, applies feedback and makes improvements.\n5 = Exemplary timeliness, with early or on-time completion of all documentation or monthly reports, actively supporting timely information flow without any updates or quality issues.",
                Objectives = "Percentage of documents rejected due to quality / compliance issues, Percentage of documents rejected by clients (for document control related reasons), Percentage of late documents, Average review time, Percentage of documents in the various statuses, Monthly Reporting/Health Review / Presentation as accurate and presentable",
                MeasurementSource = "Health Review Reporting, Technical Recovery Document, Works Instructions, Procedures, Presentations",
                Weight = 10.00m,
                SortOrder = 5,
                Rating1Description = "Frequently fails to create or update documentation as required, leading to knowledge gaps. Inaccurate reporting.",
                Rating2Description = "Occasionally reviews documentation but may miss occasional deadlines. Have to review documentation multiple times for the same quality issues.",
                Rating3Description = "Consistently reviews and updates documentation proactively, providing clear, professional, and well-structured writing/reporting.",
                Rating4Description = "Consistently provides accurate and detailed documentation, applies feedback and makes improvements.",
                Rating5Description = "Exemplary timeliness, with early or on-time completion of all documentation or monthly reports, actively supporting timely information flow without any updates or quality issues."
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj1.Id,
                Name = "SHE Compliancy",
                Target = "Have zero deviations/violations with regards to safety.",
                Measure = "1 = Had a SHE violation\n2 = Missed acceptance deadline of SHE policies and procedures.\n3 = 100% SHE Compliant and involved in monthly safety talks and presentations.\n4 = You are appointed in a SHE function within the BU and actively participating\n5 = You have identified and implemented an improvement related to SHE",
                Objectives = "Improve safety education, Ensure compliance with OHS guidelines, Foster proactive safety awareness, Safety Moment per quarter, Presented or compiled Safety presentation as part of monthly OHS presentations. 100% acknowledgment of OHS communications",
                MeasurementSource = "Incident reporting and tracking register, Safety Presentation, OHS Communication Acceptance, Safety Moments, Safety Role - Contribution",
                Weight = 5.00m,
                SortOrder = 6,
                Rating1Description = "Had a SHE violation",
                Rating2Description = "Missed acceptance deadline of SHE policies and procedures.",
                Rating3Description = "100% SHE Compliant and involved in monthly safety talks and presentations.",
                Rating4Description = "You are appointed in a SHE function within the BU and actively participating",
                Rating5Description = "You have identified and implemented an improvement related to SHE"
            });

            // Objective 2: Increase share of wallet (20%)
            var obj2 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Increase the share of wallet from our existing customers with new products and solutions.",
                Weight = 20.00m,
                Description = "Expand revenue from existing customers through new products and solutions",
                SortOrder = 2
            };
            _context.OKRTemplateObjectives.Add(obj2);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Continuous Service Improvements (not a request from the client)",
                Target = "CSI's that are recommend are implemented and add value to the customer/client with revenue generated",
                Measure = "1 = No CSI documented or implemented\n2 = CSI was documented but not approved for implementation\n3 = A CSI was documented and implemented\n4 = CSI's approved and implemented adding revenue over R 250 000\n5 = CSI's approved and implemented adding revenue over R 450 000",
                Objectives = "Create a culture where all team members are actively participating in CSI's/Innovation, Develop analytical skills, Develop presentation skills, Understanding cost benefit analysis",
                MeasurementSource = "CSI Register, Customer success stories",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No CSI documented or implemented",
                Rating2Description = "CSI was documented but not approved for implementation",
                Rating3Description = "A CSI was documented and implemented",
                Rating4Description = "CSI's approved and implemented adding revenue over R 250 000",
                Rating5Description = "CSI's approved and implemented adding revenue over R 450 000"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Improvements + Opportunities (Scope Increase)",
                Target = "The implementation of new opportunities and improvements",
                Measure = "1 = No new improvements or opportunities identified\n2 = Improvements or opportunities identified but not implemented\n3 = Multiple improvements identified and implemented or opportunities that have become projects\n4 = Improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000\n5 = More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 450 000",
                Objectives = "Identify opportunities that become projects (revenue generating), Identify opportunities that add value to business (\"mini CSI\")",
                MeasurementSource = "Improvements and opportunities tracking register",
                Weight = 5.00m,
                SortOrder = 2,
                Rating1Description = "No new improvements or opportunities identified",
                Rating2Description = "Improvements or opportunities identified but not implemented",
                Rating3Description = "Multiple improvements identified and implemented or opportunities that have become projects",
                Rating4Description = "Improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000",
                Rating5Description = "More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 450 000"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Revenue Increase",
                Target = "Additional revenue generated via project implementation and monthly service growth",
                Measure = "1 = Revenue decrease on service/contract and no additional project work\n2 = 0% revenue increase on contract/services\n3 = 10% of annual contract value increase with projects\n4 = 15% of annual contract value increase with projects\n5 = 20% of annual contract value increase with projects",
                Objectives = "All projects implemented to generate revenue and add value to the customer, Learn project management skills, Follow RFS/RFQ process (commercial process), Grow the business",
                MeasurementSource = "Project tracking list and expansion of services",
                Weight = 10.00m,
                SortOrder = 3,
                Rating1Description = "Revenue decrease on service/contract and no additional project work",
                Rating2Description = "0% revenue increase on contract/services",
                Rating3Description = "10% of annual contract value increase with projects",
                Rating4Description = "15% of annual contract value increase with projects",
                Rating5Description = "20% of annual contract value increase with projects"
            });

            // Objective 3-7: Same structure as Administration (0% weight objectives and 15% Self Development)
            await AddCommonObjectivesAsync(template, 0.00m, 15.00m, 3);
            await _context.SaveChangesAsync();
        }

        private async Task CreateSeniorTechLeadTemplateAsync(EmployeeRole role)
        {
            var template = new OKRTemplate
            {
                Name = "Snr and Technical Team Leads OKR Template",
                Role = "Snr and Technical Team Leads",
                Description = "OKR template for Senior and Technical Team Leads role - FY 2025",
                IsActive = true,
                CreatedDate = DateTime.Now,
                RoleId = role.Id
            };
            _context.OKRTemplates.Add(template);
            await _context.SaveChangesAsync();

            await CreateSeniorTechLeadTemplateContentAsync(template);
        }

        private async Task CreateSeniorTechLeadTemplateContentAsync(OKRTemplate template)
        {
            // Objective 1: Defend existing base (65%)
            var obj1 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Defend our existing installed base and recurring revenue with our existing client base",
                Weight = 65.00m,
                Description = "Maintain and protect existing client relationships and revenue streams",
                SortOrder = 1
            };
            _context.OKRTemplateObjectives.Add(obj1);
            await _context.SaveChangesAsync();

            // Add Key Results with weights: 5%, 5%, 20%, 15%, 15%, 5%
            await AddDefendBaseKeyResultsAsync(obj1.Id, 5.00m, 5.00m, 20.00m, 15.00m, 15.00m, 5.00m);

            // Objective 2: Increase share of wallet (20%)
            var obj2 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Increase the share of wallet from our existing customers with new products and solutions.",
                Weight = 20.00m,
                Description = "Expand revenue from existing customers through new products and solutions",
                SortOrder = 2
            };
            _context.OKRTemplateObjectives.Add(obj2);
            await _context.SaveChangesAsync();

            await AddShareOfWalletKeyResultsAsync(obj2.Id, 5.00m, 5.00m, 10.00m);

            // Objective 3-7: Common objectives
            await AddCommonObjectivesAsync(template, 0.00m, 10.00m, 3);
            
            // Objective 7: CIE Portfolio (5%)
            var obj7 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Leverage the full portfolio of CIE",
                Weight = 5.00m,
                Description = "Generate revenue from other CIE business units",
                SortOrder = 7
            };
            _context.OKRTemplateObjectives.Add(obj7);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj7.Id,
                Name = "CIE Portfolio revenue",
                Target = "Additional revenue generated from key CSI3 customers for other CIE BU's",
                Measure = "1 = No introduction to customers of other BU's\n2 = Introduction of other BU's done, but no additional revenue generated.\n3 = New contracts/services/projects for other BU's that generates revenue.\n4 = Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement\n5 = Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement",
                Objectives = "To understand service offering from other BU, To present CIE as a company; not having BU's work in silo's, Improve inter BU relationship, Work together as a team to improve revenue targets",
                MeasurementSource = "Financial Information, New contracts/projects in DI",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No introduction to customers of other BU's",
                Rating2Description = "Introduction of other BU's done, but no additional revenue generated.",
                Rating3Description = "New contracts/services/projects for other BU's that generates revenue.",
                Rating4Description = "Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement",
                Rating5Description = "Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement"
            });

            await _context.SaveChangesAsync();
        }

        private async Task CreateManagerTemplateAsync(EmployeeRole role)
        {
            var template = new OKRTemplate
            {
                Name = "Managers OKR Template",
                Role = "Manager",
                Description = "OKR template for Manager role - FY 2025",
                IsActive = true,
                CreatedDate = DateTime.Now,
                RoleId = role.Id
            };
            _context.OKRTemplates.Add(template);
            await _context.SaveChangesAsync();

            await CreateManagerTemplateContentAsync(template);
        }

        private async Task CreateManagerTemplateContentAsync(OKRTemplate template)
        {
            // Objective 1: Defend existing base (55%)
            var obj1 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Defend our existing installed base and recurring revenue with our existing client base",
                Weight = 55.00m,
                Description = "Maintain and protect existing client relationships and revenue streams",
                SortOrder = 1
            };
            _context.OKRTemplateObjectives.Add(obj1);
            await _context.SaveChangesAsync();

            // Add Key Results with weights: 5%, 5%, 15%, 12.5%, 12.5%, 5%
            await AddDefendBaseKeyResultsAsync(obj1.Id, 5.00m, 5.00m, 15.00m, 12.50m, 12.50m, 5.00m);

            // Objective 2: Increase share of wallet (20%)
            var obj2 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Increase the share of wallet from our existing customers with new products and solutions.",
                Weight = 20.00m,
                Description = "Expand revenue from existing customers through new products and solutions",
                SortOrder = 2
            };
            _context.OKRTemplateObjectives.Add(obj2);
            await _context.SaveChangesAsync();

            await AddShareOfWalletKeyResultsAsync(obj2.Id, 5.00m, 5.00m, 10.00m);

            // Objective 3: Grow new customers (5%)
            var obj3 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Grow new customers through use of commercial models, marketing and promotional campaigns, CXC, etc",
                Weight = 5.00m,
                Description = "Expand customer base through marketing and commercial initiatives",
                SortOrder = 3
            };
            _context.OKRTemplateObjectives.Add(obj3);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj3.Id,
                Name = "CXC - Top 5 customers for CSI3",
                Target = "Resolve customer problems by understanding their pains and gains",
                Measure = "1 = No new product or service proposed\n2 = Product/Solution in testing phase\n3 = 1 New product/solution added to CSI3 CXC\n4 = 1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3\n5 = 2 or more implementations at customers for products/solutions presented by CSI3",
                Objectives = "Expansion of the MES services for existing and new customers, Participate in marketing and promotional campaigns",
                MeasurementSource = "Frequent engagement with stakeholders with next steps driven and resolve, Record of implementations, Record of customers in the CXC and/or other events",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No new product or service proposed",
                Rating2Description = "Product/Solution in testing phase",
                Rating3Description = "1 New product/solution added to CSI3 CXC",
                Rating4Description = "1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3",
                Rating5Description = "2 or more implementations at customers for products/solutions presented by CSI3"
            });

            // Objective 4: Internationalise (0%)
            var obj4 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Internationalise our business coverage in new markets outside of South Africa.",
                Weight = 0.00m,
                Description = "Expand business operations internationally",
                SortOrder = 4
            };
            _context.OKRTemplateObjectives.Add(obj4);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj4.Id,
                Name = "Internationalise our business",
                Target = "Increase footprint of support for CSI3 outside South Africa",
                Measure = "1 = No new business outside SA\n2 = Engagements took place for opportunities, but no additional business\n3 = Obtained an international contract for support/project of more than R 250 000\n4 = Obtained an international contract for support/project of more than R 750 000\n5 = Obtained an international contract for support/project of more than R 1 000 000",
                Objectives = "To create additional revenue stream, To create opportunities for existing team members that might want to immigrate/travel oversea",
                MeasurementSource = "Monthly BU Review",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No new business outside SA",
                Rating2Description = "Engagements took place for opportunities, but no additional business",
                Rating3Description = "Obtained an international contract for support/project of more than R 250 000",
                Rating4Description = "Obtained an international contract for support/project of more than R 750 000",
                Rating5Description = "Obtained an international contract for support/project of more than R 1 000 000"
            });

            // Objective 5: Target new customers (5%)
            var obj5 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Target specific new identified customers.",
                Weight = 5.00m,
                Description = "Onboard new customers to expand business",
                SortOrder = 5
            };
            _context.OKRTemplateObjectives.Add(obj5);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj5.Id,
                Name = "Onboard new customers",
                Target = "Expansion of CSI3 business via new customer base",
                Measure = "1 = No new opportunities from new customers\n2 = Engagement and new opportunities list, but no revenue\n3 = 1 New customer onboarded for a a project/service\n4 = More than 1 customer onboarded for new projects/services\n5 = New customers onboarded with revenue of more than R 500 000",
                Objectives = "To create additonal revenue stream, Expansion of the business, Growth mindset, Do market research",
                MeasurementSource = "Onboarding process of new clients",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No new opportunities from new customers",
                Rating2Description = "Engagement and new opportunities list, but no revenue",
                Rating3Description = "1 New customer onboarded for a a project/service",
                Rating4Description = "More than 1 customer onboarded for new projects/services",
                Rating5Description = "New customers onboarded with revenue of more than R 500 000"
            });

            // Objective 6: Accelerate growth (10%)
            var obj6 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Accelerate growth of our community through our partner management programme.",
                Weight = 10.00m,
                Description = "Develop team members through training and certification",
                SortOrder = 6
            };
            _context.OKRTemplateObjectives.Add(obj6);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj6.Id,
                Name = "Self Development and Growth",
                Target = "Courses and certification done to improve operational/technical/soft skills",
                Measure = "1 = No IDP in place\n2 = IDP in place but no courses or training completed\n3 = IDP in place and all plan courses completed\n4 = IDP in place and all plan courses completed, with additional training completed that was approved by line management\n5 = Degree/Diploma obtained and/or technical certification that generates additional revenue",
                Objectives = "Self development, Set the example for the team to grow, Course/Knowledge obtained to be applicable in the work place/career path",
                MeasurementSource = "CDF and employee document library + LinkedIn Learning, IDP + Skill Matrix",
                Weight = 10.00m,
                SortOrder = 1,
                Rating1Description = "No IDP in place",
                Rating2Description = "IDP in place but no courses or training completed",
                Rating3Description = "IDP in place and all plan courses completed",
                Rating4Description = "IDP in place and all plan courses completed, with additional training completed that was approved by line management",
                Rating5Description = "Degree/Diploma obtained and/or technical certification that generates additional revenue"
            });

            // Objective 7: Leverage CIE portfolio (5%)
            var obj7 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Leverage the full portfolio of CIE",
                Weight = 5.00m,
                Description = "Generate revenue from other CIE business units",
                SortOrder = 7
            };
            _context.OKRTemplateObjectives.Add(obj7);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj7.Id,
                Name = "CIE Portfolio revenue",
                Target = "Additional revenue generated from key CSI3 customers for other CIE BU's",
                Measure = "1 = No introduction to customers of other BU's\n2 = Introduction of other BU's done, but no additional revenue generated.\n3 = New contracts/services/projects for other BU's that generates revenue.\n4 = Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement\n5 = Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement",
                Objectives = "To understand service offering from other BU, To present CIE as a company; not having BU's work in silo's, Improve inter BU relationship, Work together as a team to improve revenue targets",
                MeasurementSource = "Financial Information, New contracts/projects in DI",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No introduction to customers of other BU's",
                Rating2Description = "Introduction of other BU's done, but no additional revenue generated.",
                Rating3Description = "New contracts/services/projects for other BU's that generates revenue.",
                Rating4Description = "Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement",
                Rating5Description = "Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement"
            });

            await _context.SaveChangesAsync();
        }

        private async Task CreateConsultantTemplateAsync(EmployeeRole role)
        {
            var template = new OKRTemplate
            {
                Name = "Consultant OKR Template",
                Role = "Consultant",
                Description = "OKR template for Consultant role - FY 2025",
                IsActive = true,
                CreatedDate = DateTime.Now,
                RoleId = role.Id
            };
            _context.OKRTemplates.Add(template);
            await _context.SaveChangesAsync();

            await CreateConsultantTemplateContentAsync(template);
        }

        private async Task CreateConsultantTemplateContentAsync(OKRTemplate template)
        {
            // Objective 1: Defend existing base (55%)
            var obj1 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Defend our existing installed base and recurring revenue with our existing client base",
                Weight = 55.00m,
                Description = "Maintain and protect existing client relationships and revenue streams",
                SortOrder = 1
            };
            _context.OKRTemplateObjectives.Add(obj1);
            await _context.SaveChangesAsync();

            // Add Key Results with weights: 5%, 7.5%, 15%, 7.5%, 15%, 5%
            await AddDefendBaseKeyResultsAsync(obj1.Id, 5.00m, 7.50m, 15.00m, 7.50m, 15.00m, 5.00m);

            // Objective 2: Increase share of wallet (20% but only 12% weighted)
            var obj2 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Increase the share of wallet from our existing customers with new products and solutions.",
                Weight = 20.00m,
                Description = "Expand revenue from existing customers through new products and solutions",
                SortOrder = 2
            };
            _context.OKRTemplateObjectives.Add(obj2);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Continuous Service Improvements (not a request from the client)",
                Target = "CSI's that are recommend are implemented and add value to the customer/client with revenue generated",
                Measure = "1 = No CSI documented or implemented\n2 = CSI was documented but not approved for implementation\n3 = A CSI was documented and implemented\n4 = CSI's approved and implemented adding revenue over R 250 000\n5 = CSI's approved and implemented adding revenue over R 450 000",
                Objectives = "Create a culture where all team members are actively participating in CSI's/Innovation, Develop analytical skills, Develop presentation skills, Understanding cost benefit analysis",
                MeasurementSource = "CSI Register, Customer success stories",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No CSI documented or implemented",
                Rating2Description = "CSI was documented but not approved for implementation",
                Rating3Description = "A CSI was documented and implemented",
                Rating4Description = "CSI's approved and implemented adding revenue over R 250 000",
                Rating5Description = "CSI's approved and implemented adding revenue over R 450 000"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Improvements + Opportunities (Scope Increase)",
                Target = "The implementation of new opportunities and improvements",
                Measure = "1 = No new improvements or opportunities identified\n2 = Improvements or opportunities identified but not implemented\n3 = Multiple improvements identified and implemented or opportunities that have become projects\n4 = Improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000\n5 = More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 450 000",
                Objectives = "Identify opportunities that become projects (revenue generating), Identify opportunities that add value to business (\"mini CSI\")",
                MeasurementSource = "Improvements and opportunities tracking register",
                Weight = 5.00m,
                SortOrder = 2,
                Rating1Description = "No new improvements or opportunities identified",
                Rating2Description = "Improvements or opportunities identified but not implemented",
                Rating3Description = "Multiple improvements identified and implemented or opportunities that have become projects",
                Rating4Description = "Improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000",
                Rating5Description = "More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 450 000"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj2.Id,
                Name = "Revenue Increase",
                Target = "Additional revenue generated via project implementation and monthly service growth",
                Measure = "1 = Revenue decrease on service/contract and no additional project work\n2 = 0% revenue increase on contract/services\n3 = 10% of annual contract value increase with projects\n4 = 15% of annual contract value increase with projects\n5 = 20% of annual contract value increase with projects",
                Objectives = "All projects implemented to generate revenue and add value to the customer, Learn project management skills, Follow RFS/RFQ process (commercial process), Grow the business",
                MeasurementSource = "Project tracking list and expansion of services",
                Weight = 10.00m,
                SortOrder = 3,
                Rating1Description = "Revenue decrease on service/contract and no additional project work",
                Rating2Description = "0% revenue increase on contract/services",
                Rating3Description = "10% of annual contract value increase with projects",
                Rating4Description = "15% of annual contract value increase with projects",
                Rating5Description = "20% of annual contract value increase with projects"
            });

            // Objective 3: Grow new customers (5%)
            var obj3 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Grow new customers through use of commercial models, marketing and promotional campaigns, CXC, etc",
                Weight = 5.00m,
                Description = "Expand customer base through marketing and commercial initiatives",
                SortOrder = 3
            };
            _context.OKRTemplateObjectives.Add(obj3);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj3.Id,
                Name = "CXC - Top 5 customers for CSI3",
                Target = "Resolve customer problems by understanding their pains and gains",
                Measure = "1 = No new product or service proposed\n2 = Product/Solution in testing phase\n3 = 1 New product/solution added to CSI3 CXC\n4 = 1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3\n5 = 2 or more implementations at customers for products/solutions presented by CSI3",
                Objectives = "Expansion of the MES services for existing and new customers, Participate in marketing and promotional campaigns",
                MeasurementSource = "Frequent engagement with stakeholders with next steps driven and resolve, Record of implementations, Record of customers in the CXC and/or other events",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No new product or service proposed",
                Rating2Description = "Product/Solution in testing phase",
                Rating3Description = "1 New product/solution added to CSI3 CXC",
                Rating4Description = "1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3",
                Rating5Description = "2 or more implementations at customers for products/solutions presented by CSI3"
            });

            // Objective 4: Internationalise (0%)
            var obj4 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Internationalise our business coverage in new markets outside of South Africa.",
                Weight = 0.00m,
                Description = "Expand business operations internationally",
                SortOrder = 4
            };
            _context.OKRTemplateObjectives.Add(obj4);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj4.Id,
                Name = "Internationalise our business",
                Target = "Increase footprint of support for CSI3 outside South Africa",
                Measure = "1 = No new business outside SA\n2 = Engagements took place for opportunities, but no additional business\n3 = Obtained an international contract for support/project of more than R 250 000\n4 = Obtained an international contract for support/project of more than R 750 000\n5 = Obtained an international contract for support/project of more than R 1 000 000",
                Objectives = "To create additional revenue stream, To create opportunities for existing team members that might want to immigrate/travel oversea",
                MeasurementSource = "Monthly BU Review",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No new business outside SA",
                Rating2Description = "Engagements took place for opportunities, but no additional business",
                Rating3Description = "Obtained an international contract for support/project of more than R 250 000",
                Rating4Description = "Obtained an international contract for support/project of more than R 750 000",
                Rating5Description = "Obtained an international contract for support/project of more than R 1 000 000"
            });

            // Objective 5: Target new customers (5%)
            var obj5 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Target specific new identified customers.",
                Weight = 5.00m,
                Description = "Onboard new customers to expand business",
                SortOrder = 5
            };
            _context.OKRTemplateObjectives.Add(obj5);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj5.Id,
                Name = "Onboard new customers",
                Target = "Expansion of CSI3 business via new customer base",
                Measure = "1 = No new opportunities from new customers\n2 = Engagement and new opportunities list, but no revenue\n3 = 1 New customer onboarded for a a project/service\n4 = More than 1 customer onboarded for new projects/services\n5 = New customers onboarded with revenue of more than R 500 000",
                Objectives = "To create additonal revenue stream, Expansion of the business, Growth mindset, Do market research",
                MeasurementSource = "Onboarding process of new clients",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No new opportunities from new customers",
                Rating2Description = "Engagement and new opportunities list, but no revenue",
                Rating3Description = "1 New customer onboarded for a a project/service",
                Rating4Description = "More than 1 customer onboarded for new projects/services",
                Rating5Description = "New customers onboarded with revenue of more than R 500 000"
            });

            // Objective 6: Accelerate growth (10%)
            var obj6 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Accelerate growth of our community through our partner management programme.",
                Weight = 10.00m,
                Description = "Develop team members through training and certification",
                SortOrder = 6
            };
            _context.OKRTemplateObjectives.Add(obj6);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj6.Id,
                Name = "Self Development and Growth",
                Target = "Courses and certification done to improve operational/technical/soft skills",
                Measure = "1 = No IDP in place\n2 = IDP in place but no courses or training completed\n3 = IDP in place and all plan courses completed\n4 = IDP in place and all plan courses completed, with additional training completed that was approved by line management\n5 = Degree/Diploma obtained and/or technical certification that generates additional revenue",
                Objectives = "Self development, Set the example for the team to grow, Course/Knowledge obtained to be applicable in the work place/career path",
                MeasurementSource = "CDF and employee document library + LinkedIn Learning, IDP + Skill Matrix",
                Weight = 10.00m,
                SortOrder = 1,
                Rating1Description = "No IDP in place",
                Rating2Description = "IDP in place but no courses or training completed",
                Rating3Description = "IDP in place and all plan courses completed",
                Rating4Description = "IDP in place and all plan courses completed, with additional training completed that was approved by line management",
                Rating5Description = "Degree/Diploma obtained and/or technical certification that generates additional revenue"
            });

            // Objective 7: Leverage CIE portfolio (5%)
            var obj7 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Leverage the full portfolio of CIE",
                Weight = 5.00m,
                Description = "Generate revenue from other CIE business units",
                SortOrder = 7
            };
            _context.OKRTemplateObjectives.Add(obj7);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj7.Id,
                Name = "CIE Portfolio revenue",
                Target = "Additional revenue generated from key CSI3 customers for other CIE BU's",
                Measure = "1 = No introduction to customers of other BU's\n2 = Introduction of other BU's done, but no additional revenue generated.\n3 = New contracts/services/projects for other BU's that generates revenue.\n4 = Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement\n5 = Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement",
                Objectives = "To understand service offering from other BU, To present CIE as a company; not having BU's work in silo's, Improve inter BU relationship, Work together as a team to improve revenue targets",
                MeasurementSource = "Financial Information, New contracts/projects in DI",
                Weight = 5.00m,
                SortOrder = 1,
                Rating1Description = "No introduction to customers of other BU's",
                Rating2Description = "Introduction of other BU's done, but no additional revenue generated.",
                Rating3Description = "New contracts/services/projects for other BU's that generates revenue.",
                Rating4Description = "Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement",
                Rating5Description = "Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement"
            });

            await _context.SaveChangesAsync();
        }

        // Helper methods to reduce code duplication
        private async Task AddDefendBaseKeyResultsAsync(int objectiveId, decimal behaviourWeight, decimal leadershipWeight, decimal taskWeight, decimal contractWeight, decimal docWeight, decimal sheWeight)
        {
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Behaviour - Team Orientated",
                Target = "Team Orientated",
                Measure = "1 = Not a team player, various HR related issues\n2 = Passive in the environment with minimal team involvement\n3 = Consistently collaborates and shares updates/knowledge while being approachable and open to input.\n4 = Demonstrate changes suggested and implemented that have created a positive impact on the whole team/environment\n5 = Multiple changes proposed and implemented, having a direct impact on team optimization and possible financial benefit",
                Objectives = "Proven track record of being a team-oriented professional, willing to work closely with others to achieve goals. Willingness, Language and office ethics, Shares knowledge",
                MeasurementSource = "Communication between team members where you have demonstrated the ability to transfer knowledge and assist when required, HR records, BoE - Body of evidence",
                Weight = behaviourWeight,
                SortOrder = 1,
                Rating1Description = "Not a team player, various HR related issues",
                Rating2Description = "Passive in the environment with minimal team involvement",
                Rating3Description = "Consistently collaborates and shares updates/knowledge while being approachable and open to input.",
                Rating4Description = "Demonstrate changes suggested and implemented that have created a positive impact on the whole team/environment",
                Rating5Description = "Multiple changes proposed and implemented, having a direct impact on team optimization and possible financial benefit"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Leadership/Ownership - Demonstrate leadership characteristics",
                Target = "Demonstrate leadership characteristics",
                Measure = "1 = Rarely takes initiative; occasionally identifies obvious issues but does not act on them\n2 = Occasionally takes responsibility but may not follow through to resolution.\n3 = Proactively seeks out and resolves potential problems with minimal guidance with positive client feedback.\n4 = Takes responsibility for complex issues, following up and ensuring thorough resolution that minimized business impact.\n5 = Showcase where you took ownership of issues across multiple technologies, which is over and above your current role requirements",
                Objectives = "Lead by example, Sacrifice own time to support business, Willingness, Honesty, Contribute to company values, Ownership",
                MeasurementSource = "Decision-Making, Accountability, Proactive Problem Solving, Team Influence, Delegation, Initiatives, RCA's, Award Nominations",
                Weight = leadershipWeight,
                SortOrder = 2,
                Rating1Description = "Rarely takes initiative; occasionally identifies obvious issues but does not act on them",
                Rating2Description = "Occasionally takes responsibility but may not follow through to resolution.",
                Rating3Description = "Proactively seeks out and resolves potential problems with minimal guidance with positive client feedback.",
                Rating4Description = "Takes responsibility for complex issues, following up and ensuring thorough resolution that minimized business impact.",
                Rating5Description = "Showcase where you took ownership of issues across multiple technologies, which is over and above your current role requirements"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Task Compliancy",
                Target = "Plan 30% of time and complete on time",
                Measure = "1 = Frequently misses deadlines; tasks are often incomplete or improperly done. Struggles with planning, leading to inefficient task execution.\n2 = Completes tasks on time but frequently needs support or corrections (Less than 60 % of work completed on time).\n3 = Consistently completes tasks efficiently and on time. Delivers projects on time. (90% of planned work executed on time)\n4 = Exhibits outstanding time management, often completing tasks ahead of schedule without compromising quality. Demonstrates high reliability and consistently delivers work on time with positive feedback and recognition\n5 = Exemplifies dependability, consistently exceeding expectations in timely and reliable task completion. Demonstrates strong strategic planning skills, setting and achieving goals that support team and organizational success.",
                Objectives = "Have an engaged and value added team, Increase productivity and efficiency in the team, Focus on proactive work and analytics + reporting, Learn to plan and execute according to schedule, Delivering Projects on time",
                MeasurementSource = "Planning and execution list, Project involvement",
                Weight = taskWeight,
                SortOrder = 3,
                Rating1Description = "Frequently misses deadlines; tasks are often incomplete or improperly done. Struggles with planning, leading to inefficient task execution.",
                Rating2Description = "Completes tasks on time but frequently needs support or corrections (Less than 60 % of work completed on time).",
                Rating3Description = "Consistently completes tasks efficiently and on time. Delivers projects on time. (90% of planned work executed on time)",
                Rating4Description = "Exhibits outstanding time management, often completing tasks ahead of schedule without compromising quality. Demonstrates high reliability and consistently delivers work on time with positive feedback and recognition",
                Rating5Description = "Exemplifies dependability, consistently exceeding expectations in timely and reliable task completion. Demonstrates strong strategic planning skills, setting and achieving goals that support team and organizational success."
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Contractual Compliance",
                Target = "Adhere to all client and ITIL processes to provide support to our end customers",
                Measure = "1 = Limited understanding of ITIL framework, requiring frequent guidance. Don't adhere to support requirements. Contract breach\n2 = Basic understanding of ITIL but inconsistently applies best practices resulting in support gap.\n3 = Consistently applies ITIL best practices effectively. Generally meets client expectations for compliance.\n4 = Demonstrate where a process/practice was changed to improve operations and service delivery with positive result\n5 = Due to your contract compliance and performance client gave recognition on multiple occasions.",
                Objectives = "Exceeding SLA compliancy, To be the differentiator at clients when choosing a preferred service provider, To set the bar for other service providers; they need to follow while we lead. Understand all the ITIL process and enforce compliancy",
                MeasurementSource = "SLA conformance and feedback; monthly reports, Client recognition, Not safety violations, Processes and procedures",
                Weight = contractWeight,
                SortOrder = 4,
                Rating1Description = "Limited understanding of ITIL framework, requiring frequent guidance. Don't adhere to support requirements. Contract breach",
                Rating2Description = "Basic understanding of ITIL but inconsistently applies best practices resulting in support gap.",
                Rating3Description = "Consistently applies ITIL best practices effectively. Generally meets client expectations for compliance.",
                Rating4Description = "Demonstrate where a process/practice was changed to improve operations and service delivery with positive result",
                Rating5Description = "Due to your contract compliance and performance client gave recognition on multiple occasions."
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Documentation/Reporting and quality",
                Target = "Create quality processes/procedures and documentation",
                Measure = "1 = Frequently fails to create or update documentation as required, leading to knowledge gaps. Inaccurate reporting.\n2 = Occasionally reviews documentation but may miss occasional deadlines. Have to review documentation multiple times for the same quality issues.\n3 = Consistently reviews and updates documentation proactively, providing clear, professional, and well-structured writing/reporting.\n4 = Consistently provides accurate and detailed documentation, applies feedback and makes improvements.\n5 = Exemplary timeliness, with early or on-time completion of all documentation or monthly reports, actively supporting timely information flow without any updates or quality issues.",
                Objectives = "Percentage of documents rejected due to quality / compliance issues, Percentage of documents rejected by clients (for document control related reasons), Percentage of late documents, Average review time, Percentage of documents in the various statuses, Monthly Reporting/Health Review / Presentation as accurate and presentable",
                MeasurementSource = "Health Review Reporting, Technical Recovery Document, Works Instructions, Procedures, Presentations",
                Weight = docWeight,
                SortOrder = 5,
                Rating1Description = "Frequently fails to create or update documentation as required, leading to knowledge gaps. Inaccurate reporting.",
                Rating2Description = "Occasionally reviews documentation but may miss occasional deadlines. Have to review documentation multiple times for the same quality issues.",
                Rating3Description = "Consistently reviews and updates documentation proactively, providing clear, professional, and well-structured writing/reporting.",
                Rating4Description = "Consistently provides accurate and detailed documentation, applies feedback and makes improvements.",
                Rating5Description = "Exemplary timeliness, with early or on-time completion of all documentation or monthly reports, actively supporting timely information flow without any updates or quality issues."
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "SHE Compliancy",
                Target = "Have zero deviations/violations with regards to safety.",
                Measure = "1 = Had a SHE violation\n2 = Missed acceptance deadline of SHE policies and procedures.\n3 = 100% SHE Compliant and involved in monthly safety talks and presentations.\n4 = You are appointed in a SHE function within the BU and actively participating\n5 = You have identified and implemented an improvement related to SHE",
                Objectives = "Improve safety education, Ensure compliance with OHS guidelines, Foster proactive safety awareness, Safety Moment per quarter, Presented or compiled Safety presentation as part of monthly OHS presentations. 100% acknowledgment of OHS communications",
                MeasurementSource = "Incident reporting and tracking register, Safety Presentation, OHS Communication Acceptance, Safety Moments, Safety Role - Contribution",
                Weight = sheWeight,
                SortOrder = 6,
                Rating1Description = "Had a SHE violation",
                Rating2Description = "Missed acceptance deadline of SHE policies and procedures.",
                Rating3Description = "100% SHE Compliant and involved in monthly safety talks and presentations.",
                Rating4Description = "You are appointed in a SHE function within the BU and actively participating",
                Rating5Description = "You have identified and implemented an improvement related to SHE"
            });
        }

        private async Task AddShareOfWalletKeyResultsAsync(int objectiveId, decimal csiWeight, decimal improvementsWeight, decimal revenueWeight)
        {
            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Continuous Service Improvements (not a request from the client)",
                Target = "CSI's that are recommend are implemented and add value to the customer/client with revenue generated",
                Measure = "1 = No CSI documented or implemented\n2 = CSI was documented but not approved for implementation\n3 = A CSI was documented and implemented\n4 = CSI's approved and implemented adding revenue over R 250 000\n5 = CSI's approved and implemented adding revenue over R 450 000",
                Objectives = "Create a culture where all team members are actively participating in CSI's/Innovation, Develop analytical skills, Develop presentation skills, Understanding cost benefit analysis",
                MeasurementSource = "CSI Register, Customer success stories",
                Weight = csiWeight,
                SortOrder = 1,
                Rating1Description = "No CSI documented or implemented",
                Rating2Description = "CSI was documented but not approved for implementation",
                Rating3Description = "A CSI was documented and implemented",
                Rating4Description = "CSI's approved and implemented adding revenue over R 250 000",
                Rating5Description = "CSI's approved and implemented adding revenue over R 450 000"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Improvements + Opportunities (Scope Increase)",
                Target = "The implementation of new opportunities and improvements",
                Measure = "1 = No new improvements or opportunities identified\n2 = Improvements or opportunities identified but not implemented\n3 = Multiple improvements identified and implemented or opportunities that have become projects\n4 = Improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000\n5 = More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 450 000",
                Objectives = "Identify opportunities that become projects (revenue generating), Identify opportunities that add value to business (\"mini CSI\")",
                MeasurementSource = "Improvements and opportunities tracking register",
                Weight = improvementsWeight,
                SortOrder = 2,
                Rating1Description = "No new improvements or opportunities identified",
                Rating2Description = "Improvements or opportunities identified but not implemented",
                Rating3Description = "Multiple improvements identified and implemented or opportunities that have become projects",
                Rating4Description = "Improvement implemented with customer recognition, or projects/services that result in revenue of over R 250 000",
                Rating5Description = "More than 1 improvement implemented with customer recognition, or projects/services that result in revenue of over R 450 000"
            });

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = objectiveId,
                Name = "Revenue Increase",
                Target = "Additional revenue generated via project implementation and monthly service growth",
                Measure = "1 = Revenue decrease on service/contract and no additional project work\n2 = 0% revenue increase on contract/services\n3 = 10% of annual contract value increase with projects\n4 = 15% of annual contract value increase with projects\n5 = 20% of annual contract value increase with projects",
                Objectives = "All projects implemented to generate revenue and add value to the customer, Learn project management skills, Follow RFS/RFQ process (commercial process), Grow the business",
                MeasurementSource = "Project tracking list and expansion of services",
                Weight = revenueWeight,
                SortOrder = 3,
                Rating1Description = "Revenue decrease on service/contract and no additional project work",
                Rating2Description = "0% revenue increase on contract/services",
                Rating3Description = "10% of annual contract value increase with projects",
                Rating4Description = "15% of annual contract value increase with projects",
                Rating5Description = "20% of annual contract value increase with projects"
            });
        }

        private async Task AddCommonObjectivesAsync(OKRTemplate template, decimal growCustomersWeight, decimal selfDevWeight, int startSortOrder)
        {
            // Objective: Grow new customers
            var obj3 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Grow new customers through use of commercial models, marketing and promotional campaigns, CXC, etc",
                Weight = growCustomersWeight,
                Description = "Expand customer base through marketing and commercial initiatives",
                SortOrder = startSortOrder
            };
            _context.OKRTemplateObjectives.Add(obj3);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj3.Id,
                Name = "CXC - Top 5 customers for CSI3",
                Target = "Resolve customer problems by understanding their pains and gains",
                Measure = "1 = No new product or service proposed\n2 = Product/Solution in testing phase\n3 = 1 New product/solution added to CSI3 CXC\n4 = 1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3\n5 = 2 or more implementations at customers for products/solutions presented by CSI3",
                Objectives = "Expansion of the MES services for existing and new customers, Participate in marketing and promotional campaigns",
                MeasurementSource = "Frequent engagement with stakeholders with next steps driven and resolve, Record of implementations, Record of customers in the CXC and/or other events",
                Weight = growCustomersWeight,
                SortOrder = 1,
                Rating1Description = "No new product or service proposed",
                Rating2Description = "Product/Solution in testing phase",
                Rating3Description = "1 New product/solution added to CSI3 CXC",
                Rating4Description = "1 New products/solutions added to CSI3 CXC and 1 implementation at customers for products/solutions presented by CSI3",
                Rating5Description = "2 or more implementations at customers for products/solutions presented by CSI3"
            });

            // Objective: Internationalise
            var obj4 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Internationalise our business coverage in new markets outside of South Africa.",
                Weight = 0.00m,
                Description = "Expand business operations internationally",
                SortOrder = startSortOrder + 1
            };
            _context.OKRTemplateObjectives.Add(obj4);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj4.Id,
                Name = "Internationalise our business",
                Target = "Increase footprint of support for CSI3 outside South Africa",
                Measure = "1 = No new business outside SA\n2 = Engagements took place for opportunities, but no additional business\n3 = Obtained an international contract for support/project of more than R 250 000\n4 = Obtained an international contract for support/project of more than R 750 000\n5 = Obtained an international contract for support/project of more than R 1 000 000",
                Objectives = "To create additional revenue stream, To create opportunities for existing team members that might want to immigrate/travel oversea",
                MeasurementSource = "Monthly BU Review",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No new business outside SA",
                Rating2Description = "Engagements took place for opportunities, but no additional business",
                Rating3Description = "Obtained an international contract for support/project of more than R 250 000",
                Rating4Description = "Obtained an international contract for support/project of more than R 750 000",
                Rating5Description = "Obtained an international contract for support/project of more than R 1 000 000"
            });

            // Objective: Target new customers
            var obj5 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Target specific new identified customers.",
                Weight = 0.00m,
                Description = "Onboard new customers to expand business",
                SortOrder = startSortOrder + 2
            };
            _context.OKRTemplateObjectives.Add(obj5);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj5.Id,
                Name = "Onboard new customers",
                Target = "Expansion of CSI3 business via new customer base",
                Measure = "",
                Objectives = "",
                MeasurementSource = "",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No new opportunities from new customers",
                Rating2Description = "Engagement and new opportunities list, but no revenue",
                Rating3Description = "1 New customer onboarded for a project/service",
                Rating4Description = "More than 1 customer onboarded for new projects/services",
                Rating5Description = "New customers onboarded with revenue of more than R 500 000"
            });

            // Objective: Accelerate growth
            var obj6 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Accelerate growth of our community through our partner management programme.",
                Weight = selfDevWeight,
                Description = "Develop team members through training and certification",
                SortOrder = startSortOrder + 3
            };
            _context.OKRTemplateObjectives.Add(obj6);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj6.Id,
                Name = "Self Development and Growth",
                Target = "Courses and certification done to improve operational/technical/soft skills",
                Measure = "1 = No IDP in place\n2 = IDP in place but no courses or training completed\n3 = IDP in place and all plan courses completed\n4 = IDP in place and all plan courses completed, with additional training completed that was approved by line management\n5 = Degree/Diploma obtained and/or technical certification that generates additional revenue",
                Objectives = "Self development, Set the example for the team to grow, Course/Knowledge obtained to be applicable in the work place/career path",
                MeasurementSource = "CDF and employee document library + LinkedIn Learning, IDP + Skill Matrix",
                Weight = selfDevWeight,
                SortOrder = 1,
                Rating1Description = "No IDP in place",
                Rating2Description = "IDP in place but no courses or training completed",
                Rating3Description = "IDP in place and all plan courses completed",
                Rating4Description = "IDP in place and all plan courses completed, with additional training completed that was approved by line management",
                Rating5Description = "Degree/Diploma obtained and/or technical certification that generates additional revenue"
            });

            // Objective: Leverage CIE portfolio
            var obj7 = new OKRTemplateObjective
            {
                OKRTemplateId = template.Id,
                Name = "Leverage the full portfolio of CIE",
                Weight = 0.00m,
                Description = "Generate revenue from other CIE business units",
                SortOrder = startSortOrder + 4
            };
            _context.OKRTemplateObjectives.Add(obj7);
            await _context.SaveChangesAsync();

            _context.OKRTemplateKeyResults.Add(new OKRTemplateKeyResult
            {
                OKRTemplateObjectiveId = obj7.Id,
                Name = "CIE Portfolio revenue",
                Target = "Additional revenue generated from key CSI3 customers for other CIE BU's",
                Measure = "1 = No introduction to customers of other BU's\n2 = Introduction of other BU's done, but no additional revenue generated.\n3 = New contracts/services/projects for other BU's that generates revenue.\n4 = Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement\n5 = Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement",
                Objectives = "To understand service offering from other BU, To present CIE as a company; not having BU's work in silo's, Improve inter BU relationship, Work together as a team to improve revenue targets",
                MeasurementSource = "Financial Information, New contracts/projects in DI",
                Weight = 0.00m,
                SortOrder = 1,
                Rating1Description = "No introduction to customers of other BU's",
                Rating2Description = "Introduction of other BU's done, but no additional revenue generated.",
                Rating3Description = "New contracts/services/projects for other BU's that generates revenue.",
                Rating4Description = "Additional revenue generated by other BU's to the value of R500 000 due to an CSI3 engagement",
                Rating5Description = "Additional revenue generated exceeding R 1 million by other BU's due to an CSI3 engagement"
            });
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

            // Create default HR user if it doesn't exist
            var hrUser = await _userManager.FindByEmailAsync("hr@okr.com");
            if (hrUser == null)
            {
                hrUser = new ApplicationUser
                {
                    UserName = "hr@okr.com",
                    Email = "hr@okr.com",
                    EmailConfirmed = true,
                    FirstName = "HR",
                    LastName = "Super User"
                };

                var result = await _userManager.CreateAsync(hrUser, "HR123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(hrUser, "HR");
                    
                    // Create employee record
                    var employee = new Employee
                    {
                        UserId = hrUser.Id,
                        FirstName = "HR",
                        LastName = "Super User",
                        Email = "hr@okr.com",
                        Role = "HR",
                        Position = "HR Manager",
                        LineOfBusiness = "Digital Industries - CSI3",
                        FinancialYear = "FY 2025",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };
                    
                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();
                }
            }
        }
    }
}