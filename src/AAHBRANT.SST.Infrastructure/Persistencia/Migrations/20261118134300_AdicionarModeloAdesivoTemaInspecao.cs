using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarModeloAdesivoTemaInspecao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ModeloAdesivo",
                table: "TemasInspecao",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Só aditiva: as linhas existentes ficam como Liberado (1), exceto os temas padrão de LOCAL/DISPOSITIVO,
            // que recebem Inspecionado (2). Mesmo mapeamento de TemasInspecaoPadraoSeeder.ModeloAdesivoPorTema; o seeder
            // não reinsere tema que já existe, então o ajuste dos temas já cadastrados precisa vir aqui.
            migrationBuilder.Sql(
                "UPDATE [TemasInspecao] SET [ModeloAdesivo] = 2 WHERE [Nome] IN " +
                "(N'Extintores', N'Instalações Elétricas', N'Proteções Coletivas (EPC)', N'EPI', N'Espaço Confinado', " +
                "N'Produtos Químicos e Inflamáveis')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModeloAdesivo",
                table: "TemasInspecao");
        }
    }
}
