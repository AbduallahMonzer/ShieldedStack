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
using System.Net.Http.Headers;
using System.Text.Json;

namespace JwtAuthApi.Controllers;

[ApiController]
[Authorize]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly string _connectionString;
    private readonly string _jwtSecret;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly IHttpClientFactory _httpClientFactory;

    public AuthController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DB connection string");
        _jwtSecret = configuration["JwtSettings:SecretKey"]
            ?? throw new InvalidOperationException("JWT Secret Key is missing");
        _clientId = configuration["OAuth:ClientId"]
            ?? throw new InvalidOperationException("OAuth ClientId is missing");
        _clientSecret = configuration["OAuth:ClientSecret"]
            ?? throw new InvalidOperationException("OAuth ClientSecret is missing");
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("oauth/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> OAuthCallback(string code)
    {
        if (string.IsNullOrEmpty(code))
            return ResponseHelper.HandleMissing("Authorization code");

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        var tokenRequest = new HttpRequestMessage
            (HttpMethod.Post, "https://mail.lifecapital.eg/oauth/token");
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", "https://localhost:3000/oauth/callback" },
            { "client_id", _clientId },
            { "client_secret", _clientSecret }
        });

        var tokenResponse = await httpClient.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
            return StatusCode((int)tokenResponse.StatusCode, "Failed to exchange token");

        var tokenData = JsonSerializer.Deserialize<JsonElement>
            (await tokenResponse.Content.ReadAsStringAsync());
        if (!tokenData.TryGetProperty("access_token", out var accessTokenElement))
            return BadRequest("Access token not found");

        var profileRequest = new HttpRequestMessage(HttpMethod.Get, 
            "https://mail.lifecapital.eg/oauth/profile");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue    
            ("Bearer", accessTokenElement.GetString());
        var profileResponse = await httpClient.SendAsync(profileRequest);

        if (!profileResponse.IsSuccessStatusCode)
            return StatusCode((int)profileResponse.StatusCode, "Failed to get profile");

        var profile = JsonSerializer.Deserialize<JsonElement>
            (await profileResponse.Content.ReadAsStringAsync());
        var email = profile.GetProperty("email").GetString();
        var username = email?.Split('@')[0];

        if (string.IsNullOrEmpty(username))
            return BadRequest("Invalid profile info");

        NpgsqlConnection? conn = null;
        try
        {
            conn = DbHelper.OpenConnection(_connectionString);

            var checkCmd = DbHelper.CreateCommand(conn, @"
                SELECT COUNT(*) FROM user_account WHERE username = @username", new()
            {
                { "@username", username }
            });

            var result = checkCmd.ExecuteScalar();
            var exists = result is not null && Convert.ToInt64(result) > 0;

            if (!exists)
            {
                var insertCmd = DbHelper.CreateCommand(conn, @"
                    INSERT INTO user_account 
                        (username, 
                        password, 
                        salt, 
                        email, 
                        role)
                        VALUES 
                        (@username, 
                        @password, 
                        @salt, 
                        @email, 
                        @role)", new()
                {
                    { "@username", username },
                    { "@password", "oauth" },
                    { "@salt", "oauth" },
                    { "@email", email },
                    { "@role", "user" }
                });

                insertCmd.ExecuteNonQuery();
            }

            var role = exists ? DbHelper.GetUserRole(conn, username) : "user";
            var token = JwtHelper.GenerateToken(username, role, _jwtSecret);
            CookieHelper.AppendAuthToken(Response, token);
            return Ok();
        }
        catch (Exception ex)
        {
            return ResponseHelper.HandleServerError("OAuth", ex);
        }
        finally
        {
            conn?.Close();
        }
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    public IActionResult SignUp([FromBody] User user)
    {
        if (user == null || string.IsNullOrEmpty(user.Username)
            || string.IsNullOrEmpty(user.Password))
            return ResponseHelper.HandleMissing("Username and Password");

        NpgsqlConnection? conn = null;
        try
        {
            conn = DbHelper.OpenConnection(_connectionString);

            var checkCmd = DbHelper.CreateCommand(conn, @"
                SELECT COUNT(*) 
                FROM user_account 
                WHERE username = @username", 
                new()
            {
                { "@username", user.Username }
            });

            if ((long?)checkCmd.ExecuteScalar() > 0)
                return Conflict("Username already exists");

            var salt = PasswordHashUtility.GenerateSalt();
            var hashed = PasswordHashUtility.HashPassword(user.Password, salt);

            var insertCmd = DbHelper.CreateCommand(conn, @"
                INSERT INTO user_account 
                    (username, 
                    password, 
                    salt, 
                    email, 
                    phone_number, 
                    role)
                    VALUES 
                        (@username, 
                        @password, 
                        @salt, 
                        @email, 
                        @phone_number, 
                        @role)
                        RETURNING id", new()
            {
                { "@username", user.Username },
                { "@password", hashed },
                { "@salt", salt },
                { "@email", user.Email },
                { "@phone_number", user.PhoneNumber },
                { "@role", user.Role ?? "user" }
            });

            var userId = insertCmd.ExecuteScalar();
            var token = JwtHelper.GenerateToken(user.Username, user.Role ?? "user", _jwtSecret);
            CookieHelper.AppendAuthToken(Response, token);

            return Ok(new { message = "Signup and login successful", userId });
        }
        catch (Exception ex)
        {
            return ResponseHelper.HandleServerError("signup", ex);
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
            return ResponseHelper.HandleMissing("Username and Password");

        try
        {
            var conn = DbHelper.OpenConnection(_connectionString);

            var cmd = DbHelper.CreateCommand(conn, @"SELECT 
                password,  
                salt, 
                role 
                FROM user_account 
                WHERE username = @username", new()
            {
                { "@username", loginRequest.Username }
            });

            var reader = cmd.ExecuteReader();
            string? hashed = null, salt = null, role = "user";

            if (reader.Read())
            {
                hashed = reader.GetString(0);
                salt = reader.GetString(1);
                role = reader.GetString(2);
            }
            reader.Close();
            conn.Close();

            var hashedInput = PasswordHashUtility.HashPassword(loginRequest.Password, salt!);
            if (!hashedInput.Equals(hashed, StringComparison.OrdinalIgnoreCase))
                return Unauthorized("Invalid username or password");

            var token = JwtHelper.GenerateToken(loginRequest.Username, role, _jwtSecret);
            CookieHelper.AppendAuthToken(Response, token);
            return Ok(new { message = "Login successful" });
        }
        catch (Exception ex)
        {
            return ResponseHelper.HandleServerError("login", ex);
        }
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AuthToken");
        return Ok(new { message = "Logged out" });
    }

    [HttpPost("token/verify-refresh")]
    public IActionResult VerifyAndRefresh()
    {
        var username = HttpContext.User.Claims.FirstOrDefault
            (c => c.Type == ClaimTypes.Name)?.Value;
        if (username == null) return Unauthorized();

        var role = DbHelper.GetUserRole(DbHelper.OpenConnection(_connectionString), username);
        var token = JwtHelper.GenerateToken(username, role, _jwtSecret);
        CookieHelper.AppendAuthToken(Response, token);
        return Ok(new { message = "Token verified and refreshed" });
    }
}

