using MySqlConnector;
using HeistApi.Models;

namespace HeistApi.Repositories;

public class EquipmentRepository
{
    private readonly DatabaseConnection _database;

    public EquipmentRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<Equipment?> GetByIdAsync(int equipmentId)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT equipment_id, name
            FROM equipment
            WHERE equipment_id = @equipmentId;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@equipmentId", equipmentId);

        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            return null;

        return MapEquipment(reader);
    }

    public async Task<List<Equipment>> GetAllAsync()
    {
        var equipment = new List<Equipment>();

        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            SELECT equipment_id, name
            FROM equipment;
            """;

        using var command = new MySqlCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            equipment.Add(MapEquipment(reader));
        }

        return equipment;
    }

    private static Equipment MapEquipment(MySqlDataReader reader)
    {
        return new Equipment
        {
            EquipmentId = reader.GetInt32("equipment_id"),
            Name = reader.GetString("name")
        };
    }
}