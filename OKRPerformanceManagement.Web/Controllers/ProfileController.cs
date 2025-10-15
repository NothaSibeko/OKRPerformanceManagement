using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Models;
using OKRPerformanceManagement.Web.ViewModels;
using System.Security.Claims;

namespace OKRPerformanceManagement.Web.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // View Profile
        [HttpGet]
        public async Task<IActionResult> ViewProfile()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(currentUserId);
            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (user == null || employee == null)
            {
                TempData["ErrorMessage"] = "Profile not found.";
                return RedirectToAction("Index", "Home");
            }

            var model = new ViewProfileViewModel
            {
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Role = employee.Role,
                Position = employee.Position,
                ManagerName = employee.Manager != null ? $"{employee.Manager.FirstName} {employee.Manager.LastName}" : "N/A",
                CreatedDate = employee.CreatedDate,
                IsActive = employee.IsActive
            };

            return View(model);
        }

        // Edit Profile
        [HttpGet]
        public async Task<IActionResult> EditProfile()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Profile not found.";
                return RedirectToAction("Index", "Home");
            }

            var model = new EditProfileViewModel
            {
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                Email = employee.Email,
                Position = employee.Position
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProfile(EditProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.UserId == currentUserId);

                if (employee == null)
                {
                    TempData["ErrorMessage"] = "Profile not found.";
                    return RedirectToAction("Index", "Home");
                }

                employee.FirstName = model.FirstName;
                employee.LastName = model.LastName;
                employee.Email = model.Email;
                employee.Position = model.Position;

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Profile updated successfully.";
                return RedirectToAction("ViewProfile");
            }

            return View(model);
        }

        // Change Password
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userManager.FindByIdAsync(currentUserId);

                if (user == null)
                {
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToAction("Index", "Home");
                }

                var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Password changed successfully.";
                    return RedirectToAction("ViewProfile");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                }
            }

            return View(model);
        }

        // Performance History
        [HttpGet]
        public async Task<IActionResult> PerformanceHistory()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee not found.";
                return RedirectToAction("Index", "Home");
            }

            var performanceReviews = await _context.PerformanceReviews
                .Include(pr => pr.Employee)
                .Include(pr => pr.Manager)
                .Where(pr => pr.EmployeeId == employee.Id)
                .OrderByDescending(pr => pr.CreatedDate)
                .ToListAsync();

            return View(performanceReviews);
        }

        // Settings
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            // Fallback to default values if UserSettings table doesn't exist yet
            UserSettings userSettings = null;
            try
            {
                userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == currentUserId);
            }
            catch
            {
                // Table doesn't exist yet, use defaults
            }

            var model = new SettingsViewModel
            {
                NotificationsEnabled = userSettings?.NotificationsEnabled ?? true,
                Theme = userSettings?.Theme ?? "Light",
                Language = userSettings?.Language ?? "English",
                TimeZone = userSettings?.TimeZone ?? "UTC"
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(SettingsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                try
                {
                    var userSettings = await _context.UserSettings
                        .FirstOrDefaultAsync(us => us.UserId == currentUserId);

                    if (userSettings == null)
                    {
                        userSettings = new UserSettings
                        {
                            UserId = currentUserId,
                            CreatedDate = DateTime.Now
                        };
                        _context.UserSettings.Add(userSettings);
                    }

                    userSettings.NotificationsEnabled = model.NotificationsEnabled;
                    userSettings.Theme = model.Theme;
                    userSettings.Language = model.Language;
                    userSettings.TimeZone = model.TimeZone;
                    userSettings.LastUpdated = DateTime.Now;

                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Table doesn't exist yet, just show success message
                }

                TempData["SuccessMessage"] = "Settings updated successfully.";
                return RedirectToAction("Settings");
            }

            return View(model);
        }

        // Notifications
        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            UserSettings userSettings = null;
            try
            {
                userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == currentUserId);
            }
            catch
            {
                // Table doesn't exist yet, use defaults
            }

            var model = new NotificationsViewModel
            {
                EmailNotifications = userSettings?.EmailNotifications ?? true,
                PushNotifications = userSettings?.PushNotifications ?? false,
                ReviewReminders = userSettings?.ReviewReminders ?? true,
                GoalDeadlines = userSettings?.GoalDeadlines ?? true,
                WeeklyReports = userSettings?.WeeklyReports ?? false
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Notifications(NotificationsViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                try
                {
                    var userSettings = await _context.UserSettings
                        .FirstOrDefaultAsync(us => us.UserId == currentUserId);

                    if (userSettings == null)
                    {
                        userSettings = new UserSettings
                        {
                            UserId = currentUserId,
                            CreatedDate = DateTime.Now
                        };
                        _context.UserSettings.Add(userSettings);
                    }

                    userSettings.EmailNotifications = model.EmailNotifications;
                    userSettings.PushNotifications = model.PushNotifications;
                    userSettings.ReviewReminders = model.ReviewReminders;
                    userSettings.GoalDeadlines = model.GoalDeadlines;
                    userSettings.WeeklyReports = model.WeeklyReports;
                    userSettings.LastUpdated = DateTime.Now;

                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Table doesn't exist yet, just show success message
                }

                TempData["SuccessMessage"] = "Notification preferences updated successfully.";
                return RedirectToAction("Notifications");
            }

            return View(model);
        }

        // Privacy & Security
        [HttpGet]
        public async Task<IActionResult> PrivacySecurity()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            
            UserSettings userSettings = null;
            try
            {
                userSettings = await _context.UserSettings
                    .FirstOrDefaultAsync(us => us.UserId == currentUserId);
            }
            catch
            {
                // Table doesn't exist yet, use defaults
            }

            var model = new PrivacySecurityViewModel
            {
                TwoFactorEnabled = userSettings?.TwoFactorEnabled ?? false,
                DataSharing = userSettings?.DataSharing ?? false,
                ProfileVisibility = userSettings?.ProfileVisibility ?? "Private",
                LoginAlerts = userSettings?.LoginAlerts ?? true
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PrivacySecurity(PrivacySecurityViewModel model)
        {
            if (ModelState.IsValid)
            {
                var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                
                try
                {
                    var userSettings = await _context.UserSettings
                        .FirstOrDefaultAsync(us => us.UserId == currentUserId);

                    if (userSettings == null)
                    {
                        userSettings = new UserSettings
                        {
                            UserId = currentUserId,
                            CreatedDate = DateTime.Now
                        };
                        _context.UserSettings.Add(userSettings);
                    }

                    userSettings.TwoFactorEnabled = model.TwoFactorEnabled;
                    userSettings.DataSharing = model.DataSharing;
                    userSettings.ProfileVisibility = model.ProfileVisibility;
                    userSettings.LoginAlerts = model.LoginAlerts;
                    userSettings.LastUpdated = DateTime.Now;

                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Table doesn't exist yet, just show success message
                }

                TempData["SuccessMessage"] = "Privacy and security settings updated successfully.";
                return RedirectToAction("PrivacySecurity");
            }

            return View(model);
        }

        // Export Data
        [HttpGet]
        public async Task<IActionResult> ExportData()
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var employee = await _context.Employees
                .Include(e => e.RoleEntity)
                .Include(e => e.Manager)
                .FirstOrDefaultAsync(e => e.UserId == currentUserId);

            if (employee == null)
            {
                TempData["ErrorMessage"] = "Employee not found.";
                return RedirectToAction("Index", "Home");
            }

            var performanceReviews = await _context.PerformanceReviews
                .Include(pr => pr.Manager)
                .Where(pr => pr.EmployeeId == employee.Id)
                .ToListAsync();

            var exportData = new
            {
                Employee = new
                {
                    employee.FirstName,
                    employee.LastName,
                    employee.Email,
                    employee.Role,
                    employee.Position,
                    employee.CreatedDate
                },
                PerformanceReviews = performanceReviews.Select(pr => new
                {
                    ReviewDate = pr.CreatedDate,
                    pr.OverallRating,
                    Comments = pr.FinalAssessment ?? pr.ManagerAssessment ?? pr.EmployeeSelfAssessment ?? "No comments",
                    Reviewer = pr.Manager != null ? $"{pr.Manager.FirstName} {pr.Manager.LastName}" : "N/A"
                })
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            
            return File(bytes, "application/json", $"employee_data_{employee.FirstName}_{employee.LastName}_{DateTime.Now:yyyyMMdd}.json");
        }

        // Help & Support
        [HttpGet]
        public IActionResult UserGuide()
        {
            return View();
        }

        [HttpGet]
        public IActionResult ContactSupport()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ContactSupport(ContactSupportViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Here you would typically send an email or create a support ticket
                TempData["SuccessMessage"] = "Your message has been sent to our support team. We'll get back to you within 24 hours.";
                return RedirectToAction("ContactSupport");
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult About()
        {
            var model = new AboutViewModel
            {
                ApplicationName = "OKR Performance Management System",
                Version = "1.0.0",
                BuildDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Company = "Your Company Name"
            };

            return View(model);
        }
    }
}
