using MySqlConnector;
using HeistApi.Models;
using HeistApi.DTOs;

namespace HeistApi.Repositories;

public class CharacterRepository
{
    private readonly DatabaseConnection _database;

    public CharacterRepository(DatabaseConnection database)
    {
        _database = database;
    }

    public async Task<CharacterDto?> GetByIdAsync(int characterId)
    {
        // Create a connection to the database
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();
    //pull the base character data from the characters table
        const string sql = """
            SELECT character_id,
                owner_id,
                character_name,
                character_level,
                character_xp,
                character_class,
                available_for_hire
            FROM characters
            WHERE character_id = @characterId;
            """;
        
        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@characterId", characterId);

        int id;
        int ownerId;
        string name;
        int level;
        int xp;
        string characterClass;
        bool availableForHire;
    //
        using (var reader = await command.ExecuteReaderAsync())
    {
        if (!await reader.ReadAsync())
            return null;

        id = reader.GetInt32("character_id");
        ownerId = reader.GetInt32("owner_id");
        name = reader.GetString("character_name");
        level = reader.GetInt32("character_level");
        xp = reader.GetInt32("character_xp");
        characterClass = reader.GetString("character_class");
        availableForHire = reader.GetBoolean("available_for_hire");
    }

    // reader is now closed
    // Now, retrieve the character stats from the character_stats table
    const string statsSql = """
        SELECT strength,
            agility,
            intelligence,
            dexterity,
            charisma,
            perception
        FROM character_stats
        WHERE character_id = @characterId;
        """;

    using var statsCommand = new MySqlCommand(statsSql, connection);
    statsCommand.Parameters.AddWithValue("@characterId", characterId);

    CharacterStats stats;

    using (var statsReader = await statsCommand.ExecuteReaderAsync())
    {
        if (!await statsReader.ReadAsync())
            return null;

        stats = new CharacterStats
        {
            CharacterId = characterId,
            Strength = statsReader.GetInt32("strength"),
            Agility = statsReader.GetInt32("agility"),
            Intelligence = statsReader.GetInt32("intelligence"),
            Dexterity = statsReader.GetInt32("dexterity"),
            Charisma = statsReader.GetInt32("charisma"),
            Perception = statsReader.GetInt32("perception")
        };
    }



        // career_stats comes next
        const string careerStatsSql = """
            SELECT total_heists,
                successful_heists,
                failed_heists,
                total_earnings
            FROM career_stats
            WHERE character_id = @characterId;
            """;
            using var careerStatsCommand = new MySqlCommand(careerStatsSql, connection);
            careerStatsCommand.Parameters.AddWithValue("@characterId", characterId);

        CareerStats careerStats;
        using (var careerStatsReader = await careerStatsCommand.ExecuteReaderAsync())
        {
            if (!await careerStatsReader.ReadAsync())
                return null;

            careerStats = new CareerStats
            {
                CharacterId = characterId,
                TotalHeists = careerStatsReader.GetInt32("total_heists"),
                SuccessfulHeists = careerStatsReader.GetInt32("successful_heists"),
                FailedHeists = careerStatsReader.GetInt32("failed_heists"),
                TotalEarnings = careerStatsReader.GetDecimal("total_earnings")
            };
        }
        const string equipmentSql = """
            SELECT equipment_id
            FROM character_equipment
            WHERE character_id = @characterId;
            """;

        using var equipmentCommand = new MySqlCommand(equipmentSql, connection);
        equipmentCommand.Parameters.AddWithValue("@characterId", characterId);

        var equipment = new List<int>();

        using (var equipmentReader = await equipmentCommand.ExecuteReaderAsync())
        {
            while (await equipmentReader.ReadAsync())
            {
                equipment.Add(
                    equipmentReader.GetInt32("equipment_id")
                );
            }
        }

        return new CharacterDto
        {
            CharacterId = id,
            Name = name,
            Level = level,
            Xp = xp,
            Class = characterClass,

            Stats = new CharacterStatsDto
            {
                Strength = stats.Strength,
                Agility = stats.Agility,
                Intelligence = stats.Intelligence,
                Dexterity = stats.Dexterity,
                Charisma = stats.Charisma,
                Perception = stats.Perception
            },

            Equipment = equipment,

            Career = new CareerStatsDto
            {
                TotalHeists = careerStats.TotalHeists,
                SuccessfulHeists = careerStats.SuccessfulHeists,
                FailedHeists = careerStats.FailedHeists,
                TotalEarnings = careerStats.TotalEarnings
            },

            AvailableForHire = availableForHire,
            OwnerId = ownerId
        };

    }
    public async Task<int> CreateAsync(CharacterDto character)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Insert base character information
            const string characterSql = """
            INSERT INTO characters
                (owner_id,
                character_name,
                character_level,
                character_xp,
                character_class,
                available_for_hire)
            VALUES
                (@ownerId,
                @name,
                @level,
                @xp,
                @class,
                @availableForHire);
            """;

