using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class AddCamarasHabilitadasEnsaio : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// defaultValue TRUE nas duas, e não o `false` que o scaffold gera: o ensaio
        /// nasce completo (câmaras A e B) e desmarcar é ato do operador. Com `false`,
        /// TODO ensaio já existente acordaria sem nenhuma câmara habilitada — o aceite
        /// passaria a recusar laudo de ensaio que estava pronto.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CamaraAHabilitada",
                table: "Ensaios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CamaraBHabilitada",
                table: "Ensaios",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CamaraAHabilitada",
                table: "Ensaios");

            migrationBuilder.DropColumn(
                name: "CamaraBHabilitada",
                table: "Ensaios");
        }
    }
}
