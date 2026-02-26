using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Services;
using DataMais.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Carrega configurações do .env
var configService = new ConfigService();
var appConfig = configService.GetConfig();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configuração do Entity Framework com PostgreSQL
var connectionString = $"Host={appConfig.Database.Host};Port={appConfig.Database.Port};Database={appConfig.Database.Database};Username={appConfig.Database.Username};Password={appConfig.Database.Password}";

builder.Services.AddDbContext<DataMaisDbContext>(options =>
    options.UseNpgsql(connectionString));

// Registra o ConfigService como singleton
builder.Services.AddSingleton<ConfigService>(configService);

// Registra o ModbusService como scoped
builder.Services.AddScoped<DataMais.Services.ModbusService>();

// CORS para permitir requisições do frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "http://localhost:5173", 
                "http://localhost:3000",
                "https://modec.automais.cloud"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthorization();
app.MapControllers();

// Aplica migrations automaticamente (apenas em desenvolvimento)
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<DataMaisDbContext>();
        try
        {
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            // Log error - migrations will be applied manually in production
            Console.WriteLine($"Migration error: {ex.Message}");
        }
    }
}

app.Run();
