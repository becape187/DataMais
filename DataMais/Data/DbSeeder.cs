using DataMais.Models;

namespace DataMais.Data;

/// <summary>
/// Seeds idempotentes rodados no startup (Program.cs).
/// </summary>
public static class DbSeeder
{
    private record CampoSeed(string Secao, string Nome, string TipoResposta, bool ReprovaSeSim);

    // Checklist do relatório rev02 (Formulário de Teste Funcional – Cilindro Hidráulico).
    // A ordem aqui é a ordem de exibição dentro de cada seção.
    private static readonly CampoSeed[] CamposRev02 =
    {
        // 3. Inspeção Visual Inicial
        new("Inspeção Visual", "Estado da pintura", "TextoSimples", false),
        new("Inspeção Visual", "Vazamentos visíveis", "SimOuNao", true), // Sim → Reprovado (regra rev02)
        new("Inspeção Visual", "Estado das conexões e flanges", "SimOuNao", false),
        new("Inspeção Visual", "Haste (riscos/corrosão/empenamento)", "SimOuNao", false),
        new("Inspeção Visual", "Identificação e plaquetas legíveis", "SimOuNao", false),
        new("Inspeção Visual", "Observações da Inspeção", "MultiplasLinhas", false),

        // 4. Testes Funcionais (apenas os itens de checklist; medições automáticas ficam no laudo)
        new("Testes Funcionais", "Teste de Movimento (curso completo)", "SimOuNao", false),
        new("Testes Funcionais", "Movimentos suaves, sem trancos", "SimOuNao", false),
        new("Testes Funcionais", "Fim de curso alcançado corretamente", "SimOuNao", false),
        new("Testes Funcionais", "Vazamento externo (conexões/selos)", "SimOuNao", false),

        // 5. Condições Finais
        new("Condições Finais", "Ruídos anormais durante operação", "SimOuNao", false),
        new("Condições Finais", "Observações Finais", "MultiplasLinhas", false),
    };

    /// <summary>
    /// Insere os campos padrão do relatório rev02 que ainda não existem (por Nome).
    /// Idempotente e não ressuscita campos que o admin tenha excluído (checa incluindo soft-deleted).
    /// </summary>
    public static void SeedCamposRelatorioRev02(DataMaisDbContext ctx)
    {
        var existentes = ctx.CamposRelatorio
            .Select(c => c.Nome)
            .ToHashSet();

        var novos = new List<CampoRelatorio>();
        var ordem = 1;
        foreach (var s in CamposRev02)
        {
            if (!existentes.Contains(s.Nome))
            {
                novos.Add(new CampoRelatorio
                {
                    Nome = s.Nome,
                    Secao = s.Secao,
                    TipoResposta = s.TipoResposta,
                    ReprovaSeSim = s.ReprovaSeSim,
                    Ordem = ordem,
                    DataCriacao = DateTime.UtcNow
                });
            }
            ordem++;
        }

        if (novos.Count > 0)
        {
            ctx.CamposRelatorio.AddRange(novos);
            ctx.SaveChanges();
            Console.WriteLine($"✓ {novos.Count} campo(s) do checklist rev02 criados.");
        }
    }
}
