using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PainelComercial.Api.Migrations
{
    /// <inheritdoc />
    public partial class ChaveIdempotencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ChaveIdempotencia",
                table: "Movimentacoes",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Movimentacoes_ChaveIdempotencia",
                table: "Movimentacoes",
                column: "ChaveIdempotencia",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Movimentacoes_ChaveIdempotencia",
                table: "Movimentacoes");

            migrationBuilder.DropColumn(
                name: "ChaveIdempotencia",
                table: "Movimentacoes");
        }
    }
}
