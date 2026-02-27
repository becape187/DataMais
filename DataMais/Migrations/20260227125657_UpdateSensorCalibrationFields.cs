using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSensorCalibrationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Adiciona os novos campos de calibração
            migrationBuilder.AddColumn<decimal>(
                name: "InputMin",
                table: "Sensores",
                type: "numeric(10,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OutputMin",
                table: "Sensores",
                type: "numeric(10,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InputMax",
                table: "Sensores",
                type: "numeric(10,6)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OutputMax",
                table: "Sensores",
                type: "numeric(10,6)",
                nullable: true);

            // Remove o campo Scale antigo
            migrationBuilder.DropColumn(
                name: "Scale",
                table: "Sensores");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaura o campo Scale
            migrationBuilder.AddColumn<decimal>(
                name: "Scale",
                table: "Sensores",
                type: "numeric(10,6)",
                nullable: true);

            // Remove os novos campos
            migrationBuilder.DropColumn(
                name: "InputMax",
                table: "Sensores");

            migrationBuilder.DropColumn(
                name: "InputMin",
                table: "Sensores");

            migrationBuilder.DropColumn(
                name: "OutputMax",
                table: "Sensores");

            migrationBuilder.DropColumn(
                name: "OutputMin",
                table: "Sensores");
        }
    }
}
