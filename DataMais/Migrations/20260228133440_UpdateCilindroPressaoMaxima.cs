using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataMais.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCilindroPressaoMaxima : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Adicionar novas colunas como nullable temporariamente
            migrationBuilder.AddColumn<decimal>(
                name: "MaximaPressaoA",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximaPressaoB",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);

            // 2. Migrar dados existentes: usar MaximaPressaoSuportadaA/B como base
            // Se não existir, usar um valor padrão de 350 (conforme exemplo do usuário)
            migrationBuilder.Sql(@"
                UPDATE ""Cilindros""
                SET ""MaximaPressaoA"" = COALESCE(""MaximaPressaoSuportadaA"", 350),
                    ""MaximaPressaoB"" = COALESCE(""MaximaPressaoSuportadaB"", 350)
                WHERE ""MaximaPressaoA"" IS NULL OR ""MaximaPressaoB"" IS NULL;
            ");

            // 3. Tornar as novas colunas NOT NULL
            migrationBuilder.AlterColumn<decimal>(
                name: "MaximaPressaoA",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 350m);

            migrationBuilder.AlterColumn<decimal>(
                name: "MaximaPressaoB",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 350m);

            // 4. Remover colunas antigas
            migrationBuilder.DropColumn(
                name: "MaximaPressaoSegurancaA",
                table: "Cilindros");

            migrationBuilder.DropColumn(
                name: "MaximaPressaoSegurancaB",
                table: "Cilindros");

            migrationBuilder.DropColumn(
                name: "MaximaPressaoSuportadaA",
                table: "Cilindros");

            migrationBuilder.DropColumn(
                name: "MaximaPressaoSuportadaB",
                table: "Cilindros");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Adicionar colunas antigas de volta
            migrationBuilder.AddColumn<decimal>(
                name: "MaximaPressaoSuportadaA",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximaPressaoSuportadaB",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximaPressaoSegurancaA",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaximaPressaoSegurancaB",
                table: "Cilindros",
                type: "numeric(10,2)",
                nullable: true);

            // Migrar dados de volta (usar MaximaPressaoA/B para SuportadaA/B)
            migrationBuilder.Sql(@"
                UPDATE ""Cilindros""
                SET ""MaximaPressaoSuportadaA"" = ""MaximaPressaoA"",
                    ""MaximaPressaoSuportadaB"" = ""MaximaPressaoB"",
                    ""MaximaPressaoSegurancaA"" = ""MaximaPressaoA"",
                    ""MaximaPressaoSegurancaB"" = ""MaximaPressaoB"";
            ");

            // Remover novas colunas
            migrationBuilder.DropColumn(
                name: "MaximaPressaoA",
                table: "Cilindros");

            migrationBuilder.DropColumn(
                name: "MaximaPressaoB",
                table: "Cilindros");
        }
    }
}
