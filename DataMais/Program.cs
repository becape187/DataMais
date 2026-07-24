using Microsoft.EntityFrameworkCore;
using DataMais.Data;
using DataMais.Models;
using DataMais.Services;
using DataMais.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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

// Registra o ModbusService como singleton para manter uma única conexão Modbus
builder.Services.AddSingleton<DataMais.Services.ModbusService>();

// Monitora REGISTRO_RODANDO em background: quando o CLP conclui o ensaio,
// finaliza e gera o relatório automaticamente (mesmo sem ninguém na tela).
builder.Services.AddHostedService<DataMais.Services.RegistroConclusaoMonitor>();

// ── Autenticação JWT ────────────────────────────────────────────────────────
// Segredo vem do .env (JWT_SECRET), já carregado pelo ConfigService acima.
// Em produção DEVE ser definido no EnvironmentFile do systemd.
var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET");
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    // Fallback apenas para desenvolvimento local. Nunca usar em produção.
    jwtSecret = "datamais_dev_secret_change_me_please_0123456789";
    Console.WriteLine("⚠️ JWT_SECRET ausente/curto — usando segredo de desenvolvimento. Defina JWT_SECRET em produção!");
}

builder.Services.AddSingleton(new TokenService(jwtSecret));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

// Por padrão, todo endpoint exige usuário autenticado. Endpoints públicos
// (ex.: /api/auth/login) usam [AllowAnonymous].
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Aplica migrations automaticamente em qualquer ambiente.
// O deploy reinicia o serviço, então novas migrations são aplicadas no startup
// (em produção também), sem necessidade de rodar `dotnet ef database update` na VM.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<DataMaisDbContext>();
    try
    {
        dbContext.Database.Migrate();

        // Normaliza role legada "Usuario" → "Operador" (padronização de perfis).
        var rolesLegadas = dbContext.Usuarios.Where(u => u.Role == "Usuario").ToList();
        if (rolesLegadas.Count > 0)
        {
            foreach (var u in rolesLegadas) u.Role = "Operador";
            dbContext.SaveChanges();
            Console.WriteLine($"✓ {rolesLegadas.Count} usuário(s) com role 'Usuario' migrados para 'Operador'.");
        }

        // Seed do admin inicial (admin/admin) quando não existe NENHUM usuário Admin.
        // (Antes checava banco vazio; mudou para self-heal quando o banco já tem usuários
        //  mas nenhum com perfil Admin — ex.: instalação que existia antes do login.)
        if (!dbContext.Usuarios.Any(u => u.Role == "Admin"))
        {
            dbContext.Usuarios.Add(new Usuario
            {
                Nome = "Administrador",
                Email = "admin",
                SenhaHash = BCrypt.Net.BCrypt.HashPassword("admin"),
                Role = "Admin",
                Ativo = true,
                DataCriacao = DateTime.UtcNow
            });
            dbContext.SaveChanges();
            Console.WriteLine("✓ Usuário admin inicial criado (login: admin / senha: admin). TROQUE A SENHA.");
        }

        // Seed idempotente do checklist do relatório rev02.
        DataMais.Data.DbSeeder.SeedCamposRelatorioRev02(dbContext);
    }
    catch (Exception ex)
    {
        // Não derruba o serviço se a migration falhar (ex.: banco indisponível no boot).
        Console.WriteLine($"Migration error: {ex.Message}");
    }
}

app.Run();
