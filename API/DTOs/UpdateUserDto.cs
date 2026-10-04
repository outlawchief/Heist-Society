namespace HeistApi.DTOs;

public class UpdateUserDto
{
    public string? Username { get; set; }
    public string? Email { get; set; }
    public int Cash { get; set; }
}