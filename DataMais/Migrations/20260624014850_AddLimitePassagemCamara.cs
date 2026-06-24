using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class AddLimitePassagemCamara : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LimitePassagemCamaraA",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LimitePassagemCamaraB",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LimitePassagemCamaraA",
                table: "Cilindros");

            migrationBuilder.DropColumn(
                name: "LimitePassagemCamaraB",
                table: "Cilindros");
        }
    }
}