            using var characterCommand =
                new MySqlCommand(characterSql, connection, transaction);

            characterCommand.Parameters.AddWithValue(
                "@ownerId", character.OwnerId);

            characterCommand.Parameters.AddWithValue(
                "@name", character.Name);

            characterCommand.Parameters.AddWithValue(
                "@level", character.Level);

            characterCommand.Parameters.AddWithValue(
                "@xp", character.Xp);

            characterCommand.Parameters.AddWithValue(
                "@class", character.Class);

            characterCommand.Parameters.AddWithValue(
                "@availableForHire", character.AvailableForHire);

            await characterCommand.ExecuteNonQueryAsync();
            int characterId = (int)characterCommand.LastInsertedId;


            await transaction.CommitAsync();

            return characterId;
            const string statsSql = """
            INSERT INTO character_stats
                (character_id,
                strength,
                agility,
                intelligence,
                dexterity,
                charisma,
                perception)
            VALUES
                (@characterId,
                @strength,
                @agility,
                @intelligence,
                @dexterity,
                @charisma,
                @perception);
            """;

            using var statsCommand =
                new MySqlCommand(statsSql, connection, transaction);

            statsCommand.Parameters.AddWithValue(
                "@characterId", characterId);

            statsCommand.Parameters.AddWithValue(
                "@strength", character.Stats.Strength);

            statsCommand.Parameters.AddWithValue(
                "@agility", character.Stats.Agility);

            statsCommand.Parameters.AddWithValue(
                "@intelligence", character.Stats.Intelligence);

            statsCommand.Parameters.AddWithValue(
                "@dexterity", character.Stats.Dexterity);

            statsCommand.Parameters.AddWithValue(
                "@charisma", character.Stats.Charisma);

            statsCommand.Parameters.AddWithValue(
                "@perception", character.Stats.Perception);

            await statsCommand.ExecuteNonQueryAsync();

            //Next we add career stats into the character_career table.
            const string careerSql = """
            INSERT INTO career_stats
                (character_id,
                total_heists,
                successful_heists,
                failed_heists,
                total_earnings)
            VALUES
                (@characterId,
                @totalHeists,
                @successfulHeists,
                @failedHeists,
                @totalEarnings);
            """;

            using var careerCommand =
                new MySqlCommand(careerSql, connection, transaction);

            careerCommand.Parameters.AddWithValue(
                "@characterId", characterId);

            careerCommand.Parameters.AddWithValue(
                "@totalHeists", character.Career.TotalHeists);

            careerCommand.Parameters.AddWithValue(
                "@successfulHeists", character.Career.SuccessfulHeists);

            careerCommand.Parameters.AddWithValue(
                "@failedHeists", character.Career.FailedHeists);

            careerCommand.Parameters.AddWithValue(
                "@totalEarnings", character.Career.TotalEarnings);

            await careerCommand.ExecuteNonQueryAsync();

