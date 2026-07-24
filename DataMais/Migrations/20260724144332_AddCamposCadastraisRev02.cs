using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class AddCamposCadastraisRev02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Departamento",
                table: "Ensaios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocalTeste",
                table: "Ensaios",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrdemServico",
                table: "Ensaios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vessel",
                table: "Ensaios",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FluidoUtilizado",
                table: "Cilindros",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroSerie",
                table: "Cilindros",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartNumber",
                table: "Cilindros",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Departamento",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "LocalTeste",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "OrdemServico",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "Vessel",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "FluidoUtilizado",
                table: "Cilindros");

            migrationBuilder.DropColumn(
                name: "NumeroSerie",
                table: "Cilindros");

            migrationBuilder.DropColumn(
                name: "PartNumber",
                table: "Cilindros");
        }
    }
}
