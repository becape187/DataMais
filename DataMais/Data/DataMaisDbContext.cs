using Microsoft.EntityFrameworkCore;
using DataMais.Models;

namespace DataMais.Data;

public class DataMaisDbContext : DbContext
{
    public DataMaisDbContext(DbContextOptions<DataMaisDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<Cilindro> Cilindros { get; set; }
    public DbSet<ModbusConfig> ModbusConfigs { get; set; }
    public DbSet<Sensor> Sensores { get; set; }
    public DbSet<Ensaio> Ensaios { get; set; }
    public DbSet<Relatorio> Relatorios { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<CampoRelatorio> CamposRelatorio { get; set; }
    public DbSet<RespostaCampoRelatorio> RespostasCampoRelatorio { get; set; }
    public DbSet<RelatorioVersao> RelatorioVersoes { get; set; }
    public DbSet<ContadorRelatorio> ContadoresRelatorio { get; set; }
    public DbSet<EnsaioEtapa> EnsaioEtapas { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração de Cliente
        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasIndex(e => e.Cnpj).IsUnique();
            entity.HasIndex(e => e.Email);
        });

        // Configuração de Cilindro
        modelBuilder.Entity<Cilindro>(entity =>
        {
            entity.HasIndex(e => new { e.ClienteId, e.CodigoCliente }).IsUnique();
            entity.HasIndex(e => e.CodigoInterno).IsUnique();
            
            entity.HasOne(c => c.Cliente)
                .WithMany(cl => cl.Cilindros)
                .HasForeignKey(c => c.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuração de ModbusConfig
        modelBuilder.Entity<ModbusConfig>(entity =>
        {
            // Índice não único para permitir múltiplos registros com mesmo nome (mas funções diferentes)
            entity.HasIndex(e => e.Nome);
            entity.HasIndex(e => new { e.IpAddress, e.OrdemLeitura });
            // Índice composto ÚNICO: permite mesmo nome com funções diferentes, mas não duplicatas
            entity.HasIndex(e => new { e.Nome, e.FuncaoModbus }).IsUnique();
        });

        // Configuração de Sensor
        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.HasIndex(e => e.Nome).IsUnique();
            
            entity.HasOne(s => s.ModbusConfig)
                .WithMany()
                .HasForeignKey(s => s.ModbusConfigId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuração de Ensaio
        modelBuilder.Entity<Ensaio>(entity =>
        {
            entity.HasIndex(e => e.Numero).IsUnique();
            entity.HasIndex(e => e.Status);
            
            entity.HasOne(e => e.Cliente)
                .WithMany(cl => cl.Ensaios)
                .HasForeignKey(e => e.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Cilindro)
                .WithMany(c => c.Ensaios)
                .HasForeignKey(e => e.CilindroId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configuração de EnsaioEtapa
        modelBuilder.Entity<EnsaioEtapa>(entity =>
        {
            // Uma tentativa por câmara: a repetição entra como Tentativa seguinte
            entity.HasIndex(e => new { e.EnsaioId, e.Camara, e.Tentativa }).IsUnique();
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Ensaio)
                .WithMany(en => en.Etapas)
                .HasForeignKey(e => e.EnsaioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuração de Relatorio
        modelBuilder.Entity<Relatorio>(entity =>
        {
            entity.HasIndex(e => e.Numero).IsUnique();
            entity.HasIndex(e => e.Data);
            entity.Property(e => e.Situacao).HasDefaultValue("Rascunho");
            
            entity.HasOne(r => r.Cliente)
                .WithMany()
                .HasForeignKey(r => r.ClienteId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(r => r.Cilindro)
                .WithMany(c => c.Relatorios)
                .HasForeignKey(r => r.CilindroId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(r => r.Ensaio)
                .WithMany(e => e.Relatorios)
                .HasForeignKey(r => r.EnsaioId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configuração de Usuario
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configuração de CampoRelatorio
        modelBuilder.Entity<CampoRelatorio>(entity =>
        {
            entity.HasIndex(e => e.Ordem);
            entity.HasIndex(e => e.DataExclusao);
        });

        // Configuração de RelatorioVersao
        modelBuilder.Entity<RelatorioVersao>(entity =>
        {
            entity.HasIndex(e => e.RelatorioId);

            entity.HasOne(v => v.Relatorio)
                .WithMany(r => r.Versoes)
                .HasForeignKey(v => v.RelatorioId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configuração de RespostaCampoRelatorio
        modelBuilder.Entity<RespostaCampoRelatorio>(entity =>
        {
            entity.HasIndex(e => e.RelatorioId);
            entity.HasIndex(e => e.CampoRelatorioId);
            entity.HasIndex(e => new { e.RelatorioId, e.CampoRelatorioId }).IsUnique();
            
            // WithMany(rel => rel.RespostasCampos) — apontar a coleção é OBRIGATÓRIO aqui.
            // Com WithMany() vazio o EF não liga esta FK à navegação Relatorio.RespostasCampos:
            // trata as duas pontas como relacionamentos DIFERENTES e cria uma FK sombra
            // (RelatorioId1). O controller gravava em RelatorioId e todo Include(RespostasCampos)
            // lia por RelatorioId1 — sempre NULL. Resultado: checklist gravava e nunca voltava.
            entity.HasOne(r => r.Relatorio)
                .WithMany(rel => rel.RespostasCampos)
                .HasForeignKey(r => r.RelatorioId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(r => r.CampoRelatorio)
                .WithMany(c => c.Respostas)
                .HasForeignKey(r => r.CampoRelatorioId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
