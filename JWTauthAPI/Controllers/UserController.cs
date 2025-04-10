using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using System;
using System.Collections.Generic;
using JwtAuthApi.Models;

namespace JwtAuthApi.Controllers
{
    [ApiController]
    [Route("api")]
    public class UserController : ControllerBase
    {
    private readonly string _connectionString;
    public UserController(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Missing DB connection string");
    }

[HttpPost("new")]
[Authorize]
public IActionResult CompleteUserProfile([FromBody] User user)
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
        var checkCmd = new NpgsqlCommand(
            @"SELECT COUNT(*) 
            FROM user_account 
            WHERE username = @username",
            conn);

        var checkParam = new NpgsqlParameter("username", 
            NpgsqlTypes.NpgsqlDbType.Varchar) { Value = user.Username };
        checkCmd.Parameters.Add(checkParam);

        long? userCount = checkCmd.ExecuteScalar() as long?;

        if (userCount == 0)
        {
            return NotFound("User not found. Please sign up first.");
        }

        var updateCmd = new NpgsqlCommand(
            @"UPDATE user_account SET
            password = @password, 
            email = @email,
            phone_number = @phone_number,
            role = @role
            WHERE username = @username", conn);

        if (!string.IsNullOrEmpty(user.Password))
        {
            string salt = PasswordHashUtility.GenerateSalt();
            string hashedPassword = PasswordHashUtility.HashPassword(user.Password, salt);

            var passwordParam = new NpgsqlParameter("password",     
                NpgsqlTypes.NpgsqlDbType.Varchar) { Value = hashedPassword };
            var saltParam = new NpgsqlParameter("salt", 
                NpgsqlTypes.NpgsqlDbType.Varchar) { Value = salt };

            updateCmd.Parameters.Add(passwordParam);
            updateCmd.Parameters.Add(saltParam);
        }
        else
        {
            var passwordParam = new NpgsqlParameter("password", 
                NpgsqlTypes.NpgsqlDbType.Varchar) { Value = DBNull.Value };
            var saltParam = new NpgsqlParameter("salt", 
                NpgsqlTypes.NpgsqlDbType.Varchar) { Value = DBNull.Value };

            updateCmd.Parameters.Add(passwordParam);
            updateCmd.Parameters.Add(saltParam);
        }

        var emailParam = new NpgsqlParameter("email", 
            NpgsqlTypes.NpgsqlDbType.Varchar) 
                { Value = string.IsNullOrEmpty(user.Email) ? DBNull.Value : user.Email! };
        var phoneParam = new NpgsqlParameter("phone_number", 
            NpgsqlTypes.NpgsqlDbType.Varchar) 
                { Value = string.IsNullOrEmpty(user.PhoneNumber) ? DBNull.Value : user.PhoneNumber!};
        var roleParam = new NpgsqlParameter("role", 
            NpgsqlTypes.NpgsqlDbType.Varchar) 
                { Value = string.IsNullOrEmpty(user.Role) ? DBNull.Value : user.Role! };
        var usernameParam = new NpgsqlParameter("username", 
        NpgsqlTypes.NpgsqlDbType.Varchar) 
            { Value = user.Username };

        updateCmd.Parameters.Add(emailParam);
        updateCmd.Parameters.Add(phoneParam);
        updateCmd.Parameters.Add(roleParam);
        updateCmd.Parameters.Add(usernameParam);

        int rowsAffected = updateCmd.ExecuteNonQuery();
        if (rowsAffected == 0)
        {
            return StatusCode(500, "Failed to update user profile.");
        }

        return Ok(new
        {
            Message = "User profile updated successfully.",
            Username = user.Username,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"Internal server error: {ex.Message}");
    }
    finally
    {
        conn?.Close();
    }
}

        
    [HttpGet("list_users")]
    public IActionResult GetUsers()
    {
    // used it manual instead of using [Authorize(Roles = "admin")]
    if (HttpContext.User.Claims.FirstOrDefault(c => c.Type == "role" && c.Value == "admin") == null)
        return Unauthorized();

    NpgsqlConnection? conn = null;
    NpgsqlDataReader? reader = null;
    
    try
    {
        conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        var cmd = new NpgsqlCommand(@"SELECT 
        id, 
        username, 
        email, 
        phone_number, 
        role FROM user_account", 
        conn);
        reader = cmd.ExecuteReader();

        var users = new List<User>();

        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.IsDBNull(2) ? null : reader.GetString(2),
                PhoneNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                Role = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return Ok(users);
    }
    catch (Exception ex)
    {
        return StatusCode(500, $"Internal server error: {ex.Message}");
    }
            finally
            {
                if (conn != null)
                    conn.Close();
            }
        }
    }
}