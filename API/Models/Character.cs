namespace HeistApi.Models;

public class Character
{
    public int CharacterId { get; set; }
    public int OwnerId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Xp { get; set; }
    public string Class { get; set; } = string.Empty;

    public bool AvailableForHire { get; set; }
}