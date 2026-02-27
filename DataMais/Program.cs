using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Services;
using DataMais.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configurar níveis de log: reduzir verbosidade de logs cíclicos
// Entity Framework: apenas Warning e Error (não loga queries SQL)
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
// ModbusService: apenas Warning e Error (evita logs de conexões cíclicas)
builder.Logging.AddFilter("DataMais.Services.ModbusService", LogLevel.Warning);

// Configurar Kestrel para escutar em 0.0.0.0:5000 em produção
// Isso permite que o nginx faça proxy reverso corretamente
if (builder.Environment.IsProduction())
{
    builder.WebHost.UseUrls("http://0.0.0.0:5000");
}

// Carrega configurações do .env
var configService = new ConfigService();
var appConfig = configService.GetConfig();

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Configurar para aceitar camelCase do frontend
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Resolver conflito de nomes: usar namespace completo para tipos com mesmo nome
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
});

// Desabilitar HTTPS redirection em produção (nginx faz o proxy SSL)
if (builder.Environment.IsProduction())
{
    builder.Services.Configure<Microsoft.AspNetCore.HttpsPolicy.HttpsRedirectionOptions>(options =>
    {
        options.RedirectStatusCode = Microsoft.AspNetCore.Http.StatusCodes.Status307TemporaryRedirect;
        options.HttpsPort = null; // Desabilita redirecionamento
    });
}

// Configuração do Entity Framework com PostgreSQL
var connectionString = $"Host={appConfig.Database.Host};Port={appConfig.Database.Port};Database={appConfig.Database.Database};Username={appConfig.Database.Username};Password={appConfig.Database.Password}";

// Validação da connection string
if (string.IsNullOrWhiteSpace(appConfig.Database.Password))
{
    Console.WriteLine("❌ ERRO: Senha do PostgreSQL não está configurada!");
    Console.WriteLine($"   Arquivo .env esperado em: {configService.GetEnvFilePath()}");
    Console.WriteLine($"   Host: {appConfig.Database.Host}");
    Console.WriteLine($"   Database: {appConfig.Database.Database}");
    Console.WriteLine($"   Username: {appConfig.Database.Username}");
    Console.WriteLine("   Password: (VAZIO)");
    throw new InvalidOperationException("A senha do PostgreSQL não está configurada. Verifique o arquivo .env e a variável POSTGRES_PASSWORD.");
}

builder.Services.AddDbContext<DataMaisDbContext>(options =>
{
    options.UseNpgsql(connectionString);
    // Desabilita logs de queries SQL (apenas erros serão logados)
    options.LogTo(_ => { }, LogLevel.Warning);
});

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
// Swagger disponível em desenvolvimento e produção
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DataMais API v1");
    c.RoutePrefix = "swagger"; // Acesse em /swagger
});

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
// Em produção, o nginx faz o proxy HTTPS, então não precisa de redirecionamento HTTP->HTTPS

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
