using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Npgsql;
using System;
using System.Collections.Generic;
using JwtAuthApi.Models;
using Microsoft.Extensions.Logging;

namespace JwtAuthApi.Controllers
{
[ApiController]
[Route("api")]
public class UserController : ControllerBase
{
    private readonly string _connectionString;
    private readonly ILogger<UserController> _logger;

    public UserController(IConfiguration configuration, ILogger<UserController> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing DB connection string");
        _logger = logger;
    }

    private NpgsqlConnection OpenConnection()
    {
        var connection = new NpgsqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void AddParameter(NpgsqlCommand cmd, string paramName, object value)
    {
        var parameter = new NpgsqlParameter(paramName, value ?? DBNull.Value);
        cmd.Parameters.Add(parameter);
    }

[HttpPost("new")]
[Authorize]
public IActionResult CompleteUserProfile([FromBody] User user)
{
    if (user == null || string.IsNullOrEmpty(user.Username) 
        || string.IsNullOrEmpty(user.Password))
    return BadRequest("Username and Password are required");

    NpgsqlConnection? conn = null;

    try
    {
        conn = OpenConnection();
        var checkCmd = new NpgsqlCommand(@"SELECT COUNT(*) 
            FROM user_account 
            WHERE username = @username", 
            conn);
        AddParameter(checkCmd, "username", user.Username);
        var userCount = (long)(checkCmd.ExecuteScalar() ?? 0);

        if (userCount == 0)
        return NotFound("User not found. Please sign up first.");

        var updateCmd = new NpgsqlCommand(
            @"UPDATE user_account 
            SET password = @password, 
            salt = @salt,
            email = @email,
            phone_number = @phone_number, 
            role = @role 
            WHERE username = @username", conn);

        if (!string.IsNullOrEmpty(user.Password))
        {
            var salt = PasswordHashUtility.GenerateSalt();
            var hashedPassword = PasswordHashUtility.HashPassword(user.Password, salt);

            AddParameter(updateCmd, "password", hashedPassword);
            AddParameter(updateCmd, "salt", salt);
        } 
        else
            {
                AddParameter(updateCmd, "password", DBNull.Value);
                AddParameter(updateCmd, "salt", DBNull.Value);
            }

                AddParameter(updateCmd, "email", user.Email!);
                AddParameter(updateCmd, "phone_number", user.PhoneNumber!);
                AddParameter(updateCmd, "role", user.Role!);
                AddParameter(updateCmd, "username", user.Username);

                int rowsAffected = updateCmd.ExecuteNonQuery();

                if (rowsAffected == 0)
                    return StatusCode(500, "Failed to update user profile.");

                return Ok(new
                {
                    message = "User profile updated successfully.",
                    user.Username,
                    user.Email,
                    user.PhoneNumber,
                    user.Role
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while updating user profile");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
            finally
            {
                conn?.Close();
            }
        }

[HttpGet("list_users")]
[Authorize(Roles = "admin")]
public IActionResult GetUsers([FromQuery] int page = 1, [FromQuery] int limit = 10)
{
    NpgsqlConnection? conn = null;
    NpgsqlDataReader? reader = null;

    if (page <= 0 || limit <= 0)
    {
        return BadRequest("Page and limit must be positive integers.");
    }

    try
    {
        conn = OpenConnection();

        int offset = (page - 1) * limit;

        var cmd = new NpgsqlCommand(@$"
            SELECT 
                id,     
                username, 
                email, 
                phone_number, 
                role 
            FROM user_account
            ORDER BY username
            LIMIT @limit OFFSET @offset", conn);


        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("offset", offset);

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


        var totalCountCmd = new NpgsqlCommand("SELECT COUNT(*) FROM user_account", conn);
        var totalCount = (long?)totalCountCmd.ExecuteScalar() ?? 0;



        int totalPages = (int)Math.Ceiling(totalCount / (double)limit);


        return Ok(new
        {
            Users = users,
            TotalCount = totalCount,
            TotalPages = totalPages,
            CurrentPage = page
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error occurred while fetching users list");
        return StatusCode(500, $"Internal server error: {ex.Message}");
    }
    finally
    {
        reader?.Close();
        conn?.Close();
    }
}



[HttpPost("update_role")]
[Authorize(Roles = "admin")]
public IActionResult UpdateUserRole([FromBody] RoleUpdateModel model)
{
    if (model == null || model.UserId <= 0 
        || string.IsNullOrWhiteSpace(model.NewRole))
        return BadRequest("Invalid role update request.");

    NpgsqlConnection? conn = null;

    try
        {
            conn = OpenConnection();
            var cmd = new NpgsqlCommand(@"UPDATE user_account  
                SET role = @role   
                WHERE id = @id", 
                conn);

            cmd.Parameters.Add("id", 
                NpgsqlTypes.NpgsqlDbType.Integer).Value = model.UserId;
            cmd.Parameters.Add("role", 
                NpgsqlTypes.NpgsqlDbType.Varchar).Value = model.NewRole.ToLower();

            int rowsAffected = cmd.ExecuteNonQuery();
            if (rowsAffected == 0)
                return NotFound("User not found.");
                
            return Ok(new { message = "Role updated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user role");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
        finally
            {
                conn?.Close();
            }
        }
        public class RoleUpdateModel
{
    public int UserId { get; set; }
    public string? NewRole { get; set; }
}
[Authorize(Roles = "admin")]
[HttpGet("user/{id}")]
public IActionResult GetUserById(int id)
{
    using var conn = OpenConnection();
    var cmd = new NpgsqlCommand(@"
        SELECT id, username, role 
        FROM user_account 
        WHERE id = @id", conn);

    cmd.Parameters.Add("id", NpgsqlTypes.NpgsqlDbType.Integer).Value = id;
    using var reader = cmd.ExecuteReader();
    if (reader.Read())
    {
        var user = new {
            id = reader.GetInt32(0),
            username = reader.GetString(1),
            role = reader.GetString(2)
        };
        return Ok(user);
    }

    return NotFound();
}

    }
}
