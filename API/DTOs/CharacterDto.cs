namespace HeistApi.DTOs;

public class CharacterDto
{
    public int CharacterId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Xp { get; set; }
    public string Class { get; set; } = string.Empty;

    public CharacterStatsDto Stats { get; set; } = new();

    public List<int> Equipment { get; set; } = new();

    public CareerStatsDto Career { get; set; } = new();

    public bool AvailableForHire { get; set; }

    public int OwnerId { get; set; }
}