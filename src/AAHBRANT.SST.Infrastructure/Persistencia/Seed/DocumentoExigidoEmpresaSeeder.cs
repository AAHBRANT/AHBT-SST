using AAHBRANT.SST.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Ponto de partida do checklist da empresa terceirizada (especificação 21/10/2026, decisão 5): os quatro
// documentos que o usuário aprovou como padrão. A lista é editável pelo Técnico de Segurança e precisa
// ser validada com o Jurídico. Roda só enquanto a tabela nunca teve linha (IgnoreQueryFilters): se o
// técnico desativar ou trocar os documentos, o seeder não os recoloca.
public static class DocumentoExigidoEmpresaSeeder
{
    private static readonly (string Nome, string Descricao, bool ExigeValidade)[] Padrao =
    {
        ("PGR", "Programa de Gerenciamento de Riscos da empresa terceirizada (NR-01).", true),
        ("PCMSO", "Programa de Controle Médico de Saúde Ocupacional da empresa terceirizada (NR-07).", true),
        ("Cartão CNPJ", "Comprovante de inscrição e situação cadastral emitido pela Receita Federal.", true),
        ("Contrato social", "Contrato social ou última alteração consolidada.", false),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        if (await db.DocumentosExigidosEmpresa.IgnoreQueryFilters().AnyAsync(ct)) return;

        var ordem = 1;
        foreach (var (nome, descricao, exigeValidade) in Padrao)
            db.DocumentosExigidosEmpresa.Add(new DocumentoExigidoEmpresa
            {
                Nome = nome, Descricao = descricao, ExigeValidade = exigeValidade, Ordem = ordem++,
            });
        await db.SaveChangesAsync(ct);
    }
}
