using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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

    }
}