public static class CookieHelper
{
    public static void AppendAuthToken(HttpResponse response, string token)
    {
        response.Cookies.Append("AuthToken", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            Domain = ".localhost",
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddHours(3)
        });
    }
}

public static class DbHelper
{
    public static NpgsqlConnection OpenConnection(string connStr)
    {
        var conn = new NpgsqlConnection(connStr);
        conn.Open();
        return conn;
    }

    public static NpgsqlCommand CreateCommand(NpgsqlConnection conn, 
        string sql, Dictionary<string, object?> parameters)
    {
        var cmd = new NpgsqlCommand(sql, conn);
        foreach (var param in parameters)
        {
            cmd.Parameters.Add(param.Key, 
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = param.Value ?? DBNull.Value;
        }
        return cmd;
    }

    public static string GetUserRole(NpgsqlConnection conn, string username)
    {
        var cmd = CreateCommand(conn, @"SELECT role    
        FROM user_account 
        WHERE username = @username", 
        new() { { "@username", username } });
        return (string?)cmd.ExecuteScalar() ?? "user";
    }
}

public static class JwtHelper
{
    public static string GenerateToken(string username, string role, string secret)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

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
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public static class ResponseHelper
{
    public static IActionResult HandleMissing(string name) =>
        new BadRequestObjectResult($"{name} is required");

    public static IActionResult HandleServerError(string context, Exception ex)
    {
        Console.WriteLine(ex);
        return new ObjectResult($"Internal error during {context}") { StatusCode = 500 };
    }
}

public class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}