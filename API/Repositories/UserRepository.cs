using MySqlConnector;
using HeistApi.Models;
using HeistApi.Database;

namespace HeistApi.Repositories;

public class UserRepository
{
    private readonly DatabaseConnection _database;

// Constructor to initialize the UserRepository with a DatabaseConnection
    public UserRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<User?> GetByIdAsync(int userId)
{
    using var connection = _database.CreateConnection();
    await connection.OpenAsync();

    const string sql = """
        SELECT user_id, username, email, password_hash, cash
        FROM user
        WHERE user_id = @userId;
        """;

    using var command = new MySqlCommand(sql, connection);

    command.Parameters.AddWithValue("@userId", userId);

    using var reader = await command.ExecuteReaderAsync();

    if (!await reader.ReadAsync())
        return null;

    return new User
    {
        UserId = reader.GetInt32("user_id"),
        Username = reader.GetString("username"),
        Email = reader.GetString("email"),
        PasswordHash = reader.GetString("password_hash"),
        Cash = reader.GetInt32("cash")
    };
}
//retrieve user by username
public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT user_id, username, email, password_hash, cash
            FROM user
            WHERE username = @username;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@username", username);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapUser(reader);
    }
// Method to retrieve a user by their email address
public async Task<User?> GetByEmailAsync(string email)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT user_id, username, email, password_hash, cash
            FROM user
            WHERE email = @email;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@email", email);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapUser(reader);
    }

    
    
    //Create a new user in the database
    public async Task<int> CreateAsync(User user)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            INSERT INTO user
                (username, email, password_hash, cash)
            VALUES
                (@username, @email, @passwordHash, @cash);
            """;

        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@email", user.Email);
        command.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@cash", 1000); // Default cash value
        

        await command.ExecuteNonQueryAsync();

        return (int)command.LastInsertedId;
    }
    // Update an existing user's information in the database
    public async Task<bool> UpdateAsync(int userId, User user)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE user
            SET username = @username,
                email = @email,
                cash = @cash
            WHERE user_id = @userId;
            """;

        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@email", user.Email);
        command.Parameters.AddWithValue("@cash", user.Cash);
        command.Parameters.AddWithValue("@userId", userId);

        var rowsAffected = await command.ExecuteNonQueryAsync();

        return rowsAffected > 0;
    }
    // Update password hash
    public async Task<bool> UpdatePasswordAsync(
        int userId,
        string passwordHash)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE user
            SET password_hash = @passwordHash
            WHERE user_id = @userId;
            """;

        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@passwordHash", passwordHash);
        command.Parameters.AddWithValue("@userId", userId);

        var rowsAffected = await command.ExecuteNonQueryAsync();

        return rowsAffected > 0;
    }

    // Set cash balance
    public async Task<bool> UpdateCashAsync(int userId, int cash)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            UPDATE user
            SET cash = @cash
            WHERE user_id = @userId;
            """;

        using var command = new MySqlCommand(sql, connection);

        command.Parameters.AddWithValue("@cash", cash);
        command.Parameters.AddWithValue("@userId", userId);

        var rowsAffected = await command.ExecuteNonQueryAsync();

        return rowsAffected > 0;
    }

    // Delete user
    public async Task<bool> DeleteAsync(int userId)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            DELETE FROM user
            WHERE user_id = @userId;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@userId", userId);

        var rowsAffected = await command.ExecuteNonQueryAsync();

        return rowsAffected > 0;
    }

    // Convert database row into User model
    private static User MapUser(MySqlDataReader reader)
    {
        return new User
        {
            UserId = reader.GetInt32("user_id"),
            Username = reader.GetString("username"),
            Email = reader.GetString("email"),
            PasswordHash = reader.GetString("password_hash"),
            Cash = reader.GetInt32("cash")
        };
    }
}