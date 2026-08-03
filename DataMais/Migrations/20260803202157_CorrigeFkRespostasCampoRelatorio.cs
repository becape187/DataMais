using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeFkRespostasCampoRelatorio : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RespostasCampoRelatorio_Relatorios_RelatorioId1",
                table: "RespostasCampoRelatorio");

            migrationBuilder.DropIndex(
                name: "IX_RespostasCampoRelatorio_RelatorioId1",
                table: "RespostasCampoRelatorio");

            migrationBuilder.DropColumn(
                name: "RelatorioId1",
                table: "RespostasCampoRelatorio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RelatorioId1",
                table: "RespostasCampoRelatorio",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RespostasCampoRelatorio_RelatorioId1",
                table: "RespostasCampoRelatorio",
                column: "RelatorioId1");

            migrationBuilder.AddForeignKey(
                name: "FK_RespostasCampoRelatorio_Relatorios_RelatorioId1",
                table: "RespostasCampoRelatorio",
                column: "RelatorioId1",
                principalTable: "Relatorios",
                principalColumn: "Id");
        }
    }
}
