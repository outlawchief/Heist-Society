namespace HeistApi.Models;

public class CareerStats
{
    public int CharacterId { get; set; }

    public int Heists { get; set; }
    public int SuccessfulHeists { get; set; }
    public int MoneyStolen { get; set; }
}