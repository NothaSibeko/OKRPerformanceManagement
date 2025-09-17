using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OKRPerformanceManagement.Models;
using OKRPerformanceManagement.Data;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace OKRPerformanceManagement.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            var model = new LoginViewModel { ReturnUrl = returnUrl ?? "" };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            Console.WriteLine("=== LOGIN POST ACTION CALLED ===");
            ViewData["ReturnUrl"] = returnUrl;
            model.ReturnUrl = returnUrl ?? "";
            
            // Add debug logging
            Console.WriteLine($"Login attempt for email: {model.Email}");
            
            if (!ModelState.IsValid)
            {
                Console.WriteLine("ModelState is not valid");
                ModelState.AddModelError(string.Empty, "Please correct the errors and try again.");
                return View(model);
            }

            // Check if user exists first
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                Console.WriteLine($"User with email '{model.Email}' not found");
                ModelState.AddModelError(string.Empty, $"User with email '{model.Email}' does not exist.");
                return View(model);
            }

            Console.WriteLine($"User found: {user.Email}, ID: {user.Id}");

            // Check if user is locked out
            if (await _userManager.IsLockedOutAsync(user))
            {
                Console.WriteLine("User is locked out");
                ModelState.AddModelError(string.Empty, "User account is locked out.");
                return View(model);
            }

            // Try to sign in
            Console.WriteLine("Attempting password sign in...");
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
            
            Console.WriteLine($"Sign in result: Succeeded={result.Succeeded}, IsLockedOut={result.IsLockedOut}, RequiresTwoFactor={result.RequiresTwoFactor}, IsNotAllowed={result.IsNotAllowed}");
            
            if (result.Succeeded)
            {
                Console.WriteLine("Login successful, redirecting...");
                // Redirect to Home for all users
                return RedirectToLocal(returnUrl);
            }
            else if (result.IsLockedOut)
            {
                Console.WriteLine("User account is locked out");
                ModelState.AddModelError(string.Empty, "User account is locked out.");
                return View(model);
            }
            else if (result.RequiresTwoFactor)
            {
                Console.WriteLine("Two-factor authentication required");
                ModelState.AddModelError(string.Empty, "Two-factor authentication is required.");
                return View(model);
            }
            else if (result.IsNotAllowed)
            {
                Console.WriteLine("User is not allowed to sign in");
                ModelState.AddModelError(string.Empty, "Your account is not allowed to sign in. Please contact an administrator.");
                return View(model);
            }
            else
            {
                Console.WriteLine("Login failed - invalid password");
                ModelState.AddModelError(string.Empty, "Invalid password. Please check your password and try again.");
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            // Get managers for the dropdown
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager")
                .Select(e => new { e.Id, Name = $"{e.FirstName} {e.LastName}" })
                .ToListAsync();
            
            ViewBag.Managers = managers;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    // Create employee record
                    var employee = new Employee
                    {
                        FirstName = model.FirstName,
                        LastName = model.LastName,
                        Email = model.Email,
                        Role = model.Role,
                        Position = model.Position,
                        UserId = user.Id,
                        ManagerId = model.ManagerId == 0 ? null : model.ManagerId
                    };

                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    
                    // Redirect to Home for all users
                    return RedirectToAction("Index", "Home");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            
            // If we get here, there was an error - repopulate managers dropdown
            var managers = await _context.Employees
                .Where(e => e.Role == "Manager")
                .Select(e => new { e.Id, Name = $"{e.FirstName} {e.LastName}" })
                .ToListAsync();
            
            ViewBag.Managers = managers;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        private string GetControllerForRole(string role)
        {
            return role switch
            {
                "Manager" => "Manager",
                "Admin" => "Admin",
                _ => "Employee"
            };
        }

        [HttpGet]
        public async Task<IActionResult> DebugUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var employees = await _context.Employees.ToListAsync();
            
            ViewBag.Users = users;
            ViewBag.Employees = employees;
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CheckUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var employees = await _context.Employees.ToListAsync();
            
            // Debug: Print user details to console
            Console.WriteLine($"Found {users.Count} users in database:");
            foreach (var user in users)
            {
                Console.WriteLine($"- Email: {user.Email}, UserName: {user.UserName}, FirstName: {user.FirstName}, LastName: {user.LastName}");
                Console.WriteLine($"  EmailConfirmed: {user.EmailConfirmed}, LockoutEnabled: {user.LockoutEnabled}");
            }
            
            ViewBag.Users = users;
            ViewBag.Employees = employees;
            ViewBag.UserCount = users.Count;
            ViewBag.EmployeeCount = employees.Count;
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> CreateTestUser()
        {
            // Create a test manager user
            var testUser = new ApplicationUser
            {
                UserName = "manager@test.com",
                Email = "manager@test.com",
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "Manager"
            };

            var result = await _userManager.CreateAsync(testUser, "Test123!");
            
            if (result.Succeeded)
            {
                // Add to Manager role
                await _userManager.AddToRoleAsync(testUser, "Manager");
                
                // Create employee record
                var employee = new Employee
                {
                    UserId = testUser.Id,
                    FirstName = "Test",
                    LastName = "Manager",
                    Email = "manager@test.com",
                    Role = "Manager",
                    Position = "Test Manager",
                    CreatedDate = DateTime.Now
                };
                
                _context.Employees.Add(employee);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Test user created: manager@test.com / Test123!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to create test user: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }
            
            return RedirectToAction("Login");
        }
    }

    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class RegisterViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        [Required]
        [Display(Name = "Role")]
        public string Role { get; set; }

        [Required]
        [Display(Name = "Position")]
        public string Position { get; set; }

        [Display(Name = "Manager")]
        public int? ManagerId { get; set; }
    }
}
