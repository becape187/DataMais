using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class AddEnsaioEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EnsaioEtapas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnsaioId = table.Column<int>(type: "integer", nullable: false),
                    Camara = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    Tentativa = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataInicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataFim = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PressaoCargaConfigurada = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    TempoCargaConfigurado = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnsaioEtapas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnsaioEtapas_Ensaios_EnsaioId",
                        column: x => x.EnsaioId,
                        principalTable: "Ensaios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnsaioEtapas_EnsaioId_Camara_Tentativa",
                table: "EnsaioEtapas",
                columns: new[] { "EnsaioId", "Camara", "Tentativa" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EnsaioEtapas_Status",
                table: "EnsaioEtapas",
                column: "Status");

            // Backfill: cada ensaio antigo (câmara única) vira exatamente uma etapa,
            // copiando câmara, janela de tempo e parâmetros. Sem isso, os relatórios
            // já emitidos ficariam sem série para recalcular o veredito.
            migrationBuilder.Sql("""
                INSERT INTO "EnsaioEtapas" (
                    "EnsaioId", "Camara", "Tentativa", "Status", "DataInicio", "DataFim",
                    "PressaoCargaConfigurada", "TempoCargaConfigurado", "DataCriacao", "DataAtualizacao")
                SELECT
                    e."Id",
                    CASE WHEN UPPER(TRIM(COALESCE(e."CamaraTestada", 'A'))) = 'B' THEN 'B' ELSE 'A' END,
                    1,
                    CASE e."Status"
                        WHEN 'Concluido'  THEN 'Concluida'
                        WHEN 'EmExecucao' THEN 'EmExecucao'
                        ELSE 'Descartada'
                    END,
                    COALESCE(e."DataInicio", e."DataCriacao"),
                    e."DataFim",
                    COALESCE(e."PressaoCargaConfigurada", 0),
                    COALESCE(e."TempoCargaConfigurado", 0),
                    e."DataCriacao",
                    e."DataAtualizacao"
                FROM "Ensaios" e;
                """);

            // Status do ensaio migra para o novo ciclo de vida. 'Concluido' vira 'Aceito'
            // porque no fluxo antigo concluir já gerava o relatório; 'Pendente' nunca chegou
            // a rodar, então é encerrado como cancelado em vez de virar pendência eterna.
            migrationBuilder.Sql("""
                UPDATE "Ensaios" SET "Status" = CASE "Status"
                    WHEN 'Concluido'  THEN 'Aceito'
                    WHEN 'EmExecucao' THEN 'EmAndamento'
                    WHEN 'Pendente'   THEN 'Cancelado'
                    ELSE "Status"
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Volta os status ao vocabulário antigo antes de perder as etapas.
            migrationBuilder.Sql("""
                UPDATE "Ensaios" SET "Status" = CASE "Status"
                    WHEN 'Aceito'           THEN 'Concluido'
                    WHEN 'AguardandoAceite' THEN 'Concluido'
                    WHEN 'EmAndamento'      THEN 'EmExecucao'
                    ELSE "Status"
                END;
                """);

            migrationBuilder.DropTable(
                name: "EnsaioEtapas");
        }
    }
}
