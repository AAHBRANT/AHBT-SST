using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMetodoAssinaturaAprPt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MetodoAssinatura",
                table: "PermissoesTrabalho",
                type: "int",
                nullable: false,
                defaultValue: 5); // MetodoAutenticacaoAssinatura.SessaoLogada — o que de fato aconteceu até aqui

            migrationBuilder.AddColumn<int>(
                name: "MetodoAutenticacao",
                table: "AprAssinaturas",
                type: "int",
                nullable: false,
                defaultValue: 5); // idem: ciência sempre foi clique do usuário logado
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MetodoAssinatura",
                table: "PermissoesTrabalho");

            migrationBuilder.DropColumn(
                name: "MetodoAutenticacao",
                table: "AprAssinaturas");
        }
    }
}
