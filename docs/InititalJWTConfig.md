## Functionality for Authentication using JWT

##### Add Packages
1. `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
2. `Microsoft.AspNetCore.Authentication.JwtBearer`

##### JWT configuration
1. Add <b><i>Secret key</i></b> in **aspsettings.json**
```json
 "JWTConfig": {
  "Secret": "Super secret string used for encryption"
}
```

2. Add a **Configuration folder** folder.
3. Add **JWTConfig.cs** file which will have same sturcture as `JWTConfig` in **aspsettings.json**
```csharp
public class JWTConfig
    {
        public string Secret { get; set; }
    }
```
4. Make changes in **Program.cs** file for JWt Configuration and setup
```csharp
builder.Services.Configure<JWTConfig>(builder.Configuration.GetSection("JWTConfig"));

//existing 
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(jwt =>
{
    var key = Encoding.ASCII.GetBytes(builder.Configuration["JWTConfig:Secret"]);

    jwt.SaveToken = true;
    jwt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        RequireExpirationTime = false,

    };
});

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    // Default Lockout settings.
    options.SignIn.RequireConfirmedAccount = true;
}).AddEntityFrameworkStores<AppDbContext>();

...
app.UseAuthentication();
app.UseAuthorization();

```

> [!IMPORTANT]
> Will need to install an additional packages `Microsoft.AspNetCore.Identity.UI`


Next:[Add Auth Manager Endpoint](./AuthManagerEndPoint.md)