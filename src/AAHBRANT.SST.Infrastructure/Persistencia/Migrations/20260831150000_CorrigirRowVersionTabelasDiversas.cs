using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class CorrigirRowVersionTabelasDiversas : Migration
    {
        // Extraída da reconciliação com a master (31/08) — parte útil de uma migration duplicada que
        // também tentava remover MatrizLegal/GestaoDocumental (já removidos por
        // 20260828210131_RemoverMatrizLegalEGestaoDocumental, vinda da master, idempotente). SQL
        // Server rejeita ALTER COLUMN direto para timestamp/rowversion mesmo vindo de varbinary(max)
        // ("Cannot alter column 'RowVersion' to be data type timestamp", erro 4927) — mesma
        // limitação já corrigida em CorrigirRowVersionAcaoPlano/CorrigirRowVersionDocumentoGestao/
        // etc. A única forma é dropar e recriar a coluna. Isso reseta o token de concorrência
        // otimista de linhas existentes (inofensivo em Development/hml — sem linhas reais dependendo
        // dele). CredenciaisWebAuthn ficou fora da lista original: a tabela já não existe mais
        // (removida por 20260831122000_RemoverPinEWebAuthn).
        private static readonly string[] Tabelas =
        {
            "TemplatesBiometricoFutronic",
            "RegrasAlerta",
            "RegistrosHhtMensais",
            "DocumentoSignatarios",
            "DocumentosAssinatura",
            "DispositivosAgenteBiometrico",
            "CalendariosEventosTeams",
            "AtivosSst",
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var tabela in Tabelas)
            {
                migrationBuilder.DropColumn(name: "RowVersion", table: tabela);
                migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: tabela, type: "rowversion", rowVersion: true, nullable: true);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tabela in Tabelas)
            {
                migrationBuilder.AlterColumn<byte[]>(
                    name: "RowVersion",
                    table: tabela,
                    type: "varbinary(max)",
                    nullable: true,
                    oldClrType: typeof(byte[]),
                    oldType: "rowversion",
                    oldRowVersion: true,
                    oldNullable: true);
            }
        }
    }
}
