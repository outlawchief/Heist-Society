using HeistApi.Repositories;
using HeistApi.Database;

var builder = WebApplication.CreateBuilder(args);

// Database connection
builder.Services.AddSingleton<DatabaseConnection>();

// Repositories
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<CharacterRepository>();
builder.Services.AddScoped<EquipmentRepository>();

// OpenAPI
//builder.Services.AddOpenApi();

var app = builder.Build();

// OpenAPI available during development
/*if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}*/

app.UseHttpsRedirection();

app.Run();