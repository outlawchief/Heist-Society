namespace HeistApi.Models;

public class User
{
    public int UserId { get; set; }

    public string Username { get; set; } = string.Empty;
     public string Email { get; set; } = string.Empty;
      public string PasswordHash { get; set; } = string.Empty;
       public int Cash { get; set; } = 0;
}