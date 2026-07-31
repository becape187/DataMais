using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class AddJanelaContagemEnsaioEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataFimContagem",
                table: "EnsaioEtapas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataInicioContagem",
                table: "EnsaioEtapas",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataFimContagem",
                table: "EnsaioEtapas");

            migrationBuilder.DropColumn(
                name: "DataInicioContagem",
                table: "EnsaioEtapas");
        }
    }
}
