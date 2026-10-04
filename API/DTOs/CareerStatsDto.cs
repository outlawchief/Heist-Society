namespace HeistApi.DTOs;

public class CareerStatsDto
{
   public int CharacterId { get; set; }
   public int Heists { get; set; }
   public int SuccessfulHeists { get; set; }
   public int MoneyStolen { get; set; }
}