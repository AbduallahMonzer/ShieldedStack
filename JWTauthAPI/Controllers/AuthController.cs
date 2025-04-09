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

    public AuthController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("Missing DB connection string");
        _jwtSecret = configuration["JwtSettings:SecretKey"] ??
            throw new InvalidOperationException("JWT Secret Key is missing");
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

            insertCmd.Parameters.Add("username", NpgsqlTypes.NpgsqlDbType.Varchar).Value = user.Username;

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

            Response.Cookies.Append("AuthToken", token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddHours(3)
            });

            return Ok(new { message = "Signup and login successful", userId = newUserId, token });
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
                if(reader != null && !reader.IsClosed)
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

            Response.Cookies.Append("AuthToken", token, new CookieOptions
            {
                HttpOnly = true,  
                Secure = true,    
                SameSite = SameSiteMode.Strict,  
                Expires = DateTime.UtcNow.AddHours(3)  
            });

            return Ok("Login successful");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

[HttpGet("me")]
public IActionResult Me()
{
    var username = User.Identity?.Name;
    var role = User.Claims.FirstOrDefault(c => c.Type == "role")?.Value;

    if (string.IsNullOrEmpty(username))
        return Unauthorized();

    return Ok(new { username, role });
}

private string GenerateJwtToken(string username)
{
    var conn = new NpgsqlConnection(_connectionString);
    try
    {
        conn.Open();
        var cmd = new NpgsqlCommand(@"SELECT role FROM user_account
            WHERE username = @username", conn);
        
        var param = new NpgsqlParameter("username", NpgsqlTypes.NpgsqlDbType.Text);
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