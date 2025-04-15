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
        {
            return BadRequest("Missing authorization code");
        }

        var tokenUrl = "https://mail.lifecapital.eg/oauth/token";
        var profileUrl = "https://mail.lifecapital.eg/oauth/profile";
        var redirectUri = "https://localhost:3000/oauth/callback";

        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(10);

        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", redirectUri },
            { "client_id", _clientId },
            { "client_secret", _clientSecret }
        });

        var tokenResponse = await httpClient.SendAsync(tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return StatusCode((int)tokenResponse.StatusCode, 
                "Failed to exchange token");
        }

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
        if (!tokenData.TryGetProperty("access_token", out var accessTokenElement))
        {
            return BadRequest("Access token not found in response");
        }

        var accessToken = accessTokenElement.GetString();
        var profileRequest = new HttpRequestMessage(HttpMethod.Get, profileUrl);
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var profileResponse = await httpClient.SendAsync(profileRequest);
        if (!profileResponse.IsSuccessStatusCode)
        {
            return StatusCode((int)profileResponse.StatusCode, "Failed to get profile");
        }

        var profileJson = await profileResponse.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<JsonElement>(profileJson);

        var email = profile.GetProperty("email").GetString();
        var username = email?.Split('@')[0];

        if (string.IsNullOrEmpty(username))
        {
            return BadRequest("Invalid profile info");
        }

        NpgsqlConnection ?conn = null;
        try
        {
            conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            var checkCmd = new NpgsqlCommand(@"SELECT COUNT(*) 
                FROM user_account 
                WHERE username = @username", 
                conn);
            checkCmd.Parameters.Add("@username", 
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = username;
                object? result = checkCmd.ExecuteScalar();
                long count = result is DBNull or null ? 0 : Convert.ToInt64(result);


            if (count == 0)
            {
                var insertCmd = new NpgsqlCommand(@"INSERT INTO user_account 
                    (username, 
                    password, 
                    salt, 
                    email, 
                    role) 
                    VALUES (@username, 
                            @password, 
                            @salt, 
                            @email, 
                            @role)", 
                            conn);
                insertCmd.Parameters.Add("@username", 
                    NpgsqlTypes.NpgsqlDbType.Varchar).Value = username;
                insertCmd.Parameters.Add("@password", 
                    NpgsqlTypes.NpgsqlDbType.Varchar).Value = "oauth";
                insertCmd.Parameters.Add("@salt", 
                    NpgsqlTypes.NpgsqlDbType.Varchar).Value = "oauth";
                insertCmd.Parameters.Add("@email", 
                    NpgsqlTypes.NpgsqlDbType.Varchar).Value = email;
                insertCmd.Parameters.Add("@role", 
                    NpgsqlTypes.NpgsqlDbType.Varchar).Value = "user";
                insertCmd.ExecuteNonQuery();
            }

            string jwt = GenerateJwtToken(username);
            Cookie.AppendAuthToken(Response, jwt);
            return Ok();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, "Internal server error during OAuth");
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
                    return Unauthorized("Invalid username or password X");
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
                HashPassword(loginRequest.Password,storedSalt);
            if (!hashedInputPassword.Equals(storedHashedPassword,
                StringComparison.OrdinalIgnoreCase))
            {
                return Unauthorized("Invalid username or password Y");
            }

            string token = GenerateJwtToken(loginRequest.Username);
            Cookie.AppendAuthToken(HttpContext.Response, token);

            return Ok(new { message = "Login successful" });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
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
                    Domain = ".localhost",
                    SameSite = SameSiteMode.Lax,
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
