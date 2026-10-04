using HeistApi.DTOs;
using HeistApi.Repositories;
using HeistApi.Models;

using Microsoft.AspNetCore.Identity;
namespace HeistApi.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users");

        // GET /api/users/{id}
        group.MapGet("/{id:int}", async (
            int id,
            UserRepository repository) =>
        {
            var user = await repository.GetByIdAsync(id);

            if (user == null)
                return Results.NotFound();

            return Results.Ok(user);
        });

        // POST /api/users
        group.MapPost("/", async (
            CreateUserDto dto,
            IPasswordHasher<User> passwordHasher,
            UserRepository repository) =>
        {
            
            var user = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                                
                
            };
            string hashedPassword = passwordHasher.HashPassword(user, dto.PasswordHash);
            user.PasswordHash = hashedPassword;
            var userId = await repository.CreateAsync(user);

            return Results.Created(
                $"/api/users/{userId}",
                new { UserId = userId });
        });

        // PUT /api/users/{id}
        group.MapPut("/{id:int}", async (
    int id,
    UpdateUserDto dto,
    UserRepository repository) =>
{
    var user = await repository.GetByIdAsync(id);

    if (user is null)
        return Results.NotFound();

    if (!string.IsNullOrWhiteSpace(dto.Username))
    {
        user.Username = dto.Username;
    }
    if (!string.IsNullOrWhiteSpace(dto.Email))
    {
        user.Email = dto.Email;
    }
    
    user.Cash = dto.Cash;

    var result = await repository.UpdateAsync(id, user);

    if (!result)
        return Results.NotFound();

    return Results.NoContent();
});

        group.MapPut("/password/{id:int}", async (
            int id,
            string newPassword,
            UserRepository repository,
            IPasswordHasher<User> passwordHasher) =>
        {
            var user = await repository.GetByIdAsync(id);

            if (user is null)
                return Results.NotFound();

            var newPasswordHash = passwordHasher.HashPassword(
                user,
                newPassword
            );

            var result = await repository.UpdatePasswordAsync(
                id,
                newPasswordHash
            );

            if (!result)
                return Results.NotFound();

            return Results.NoContent();
        });

       
    }
}