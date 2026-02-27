using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniqueIndexNomeModbusConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModbusConfigs_Nome",
                table: "ModbusConfigs");

            migrationBuilder.CreateIndex(
                name: "IX_ModbusConfigs_Nome",
                table: "ModbusConfigs",
                column: "Nome");

            migrationBuilder.CreateIndex(
                name: "IX_ModbusConfigs_Nome_FuncaoModbus",
                table: "ModbusConfigs",
                columns: new[] { "Nome", "FuncaoModbus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ModbusConfigs_Nome",
                table: "ModbusConfigs");

            migrationBuilder.DropIndex(
                name: "IX_ModbusConfigs_Nome_FuncaoModbus",
                table: "ModbusConfigs");

            migrationBuilder.CreateIndex(
                name: "IX_ModbusConfigs_Nome",
                table: "ModbusConfigs",
                column: "Nome",
                unique: true);
        }
    }
}
