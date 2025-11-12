using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using OKRPerformanceManagement.Models;
using OKRPerformanceManagement.Data;
using OKRPerformanceManagement.Web.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace OKRPerformanceManagement.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
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
            ViewData["ReturnUrl"] = returnUrl;
            model.ReturnUrl = returnUrl ?? "";
            
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "Please correct the errors and try again.");
                return View(model);
            }

            // Check if user exists first
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, $"User with email '{model.Email}' does not exist.");
                return View(model);
            }

            // Check if user is locked out
            if (await _userManager.IsLockedOutAsync(user))
            {
                ModelState.AddModelError(string.Empty, "User account is locked out.");
                return View(model);
            }

            // Try to sign in
            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: false);
            
            if (result.Succeeded)
            {
                return RedirectToLocal(returnUrl);
            }
            else if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "User account is locked out.");
                return View(model);
            }
            else if (result.RequiresTwoFactor)
            {
                ModelState.AddModelError(string.Empty, "Two-factor authentication is required.");
                return View(model);
            }
            else if (result.IsNotAllowed)
            {
                ModelState.AddModelError(string.Empty, "Your account is not allowed to sign in. Please contact an administrator.");
                return View(model);
            }
            else
            {
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
                        ManagerId = model.ManagerId == 0 ? null : model.ManagerId,
                        LineOfBusiness = "Digital Industries - CSI3",
                        FinancialYear = "FY 2025",
                        IsActive = true,
                        CreatedDate = DateTime.Now
                    };

                    _context.Employees.Add(employee);
                    await _context.SaveChangesAsync();

                    // Assign Identity role to user BEFORE signing in
                    if (!string.IsNullOrEmpty(model.Role))
                    {
                        // Ensure the role exists
                        if (!await _roleManager.RoleExistsAsync(model.Role))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(model.Role));
                        }
                        
                        // Assign the role to the user
                        var roleResult = await _userManager.AddToRoleAsync(user, model.Role);
                        if (!roleResult.Succeeded)
                        {
                            // Log errors but don't fail registration
                            foreach (var error in roleResult.Errors)
                            {
                                // Role assignment failed, but user is created
                                // They can still log in and an admin can assign the role later
                            }
                        }
                    }

                    // Sign in the user first
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    
                    // If role was assigned, refresh the sign-in to ensure role claims are included in the cookie
                    if (!string.IsNullOrEmpty(model.Role))
                    {
                        // Reload user to get fresh role data
                        user = await _userManager.FindByIdAsync(user.Id);
                        // Refresh sign-in to update the authentication cookie with role claims
                        await _signInManager.RefreshSignInAsync(user);
                    }
                    
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
            
            ViewBag.Users = users;
            ViewBag.Employees = employees;
            ViewBag.UserCount = users.Count;
            ViewBag.EmployeeCount = employees.Count;
            
            return View();
        }

        [HttpGet]
        public IActionResult AccessDenied(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> CheckMyRoles()
        {
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Json(new { error = "User not found" });
            }

            var user = await _userManager.FindByIdAsync(currentUserId);
            if (user == null)
            {
                return Json(new { error = "User not found in database" });
            }

            var identityRoles = await _userManager.GetRolesAsync(user);
            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.UserId == currentUserId);
            
            var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
            var roleClaims = User.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).Select(c => c.Value).ToList();

            return Json(new
            {
                userId = currentUserId,
                email = user.Email,
                identityRoles = identityRoles,
                employeeRole = employee?.Role,
                roleClaims = roleClaims,
                isInRoleManager = User.IsInRole("Manager"),
                isInRoleAdmin = User.IsInRole("Admin"),
                allClaims = claims
            });
        }

    }
}
