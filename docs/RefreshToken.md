### Refresh JWT with Refresh token
1. Update `Program.cs` file move **TokenValidationParameters** to use it as a singleton
```csharp
var key = Encoding.ASCII.GetBytes(builder.Configuration["JWTConfig:Secret"]);
var jwtTokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuerSigningKey = true,
    IssuerSigningKey = new SymmetricSecurityKey(key),
    ValidateIssuer = false,
    ValidateAudience = false,
    ValidateLifetime = true,
    RequireExpirationTime = false,

};

builder.Services.AddSingleton(jwtTokenValidationParameters);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(jwt =>
{
    jwt.SaveToken = true;
    jwt.TokenValidationParameters = jwtTokenValidationParameters;
});

```
2. Update `AuthResult.cs` file to include RefreshToken
```csharp
public class AuthResult
    {
        public string Token { get; set; }
        public bool Success { get; set; }
        public List<string> Error { get; set; }

        public string RefreshToken {  get; set; }
    }
```

3. Create New `TokenRequest.cs` dto.
   ```csharp
    public class TokenRequest
    {
        [Required]
        public string Token { get; set; }
        [Required]
        public string RefreshToken { get; set; }
    }
   ```
4. Create new `RefreshToken.cs` Model
   ```csharp
   
    public class RefreshToken
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string Token { get; set; }
        public string JWTId { get; set; }
        public bool IsUsed { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime DateAdded { get; set; }
        public DateTime ExpireDate { get; set; }

        [ForeignKey(nameof(UserId))]
        public IdentityUser User { get; set; }


    }
   ```
5. Update `AppDbContext` to inlcude **RefreshTokens** model.
```csharp
    public class AppDbContext : IdentityDbContext
    {
        public AppDbContext(DbContextOptions options) : base(options)
        {
        }

        public virtual DbSet<ToDoItem> ToDoItems { get; set; }
        public virtual DbSet<RefreshToken> RefreshTokens { get; set; }
    }
```

6. Update `AuthManagerController.cs`
```csharp
 [Route("api/[controller]")]
    [ApiController]
    public class AuthManagerController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly JWTConfig _jwtConfig;
        private readonly TokenValidationParameters _tokenValidationParameters;
        private readonly AppDbContext _appDbContext;

        public AuthManagerController(UserManager<IdentityUser> userManager, IOptionsMonitor<JWTConfig> optionsMonitor, TokenValidationParameters tokenValidationParameters, AppDbContext appDbContext)
        {
            _userManager = userManager;
            _jwtConfig = optionsMonitor.CurrentValue;
            _tokenValidationParameters = tokenValidationParameters;
            _appDbContext = appDbContext;
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
                    var jwtToken = await GenerateJWTToken(newUser);

                    return Ok(jwtToken);
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

                var jwtToken = await GenerateJWTToken(userExists);

                return Ok(jwtToken);
            }

            return BadRequest(new RegistrationResponseDTO()
            {
                Error = new List<string>() { "Invalid Payload" },
                Success = false
            });
        }

        [HttpPost]
        [Route("RefreshToken")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenRequest tokenRequest)
        {
            if(ModelState.IsValid)
            {
                var result = await VerifyAndReGenerateToken(tokenRequest);

                if (result== null)
                {
                    return BadRequest(new RegistrationResponseDTO()
                    {
                        Error = new List<string>() { "Invalid Token" },
                        Success = false
                    });
                }

                return Ok(result);
            }

            return BadRequest(new RegistrationResponseDTO()
            {
                Error = new List<string>() { "Invalid Payload" },
                Success = false
            });
        }

        private async Task<AuthResult> VerifyAndReGenerateToken(TokenRequest tokenRequest)
        {
            var jwtTokenHandler = new JwtSecurityTokenHandler();
            try
            {
                //Validation 1 - Validate JWT token Format
                var tokenInVerification = jwtTokenHandler.ValidateToken(tokenRequest.Token, _tokenValidationParameters, out var validatedToken);

                //Validation 2 - Validate Encryption Algo
                if (validatedToken is JwtSecurityToken jwtSecurityToken)
                {
                    var result = jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase);

                    if(!result)
                    {
                        return null;
                    }
                }

                //Validation 3 - Validate Expire Date of token
                var utcExpirationDate = long.Parse(tokenInVerification.Claims.FirstOrDefault(x=> x.Type == JwtRegisteredClaimNames.Exp).Value);

                var expiryDate = UnixTimeStampDateTime(utcExpirationDate);

                if(expiryDate >  DateTime.UtcNow)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Error = new List<string>() { "Token has not yet expired"}
                    };
                }

                //Validation 4 - Validate if token exists in DB
                var storedToken = await _appDbContext.RefreshTokens.FirstOrDefaultAsync(x => x.Token == tokenRequest.RefreshToken);

                if(storedToken is null)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Error = new List<string>() { "Token does not exist" }
                    };
                }

                //Validation 5- Validate if token is revoked 
                if (storedToken.IsUsed)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Error = new List<string>() { "Token has been used" }
                    };
                }

                //Validation 6 - validate if token is revoked 
                if (storedToken.IsRevoked)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Error = new List<string>() { "Token has been revoked" }
                    };
                }
                //Validation 7
                var jti = tokenInVerification.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti).Value;

                if(storedToken.JWTId != jti)
                {
                    return new AuthResult()
                    {
                        Success = false,
                        Error = new List<string>() { "Token Does not match" }
                    };
                }

                //Update the token

                storedToken.IsUsed = true;
                 _appDbContext.RefreshTokens.Update(storedToken);
                await _appDbContext.SaveChangesAsync();

                //Generate new Token
                var dbuser = await _userManager.FindByIdAsync(storedToken.UserId);
                return await GenerateJWTToken(dbuser);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private DateTime UnixTimeStampDateTime(long utcExpirationDate)
        {
            var dateTimeVal = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            dateTimeVal = dateTimeVal.AddSeconds(utcExpirationDate).ToUniversalTime();
            return dateTimeVal;
        }

        private async Task<AuthResult> GenerateJWTToken(IdentityUser user)
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
                Expires= DateTime.UtcNow.AddSeconds(10), //usually 5-10 mins
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = jwtTokenHandler.CreateToken(tokenDescriptor);
            var JwtToken = jwtTokenHandler.WriteToken(token);

            var refreshToken = new RefreshToken()
            {
                JWTId = token.Id,
                IsUsed = false,
                IsRevoked = false,
                UserId = user.Id,
                DateAdded = DateTime.UtcNow,
                ExpireDate = DateTime.UtcNow.AddMonths(6),
                Token = RandomString(35) + Guid.NewGuid()
            };

            await _appDbContext.RefreshTokens.AddAsync(refreshToken);
            await _appDbContext.SaveChangesAsync();
            return new AuthResult()
            {
                Token = JwtToken,
                Success = true,
                RefreshToken = refreshToken.Token
            };
        }

        private string RandomString(int length)
        {
            var random = new Random();
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            return new string(Enumerable.Repeat(chars, length)
                .Select(x=> x[random.Next(x.Length)]).ToArray());
        }
    }
```