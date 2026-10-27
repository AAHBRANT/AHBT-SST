using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.GeracaoPgr;

// Assinatura eletrônica do PGR pelo Motor de Assinatura: o técnico de segurança da obra assina ao enviar para aprovação e o
// engenheiro ao aprovar, cada um com a própria sessão (quem prepara o PGR escolhe os dois no passo 4). Quem foi marcado como
// "assinatura à mão" não assina no sistema. O documento do motor é um só por geração (EntidadeTipo "GeracaoPgrEap").
public static class AssinaturaGeracaoPgr
{
    public const string EntidadeTipo = "GeracaoPgrEap";
    public const string RotuloTecnico = "técnico de segurança da obra";
    public const string RotuloEngenheiro = "engenheiro de segurança";

    // Escolha feita e completa: alguém (ou "à mão") e, para assinar no sistema, o registro profissional.
    public static void ConferirEscolha(AssinanteGeracaoPgr? assinante, string rotulo)
    {
        if (assinante is null || (!assinante.AMao && assinante.UsuarioId is null))
            throw new InvalidOperationException($"Escolha quem assina como {rotulo}, ou marque assinatura à mão, no cartão Quem assina.");
        if (!assinante.AMao && string.IsNullOrWhiteSpace(assinante.Registro))
            throw new InvalidOperationException($"Informe o registro profissional de {assinante.Nome ?? rotulo} ({rotulo}) no cartão Quem assina.");
    }

    // Só a pessoa escolhida assina a própria linha, e o usuário dela precisa estar ligado a um cadastro de trabalhador, porque o
    // motor guarda sempre um trabalhador como signatário. Devolve o trabalhador.
    public static async Task<Guid> ConferirUsuarioAsync(
        IAppDbContext db, AssinanteGeracaoPgr assinante, string rotulo, Guid? usuarioAtualId, CancellationToken ct)
    {
        if (usuarioAtualId is null)
            throw new InvalidOperationException("Não foi possível identificar o seu usuário para assinar.");
        if (assinante.UsuarioId != usuarioAtualId)
            throw new InvalidOperationException(
                $"A assinatura de {rotulo} é de {assinante.Nome ?? "outra pessoa"}. Peça para essa pessoa assinar, ou troque a pessoa no cartão Quem assina (o engenheiro pode devolver o PGR para ajustes).");
        var trabalhadorId = await db.Usuarios.Where(u => u.Id == usuarioAtualId).Select(u => u.TrabalhadorId).FirstOrDefaultAsync(ct);
        if (trabalhadorId is null)
            throw new InvalidOperationException(
                "Seu usuário não está vinculado a um cadastro de trabalhador. Peça a um administrador para vincular seu usuário antes de assinar.");
        return trabalhadorId.Value;
    }

    // Garante o documento do motor e registra a assinatura. Repetir não duplica: quem já assinou naquele papel é ignorado.
    public static async Task AssinarAsync(
        IAppDbContext db, IRegistradorAssinaturaService registrador, Guid geracaoId, Guid trabalhadorId, PapelAssinatura papel, CancellationToken ct)
    {
        var documento = await db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.EntidadeTipo == EntidadeTipo && d.EntidadeId == geracaoId, ct);
        if (documento is null)
        {
            documento = new DocumentoAssinatura { EntidadeTipo = EntidadeTipo, EntidadeId = geracaoId };
            db.DocumentosAssinatura.Add(documento);
            await db.SaveChangesAsync(ct);
        }

        var jaAssinou = await db.DocumentoSignatarios.AnyAsync(
            s => s.DocumentoAssinaturaId == documento.Id && s.TrabalhadorId == trabalhadorId && s.Papel == papel, ct);
        if (jaAssinou)
            return;

        await registrador.RegistrarAsync(
            documento.Id, new ResultadoAutenticacaoAssinatura(trabalhadorId, MetodoAutenticacaoAssinatura.SessaoLogada), null, ct, papel: papel);
    }
}
