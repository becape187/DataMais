using Microsoft.AspNetCore.Mvc;
using DataMais.Services;
using DataMais.Configuration;

namespace DataMais.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigController : ControllerBase
{
    private readonly ConfigService _configService;
    private readonly ILogger<ConfigController> _logger;

    public ConfigController(ConfigService configService, ILogger<ConfigController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult GetConfig()
    {
        try
        {
            var config = _configService.GetConfig();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter configurações");
            return StatusCode(500, new { message = "Erro ao obter configurações" });
        }
    }

    [HttpPost]
    public IActionResult SaveConfig([FromBody] AppConfig config)
    {
        try
        {
            _configService.SaveConfig(config);
            _configService.ReloadConfig();
            return Ok(new { message = "Configurações salvas com sucesso" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar configurações");
            return StatusCode(500, new { message = "Erro ao salvar configurações" });
        }
    }

    [HttpGet("env-path")]
    public IActionResult GetEnvPath()
    {
        try
        {
            var path = _configService.GetEnvFilePath();
            return Ok(new { path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter caminho do .env");
            return StatusCode(500, new { message = "Erro ao obter caminho do .env" });
        }
    }
}
