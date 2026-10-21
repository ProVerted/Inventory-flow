using Microsoft.AspNetCore.Mvc;
using InventorySystem.Data;
using InventorySystem.DTOs;
using InventorySystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace InventorySystem.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;

        public AuthController(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _passwordHasher = passwordHasher;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var normalizedUsername = request.Username.Trim();

            var exists = await _context.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername.ToLower());
            if (exists)
            {
                return Conflict(new { message = "Username already exists." });
            }

            // If no users exist in the system yet, promote the first user to Admin
            var isFirstUser = !await _context.Users.AnyAsync();
            var role = isFirstUser ? "Admin" : (!string.IsNullOrWhiteSpace(request.Role) ? request.Role.Trim() : "Staff");

            var user = new User
            {
                Username = normalizedUsername,
                Role = role
            };

            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return StatusCode(StatusCodes.Status201Created, new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var normalizedUsername = request.Username.Trim();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == normalizedUsername.ToLower());

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

            // Backward compatibility fallback in case an existing user had a plaintext password
            if (verifyResult == PasswordVerificationResult.Failed && user.PasswordHash == request.Password)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
                await _context.SaveChangesAsync();
                verifyResult = PasswordVerificationResult.SuccessRehashNeeded;
            }
            else if (verifyResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new { message = "Invalid credentials." });
            }

            if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
                await _context.SaveChangesAsync();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                }
            );

            return Ok(new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            });
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Ok(new { message = "Successfully logged out." });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var username = User.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                return Unauthorized();
            }

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            return Ok(new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role
            });
        }
    }
}