            //Lastly we insert the character's equipment into the character_equipment table.
            const string equipmentSql = """
            INSERT INTO character_equipment
                (character_id, equipment_id)
            VALUES
                (@characterId, @equipmentId);
            """;
            //loop through each equipment ID and insert it into the character_equipment table
            foreach (int equipmentId in character.Equipment)
            {
                using var equipmentCommand =
                    new MySqlCommand(equipmentSql, connection, transaction);

                equipmentCommand.Parameters.AddWithValue(
                    "@characterId", characterId);

                equipmentCommand.Parameters.AddWithValue(
                    "@equipmentId", equipmentId);

                await equipmentCommand.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            return characterId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    public async Task<bool> UpdateAsync(int characterId, CharacterDto character)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            const string characterSql = """
                UPDATE characters
                SET character_name = @name,
                    character_level = @level,
                    character_xp = @xp,
                    character_class = @class,
                    available_for_hire = @availableForHire
                WHERE character_id = @characterId;
                """;

            using var characterCommand =
                new MySqlCommand(characterSql, connection, transaction);

            characterCommand.Parameters.AddWithValue("@name", character.Name);
            characterCommand.Parameters.AddWithValue("@level", character.Level);
            characterCommand.Parameters.AddWithValue("@xp", character.Xp);
            characterCommand.Parameters.AddWithValue("@class", character.Class);
            characterCommand.Parameters.AddWithValue(
                "@availableForHire", character.AvailableForHire);
            characterCommand.Parameters.AddWithValue(
                "@characterId", characterId);

            await characterCommand.ExecuteNonQueryAsync();


            const string statsSql = """
                UPDATE character_stats
                SET strength = @strength,
                    agility = @agility,
                    intelligence = @intelligence,
                    dexterity = @dexterity,
                    charisma = @charisma,
                    perception = @perception
                WHERE character_id = @characterId;
                """;

            using var statsCommand =
                new MySqlCommand(statsSql, connection, transaction);

            statsCommand.Parameters.AddWithValue(
                "@strength", character.Stats.Strength);
            statsCommand.Parameters.AddWithValue(
                "@agility", character.Stats.Agility);
            statsCommand.Parameters.AddWithValue(
                "@intelligence", character.Stats.Intelligence);
            statsCommand.Parameters.AddWithValue(
                "@dexterity", character.Stats.Dexterity);
            statsCommand.Parameters.AddWithValue(
                "@charisma", character.Stats.Charisma);
            statsCommand.Parameters.AddWithValue(
                "@perception", character.Stats.Perception);
            statsCommand.Parameters.AddWithValue(
                "@characterId", characterId);

            await statsCommand.ExecuteNonQueryAsync();


            const string careerSql = """
                UPDATE career_stats
                SET total_heists = @totalHeists,
                    successful_heists = @successfulHeists,
                    failed_heists = @failedHeists,
                    total_earnings = @totalEarnings
                WHERE character_id = @characterId;
                """;

            using var careerCommand =
                new MySqlCommand(careerSql, connection, transaction);

            careerCommand.Parameters.AddWithValue(
                "@totalHeists", character.Career.TotalHeists);
            careerCommand.Parameters.AddWithValue(
                "@successfulHeists", character.Career.SuccessfulHeists);
            careerCommand.Parameters.AddWithValue(
                "@failedHeists", character.Career.FailedHeists);
            careerCommand.Parameters.AddWithValue(
                "@totalEarnings", character.Career.TotalEarnings);
            careerCommand.Parameters.AddWithValue(
                "@characterId", characterId);

            await careerCommand.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
    public async Task<bool> DeleteAsync(int characterId)
    {
        using var connection = _database.CreateConnection();
        await connection.OpenAsync();

        const string sql = """
            DELETE FROM characters
            WHERE character_id = @characterId;
            """;

        using var command = new MySqlCommand(sql, connection);
        command.Parameters.AddWithValue("@characterId", characterId);

        int rowsAffected = await command.ExecuteNonQueryAsync();

        return rowsAffected > 0;
    }

    public async Task<List<CharacterDto>> GetByOwnerIdAsync(int ownerId)
{
    using var connection = _database.CreateConnection();
    await connection.OpenAsync();

    const string sql = """
        SELECT character_id
        FROM characters
        WHERE owner_id = @ownerId;
        """;

    using var command = new MySqlCommand(sql, connection);
    command.Parameters.AddWithValue("@ownerId", ownerId);

    var characterIds = new List<int>();
    //put the character IDs into the list
    using (var reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            characterIds.Add(
                reader.GetInt32("character_id")
            );
        }
    }

    var characters = new List<CharacterDto>();
    //retrieve the full character details for each ID
    foreach (int characterId in characterIds)
    {
        var character = await GetByIdAsync(characterId);

        if (character != null)
        {
            characters.Add(character);
        }
    }

    return characters;
}
}