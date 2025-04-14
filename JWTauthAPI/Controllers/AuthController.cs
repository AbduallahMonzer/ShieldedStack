using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using JwtAuthApi.Models;

namespace JwtAuthApi.Controllers;

[ApiController]
[Authorize]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly string _connectionString;
    private readonly string _jwtSecret;
    private readonly IConfiguration _OAuthConfig;

    public AuthController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("Missing DB connection string");
        _jwtSecret = configuration["JwtSettings:SecretKey"] ??
            throw new InvalidOperationException("JWT Secret Key is missing");
        _OAuthConfig = configuration;
    }
[HttpGet("oauth/login")]
public IActionResult RedirectToOAuthProvider()
{
    var clientId = _OAuthConfig["OAuth:ClientId"];
    var redirectUri = _OAuthConfig["OAuth:RedirectUri"];

    if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
    {
        return BadRequest("Missing OAuth configuration. Please check ClientId and RedirectUri.");
    }

    var authUrl = "https://mail.lifecapital.eg/oauth/authorize";

    var queryParams = new Dictionary<string, string>
    {
        {"response_type", "code"},
        {"client_id", clientId},
        {"redirect_uri", redirectUri},
        {"scope", "profile email"},
        {"state", Guid.NewGuid().ToString()}
    };

    var queryString = string.Join("&", queryParams.Select(kv =>
        $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    var fullUrl = $"{authUrl}?{queryString}";

    return Redirect(fullUrl);
}
    

    [HttpPost("signup")]
    [AllowAnonymous]
    public IActionResult SignUp([FromBody] User user)
    {
        if (user == null || string.IsNullOrEmpty(user.Username) ||
            string.IsNullOrEmpty(user.Password))
        {
            return BadRequest("Username and Password are required");
        }

        NpgsqlConnection? conn = null;

        try
        {
            conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            var checkCmd = new NpgsqlCommand(@"
                SELECT COUNT(*)
                FROM user_account 
                WHERE username = @username", conn);

            checkCmd.Parameters.Add("@username",
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = user.Username;
            long? userCount = (long?)checkCmd.ExecuteScalar();

            if (userCount > 0)
            {
                return Conflict("Username already exists");
            }

            var insertCmd = new NpgsqlCommand(@"INSERT INTO user_account
                (username, password, salt, email, phone_number, role)
                VALUES (@username, @password, @salt, @email, @phone_number, @role)
                RETURNING id", conn);

            insertCmd.Parameters.Add("username", 
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = user.Username;

            string salt = PasswordHashUtility.GenerateSalt();
            string hashedPassword = PasswordHashUtility.HashPassword(user.Password, salt);

            insertCmd.Parameters.Add("password",
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = hashedPassword;
            insertCmd.Parameters.Add("salt",
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = salt;
            insertCmd.Parameters.Add("email",
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = user.Email ?? (object)DBNull.Value;
            insertCmd.Parameters.Add("phone_number",
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = user.PhoneNumber ?? (object)DBNull.Value;
            insertCmd.Parameters.Add("role",
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = user.Role ?? "user";

            var newUserId = insertCmd.ExecuteScalar();
            string token = GenerateJwtToken(user.Username);
            Cookie.AppendAuthToken(Response, token);

            return Ok(new { message = "Signup and login successful", userId = newUserId });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, $"Error occurred during signup: {ex.Message}");
        }
        finally
        {
            conn?.Close();
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest loginRequest)
    {
        if (string.IsNullOrEmpty(loginRequest?.Username)
            || string.IsNullOrEmpty(loginRequest?.Password))
        {
            return BadRequest("Username and Password are required");
        }

        try
        {
            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();
            var cmd = new NpgsqlCommand(
                "SELECT password, salt FROM user_account WHERE username = @username", conn);
            cmd.Parameters.AddWithValue("username", loginRequest.Username);

            var reader = cmd.ExecuteReader();
            string? storedHashedPassword = null;
            string? storedSalt = null;
            try
            {
                if (!reader.Read())
                {
                    return Unauthorized("Invalid username or password");
                }

                storedHashedPassword = reader.GetString(0);
                storedSalt = reader.GetString(1);
            }
            finally
            {
                if (reader != null && !reader.IsClosed)
                    reader.Close();
            }

            string hashedInputPassword = PasswordHashUtility.
                HashPassword(loginRequest.Password, storedSalt);
            if (!hashedInputPassword.Equals(storedHashedPassword,
                StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized("Invalid username or password");
            }

            string token = GenerateJwtToken(loginRequest.Username);
            Cookie.AppendAuthToken(Response, token);

            return Ok(new { message = "Login successful" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // [HttpPost("logout")]
    // public IActionResult Logout()
    // {
    //     Response.Cookies.Delete("AuthToken");
    //     return Ok(new { message = "Logged out" });
    // }

    public static class Cookie
    {
        public static void AppendAuthToken(HttpResponse response, string token)
            {
                response.Cookies.Append("AuthToken", token, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Expires = DateTime.UtcNow.AddHours(3)
                });
            }
    }

    [HttpPost("token/verify-refresh")]
        public IActionResult VerifyAndRefresh()
            {
                var usernameClaim = HttpContext.User.Claims.
                    FirstOrDefault(c => c.Type == ClaimTypes.Name);
                if (usernameClaim == null)
                {
                    return Unauthorized();
                }
                string token = GenerateJwtToken(usernameClaim.Value);
                Cookie.AppendAuthToken(Response, token);
                return Ok(new { message = "Token verified and refreshed" });
            }

    private string GenerateJwtToken(string username)
    {
        var conn = new NpgsqlConnection(_connectionString);
        try
        {
            conn.Open();
            var cmd = new NpgsqlCommand(@"SELECT role 
            FROM user_account
            WHERE username = @username",
                conn);
            
            var param = new NpgsqlParameter("username",
                NpgsqlTypes.NpgsqlDbType.Text);
            param.Value = username;
            cmd.Parameters.Add(param);

            var role = (string?)cmd.ExecuteScalar() ?? "user";
            conn.Close();

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim("role", role),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(
                issuer: "Issuer",
                audience: "Audience",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(3),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        finally
        {
            if (conn.State == System.Data.ConnectionState.Open)
            {
                conn.Close();
            }
        }
    }
}

public class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}
