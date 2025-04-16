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
            var parameter = new NpgsqlParameter(paramName, NpgsqlTypes.NpgsqlDbType.Varchar)
            {
                Value = value ?? DBNull.Value
            };
            cmd.Parameters.Add(parameter);
        }

        [HttpPost("new")]
        [Authorize]
        public IActionResult CompleteUserProfile([FromBody] User user)
        {
            if (user == null || string.IsNullOrEmpty(user.Username) 
                || string.IsNullOrEmpty(user.Password))
                return BadRequest("Username and Password are required");

            NpgsqlConnection conn = null!;
            try
            {
                conn = OpenConnection();

                var checkCmd = new NpgsqlCommand(
                    @"SELECT COUNT(*) FROM user_account WHERE username = @username", conn);
                AddParameter(checkCmd, "username", user.Username);

                var userCount = checkCmd.ExecuteScalar() as long?;

                if (userCount == 0)
                    return NotFound("User not found. Please sign up first.");

                var updateCmd = new NpgsqlCommand(
                    @"UPDATE user_account 
                    SET password = @password, 
                    email = @email,
                    phone_number = @phone_number, 
                    role = @role 
                    WHERE username = @username", 
                    conn);

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
                    Message = "User profile updated successfully.",
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
                if (conn != null)
                {
                    conn.Close();
                }
            }
        }

        [HttpGet("list_users")]
        [Authorize(Roles = "admin")]
        public IActionResult GetUsers()
        {
            NpgsqlConnection? conn = null;
            NpgsqlDataReader? reader = null;

            try
            {
                conn = OpenConnection();

                var cmd = new NpgsqlCommand(@"SELECT id, username, email, phone_number, role 
                    FROM user_account", conn);
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
                _logger.LogError(ex, "Error occurred while fetching users list");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
                if (conn != null)
                {
                    conn.Close();
                }
            }
        }
    }
}
