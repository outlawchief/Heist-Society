using HeistApi.Repositories;
using HeistApi.Database;
using HeistApi.Endpoints;
using HeistApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Database connection
builder.Services.AddSingleton<DatabaseConnection>();

// Repositories
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<CharacterRepository>();
builder.Services.AddScoped<EquipmentRepository>();
builder.Services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();

// OpenAPI
//builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Website", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// JWT Authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtKey = builder.Configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT key is missing.");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseAuthentication();
    app.UseAuthorization();


app.UseCors("Website");

// Endpoints
app.MapUserEndpoints();
app.MapAuthEndpoints();
//app.MapCharacterEndpoints();
//app.MapEquipmentEndpoints();



app.UseHttpsRedirection();

app.Run();