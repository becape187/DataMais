using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class AddRelatorioSituacaoVersao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConcluidoPorNome",
                table: "Relatorios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConcluidoPorUsuarioId",
                table: "Relatorios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataConclusao",
                table: "Relatorios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Situacao",
                table: "Relatorios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Rascunho");

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Relatorios",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RelatorioVersoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RelatorioId = table.Column<int>(type: "integer", nullable: false),
                    NumeroVersao = table.Column<int>(type: "integer", nullable: false),
                    Acao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UsuarioId = table.Column<int>(type: "integer", nullable: true),
                    UsuarioNome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DataHora = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Resultado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Observacoes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RespostasJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RelatorioVersoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RelatorioVersoes_Relatorios_RelatorioId",
                        column: x => x.RelatorioId,
                        principalTable: "Relatorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RelatorioVersoes_RelatorioId",
                table: "RelatorioVersoes",
                column: "RelatorioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RelatorioVersoes");

            migrationBuilder.DropColumn(
                name: "ConcluidoPorNome",
                table: "Relatorios");

            migrationBuilder.DropColumn(
                name: "ConcluidoPorUsuarioId",
                table: "Relatorios");

            migrationBuilder.DropColumn(
                name: "DataConclusao",
                table: "Relatorios");

            migrationBuilder.DropColumn(
                name: "Situacao",
                table: "Relatorios");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Relatorios");
        }
    }
}
