using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TodoApp.Configuration;
using TodoApp.Models.DTO.Request;
using TodoApp.Models.DTO.Response;

namespace TodoApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthManagerController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JWTConfig _jwtConfig;

        public AuthManagerController(UserManager<IdentityUser> userManager, IOptionsMonitor<JWTConfig> optionsMonitor)
        {
            _userManager = userManager;
            _jwtConfig = optionsMonitor.CurrentValue;
        }

        [HttpPost]
        [Route("Register")]
        public async Task<ActionResult> Register([FromBody] UserRegistrationDTO user)
        {
            if(ModelState.IsValid)
            {
                var existingUser = await _userManager.FindByEmailAsync(user.Email);
                if (existingUser != null)
                {
                    return BadRequest(new RegistrationResponseDTO()
                    {
                        Error = new List<string>() { "Email already exists." },
                        Success = false
                    });
                }

                var newUser = new IdentityUser() { Email = user.Email, UserName = user.UserName };
                var isCreated = await _userManager.CreateAsync(newUser, user.Password);

                if (isCreated.Succeeded)
                {
                    var jwtToken = GenerateJWTToken(newUser);

                    return Ok(new RegistrationResponseDTO()
                    {
                        Success = true,
                        Token = jwtToken
                    });
                }
                else
                {
                    return BadRequest(new RegistrationResponseDTO()
                    {
                        Error = isCreated.Errors.Select(x=> x.Description).ToList(),
                        Success = false
                    });
                }

            }

            return BadRequest(new RegistrationResponseDTO()
            {
                Error = new List<string>() { "Invalid Payload"},
                Success = false
            });
        }

        [HttpPost]
        [Route("Login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequestDto user)
        {
            if(ModelState.IsValid) 
            {
                var userExists = await _userManager.FindByEmailAsync(user.Email);

                if(userExists == null)
                {
                    return BadRequest(new RegistrationResponseDTO()
                    {
                        Error = new List<string>() { "No Such user exist" },
                        Success = false
                    });
                }

                var isCorrect = await _userManager.CheckPasswordAsync(userExists, user.Password);

                if (!isCorrect) {
                    return BadRequest(new RegistrationResponseDTO()
                    {
                        Error = new List<string>() { "Invalid Password" },
                        Success = false
                    });
                }

                var jwtToken = GenerateJWTToken(userExists);

                return Ok(new RegistrationResponseDTO()
                {
                    Success = true,
                    Token = jwtToken
                });
            }

            return BadRequest(new RegistrationResponseDTO()
            {
                Error = new List<string>() { "Invalid Payload" },
                Success = false
            });
        }

        private string GenerateJWTToken(IdentityUser user)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();

            var key = Encoding.ASCII.GetBytes(_jwtConfig.Secret);

            var tokenDescriptor = new SecurityTokenDescriptor { 
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("Id", user.Id),
                    new Claim(JwtRegisteredClaimNames.Email , user.Email),
                    new Claim(JwtRegisteredClaimNames.Sub, user.Email),
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
                }),
                Expires= DateTime.UtcNow.AddHours(6),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var JwtToken = jwtTokenHandler.WriteToken(token);

            return JwtToken;
        }

    }
}
