using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class perguntas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CamposRelatorio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TipoResposta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataExclusao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CamposRelatorio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RespostasCampoRelatorio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RelatorioId = table.Column<int>(type: "integer", nullable: false),
                    CampoRelatorioId = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RelatorioId1 = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RespostasCampoRelatorio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RespostasCampoRelatorio_CamposRelatorio_CampoRelatorioId",
                        column: x => x.CampoRelatorioId,
                        principalTable: "CamposRelatorio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RespostasCampoRelatorio_Relatorios_RelatorioId",
                        column: x => x.RelatorioId,
                        principalTable: "Relatorios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RespostasCampoRelatorio_Relatorios_RelatorioId1",
                        column: x => x.RelatorioId1,
                        principalTable: "Relatorios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CamposRelatorio_DataExclusao",
                table: "CamposRelatorio",
                column: "DataExclusao");

            migrationBuilder.CreateIndex(
                name: "IX_CamposRelatorio_Ordem",
                table: "CamposRelatorio",
                column: "Ordem");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasCampoRelatorio_CampoRelatorioId",
                table: "RespostasCampoRelatorio",
                column: "CampoRelatorioId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasCampoRelatorio_RelatorioId",
                table: "RespostasCampoRelatorio",
                column: "RelatorioId");

            migrationBuilder.CreateIndex(
                name: "IX_RespostasCampoRelatorio_RelatorioId_CampoRelatorioId",
                table: "RespostasCampoRelatorio",
                columns: new[] { "RelatorioId", "CampoRelatorioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RespostasCampoRelatorio_RelatorioId1",
                table: "RespostasCampoRelatorio",
                column: "RelatorioId1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RespostasCampoRelatorio");

            migrationBuilder.DropTable(
                name: "CamposRelatorio");
        }
    }
}
