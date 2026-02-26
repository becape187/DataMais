using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Cnpj = table.Column<string>(type: "character varying(18)", maxLength: 18, nullable: true),
                    Contato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ModbusConfigs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    SlaveId = table.Column<byte>(type: "smallint", nullable: false),
                    FuncaoModbus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EnderecoRegistro = table.Column<int>(type: "integer", nullable: false),
                    QuantidadeRegistros = table.Column<int>(type: "integer", nullable: false),
                    TipoDado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ByteOrder = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    FatorConversao = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    Offset = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    Unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OrdemLeitura = table.Column<int>(type: "integer", nullable: false),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModbusConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SenhaHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UltimoLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cilindros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CodigoCliente = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CodigoInterno = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Modelo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Fabricante = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DataFabricacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DiametroInterno = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    ComprimentoHaste = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    DiametroHaste = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    MaximaPressaoSuportadaA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    MaximaPressaoSuportadaB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    MaximaPressaoSegurancaA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    MaximaPressaoSegurancaB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    PreCargaA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    CargaNominalA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    TempoRampaSubidaA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    TempoDuracaoCargaA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    TempoRampaDescidaA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    PercentualVariacaoAlarmeA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    HistereseAlarmeA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    PercentualVariacaoDesligaProcessoA = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    PreCargaB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    CargaNominalB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    TempoRampaSubidaB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    TempoDuracaoCargaB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    TempoRampaDescidaB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    PercentualVariacaoAlarmeB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    HistereseAlarmeB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    PercentualVariacaoDesligaProcessoB = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cilindros", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cilindros_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Sensores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Unidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ModbusConfigId = table.Column<int>(type: "integer", nullable: true),
                    Ativo = table.Column<bool>(type: "boolean", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sensores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sensores_ModbusConfigs_ModbusConfigId",
                        column: x => x.ModbusConfigId,
                        principalTable: "ModbusConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Ensaios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DataFim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    CilindroId = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ensaios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ensaios_Cilindros_CilindroId",
                        column: x => x.CilindroId,
                        principalTable: "Cilindros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ensaios_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Relatorios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Numero = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CaminhoArquivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClienteId = table.Column<int>(type: "integer", nullable: false),
                    CilindroId = table.Column<int>(type: "integer", nullable: false),
                    EnsaioId = table.Column<int>(type: "integer", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Relatorios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Relatorios_Cilindros_CilindroId",
                        column: x => x.CilindroId,
                        principalTable: "Cilindros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Relatorios_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Relatorios_Ensaios_EnsaioId",
                        column: x => x.EnsaioId,
                        principalTable: "Ensaios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cilindros_ClienteId_CodigoCliente",
                table: "Cilindros",
                columns: new[] { "ClienteId", "CodigoCliente" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cilindros_CodigoInterno",
                table: "Cilindros",
                column: "CodigoInterno",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Cnpj",
                table: "Clientes",
                column: "Cnpj",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_Email",
                table: "Clientes",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_CilindroId",
                table: "Ensaios",
                column: "CilindroId");

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_ClienteId",
                table: "Ensaios",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_Numero",
                table: "Ensaios",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ensaios_Status",
                table: "Ensaios",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ModbusConfigs_IpAddress_OrdemLeitura",
                table: "ModbusConfigs",
                columns: new[] { "IpAddress", "OrdemLeitura" });

            migrationBuilder.CreateIndex(
                name: "IX_ModbusConfigs_Nome",
                table: "ModbusConfigs",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Relatorios_CilindroId",
                table: "Relatorios",
                column: "CilindroId");

            migrationBuilder.CreateIndex(
                name: "IX_Relatorios_ClienteId",
                table: "Relatorios",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Relatorios_Data",
                table: "Relatorios",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_Relatorios_EnsaioId",
                table: "Relatorios",
                column: "EnsaioId");

            migrationBuilder.CreateIndex(
                name: "IX_Relatorios_Numero",
                table: "Relatorios",
                column: "Numero",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sensores_ModbusConfigId",
                table: "Sensores",
                column: "ModbusConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Sensores_Nome",
                table: "Sensores",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Relatorios");

            migrationBuilder.DropTable(
                name: "Sensores");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Ensaios");

            migrationBuilder.DropTable(
                name: "ModbusConfigs");

            migrationBuilder.DropTable(
                name: "Cilindros");

            migrationBuilder.DropTable(
                name: "Clientes");
        }
    }
}
