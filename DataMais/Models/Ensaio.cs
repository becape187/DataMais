using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DataMais.Models;

public class Ensaio
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Numero { get; set; } = string.Empty;

    /// <summary>Ver <see cref="StatusEnsaio"/>.</summary>
    [MaxLength(50)]
    public string Status { get; set; } = StatusEnsaio.EmAndamento;

    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }

    [MaxLength(1000)]
    public string? Observacoes { get; set; }

    // Identificação do documento (relatório rev02) — preenchidos no setup do ensaio.
    /// <summary>Embarcação / unidade testada (ex.: MV29 / Frota).</summary>
    [MaxLength(100)]
    public string? Vessel { get; set; }

    /// <summary>Local do teste (ex.: Macaé).</summary>
    [MaxLength(200)]
    public string? LocalTeste { get; set; }

    /// <summary>Departamento responsável (ex.: ONSHORE PRESERVATION).</summary>
    [MaxLength(100)]
    public string? Departamento { get; set; }

    /// <summary>Ordem de Serviço / Work Order.</summary>
    [MaxLength(100)]
    public string? OrdemServico { get; set; }

    // ── Câmaras que este ensaio testa ───────────────────────────────────────
    // O padrão é o ensaio completo (as duas). O operador pode desmarcar UMA delas
    // para fechar o laudo só com a outra — é o que libera o aceite sem a câmara
    // ausente. As duas desmarcadas não existe: o ensaio precisa de ao menos uma.

    /// <summary>Câmara A entra neste ensaio e é exigida no aceite.</summary>
    public bool CamaraAHabilitada { get; set; } = true;

    /// <summary>Câmara B entra neste ensaio e é exigida no aceite.</summary>
    public bool CamaraBHabilitada { get; set; } = true;

    /// <summary>Câmaras que este ensaio precisa concluir para virar laudo ("A", "B" ou ambas).</summary>
    [NotMapped]
    public string[] CamarasHabilitadas =>
        new[] { (CamaraAHabilitada, "A"), (CamaraBHabilitada, "B") }
            .Where(c => c.Item1)
            .Select(c => c.Item2)
            .ToArray();

    // ── Depreciados: parâmetros migraram para EnsaioEtapa ───────────────────
    // Mantidos apenas porque os ensaios anteriores ao modelo de duas câmaras
    // gravaram aqui; o backfill copiou tudo para a primeira etapa. Código novo
    // deve ler de Etapas, nunca destes campos.

    /// <summary>Obsoleto — ver <see cref="EnsaioEtapa.Camara"/>.</summary>
    [MaxLength(20)]
    public string? CamaraTestada { get; set; }

    /// <summary>Obsoleto — ver <see cref="EnsaioEtapa.PressaoCargaConfigurada"/>.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? PressaoCargaConfigurada { get; set; }

    /// <summary>Obsoleto — ver <see cref="EnsaioEtapa.TempoCargaConfigurado"/>.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal? TempoCargaConfigurado { get; set; }

    // Relacionamentos
    [Required]
    public int ClienteId { get; set; }
    public virtual Cliente Cliente { get; set; } = null!;

    [Required]
    public int CilindroId { get; set; }
    public virtual Cilindro Cilindro { get; set; } = null!;

    public virtual ICollection<Relatorio> Relatorios { get; set; } = new List<Relatorio>();

    /// <summary>Uma corrida por câmara (e por repetição). Ver <see cref="EnsaioEtapa"/>.</summary>
    public virtual ICollection<EnsaioEtapa> Etapas { get; set; } = new List<EnsaioEtapa>();

    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataAtualizacao { get; set; }
}

/// <summary>Estados do <see cref="Ensaio"/> (o cabeçalho do teste completo).</summary>
public static class StatusEnsaio
{
    /// <summary>Aberto; ainda falta rodar ou repetir alguma câmara.</summary>
    public const string EmAndamento = "EmAndamento";

    /// <summary>Todas as câmaras habilitadas concluídas; esperando o operador aceitar.</summary>
    public const string AguardandoAceite = "AguardandoAceite";

    /// <summary>Aceito pelo operador — relatório gerado.</summary>
    public const string Aceito = "Aceito";

    /// <summary>Descartado; não vira laudo.</summary>
    public const string Cancelado = "Cancelado";
}
