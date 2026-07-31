namespace DataMais.Services;

/// <summary>Última leitura conhecida de um sinal do CLP.</summary>
/// <param name="Ligado">Estado do sinal; null se a leitura falhou ou nunca aconteceu.</param>
/// <param name="LidoEm">Instante (UTC) da leitura; null se nunca leu.</param>
/// <param name="Erro">Motivo da falha, quando <paramref name="Ligado"/> é null.</param>
public sealed record LeituraSinalClp(bool? Ligado, DateTime? LidoEm, string? Erro)
{
    public static readonly LeituraSinalClp Nenhuma = new(null, null, null);
}

/// <summary>
/// Cache em memória do estado dos sinais de ciclo do CLP (REGISTRO_RODANDO e
/// INICIA_CONTAGEM), alimentado pelo <see cref="RegistroConclusaoMonitor"/> a cada
/// ciclo. A tela do ensaio consome DAQUI — mostrar os sinais não custa nenhuma
/// transação Modbus extra, o que importa porque foi excesso de tráfego concorrente
/// no socket que corrompeu as leituras originalmente.
/// </summary>
public sealed class EstadoSinaisClp
{
    // Troca de referência é atômica em .NET; volatile garante visibilidade entre
    // o loop do monitor e as threads dos controllers.
    private volatile LeituraSinalClp _registroRodando = LeituraSinalClp.Nenhuma;
    private volatile LeituraSinalClp _iniciaContagem = LeituraSinalClp.Nenhuma;

    public LeituraSinalClp RegistroRodando => _registroRodando;
    public LeituraSinalClp IniciaContagem => _iniciaContagem;

    public void PublicarRegistroRodando(bool? ligado, string? erro = null) =>
        _registroRodando = new LeituraSinalClp(ligado, DateTime.UtcNow, erro);

    public void PublicarIniciaContagem(bool? ligado, string? erro = null) =>
        _iniciaContagem = new LeituraSinalClp(ligado, DateTime.UtcNow, erro);
}
