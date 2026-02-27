using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class colunas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CamaraTestada",
                table: "Ensaios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PressaoCargaConfigurada",
                table: "Ensaios",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TempoCargaConfigurado",
                table: "Ensaios",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CamaraTestada",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "PressaoCargaConfigurada",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "TempoCargaConfigurado",
                table: "Ensaios");
        }
    }
}
