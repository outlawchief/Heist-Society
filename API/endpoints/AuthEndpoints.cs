
    using HeistApi.DTOs;
using HeistApi.Models;
using HeistApi.Repositories;
using Microsoft.AspNetCore.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HeistApi.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/login", async (
            LoginDto dto,
            UserRepository repository,
            IPasswordHasher<User> passwordHasher,
            IConfiguration configuration) =>
        {
            var user = await repository.GetByUsernameAsync(dto.Username);

            if (user is null)
                
                return Results.Unauthorized();

            var result = passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.Password
            );

            if (result == PasswordVerificationResult.Failed)
            {
                 return Results.Unauthorized();
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    configuration["Jwt:Key"]!
                )
            );

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256
            );

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: credentials
            );

            var tokenString = new JwtSecurityTokenHandler()
                .WriteToken(token);

            return Results.Ok(new
            {
                Token = tokenString,
            
                UserId = user.UserId,
                Username = user.Username
            });
        });

        group.MapPost("/register", async (
        RegisterDto dto,
        UserRepository repository,
        IPasswordHasher<User> passwordHasher) =>
    {
        var existingUser = await repository.GetByUsernameAsync(dto.Username);

        if (existingUser is not null)
            return Results.Conflict(new
            {
                Message = "Username already exists"
            });

        var user = new User
        {
            Username = dto.Username,
            Email = dto.Email
        };

        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        await repository.CreateAsync(user);

        return Results.Created(
            "/api/auth/login",
            new { Message = "User registered successfully" }
        );
    });
    }

}





