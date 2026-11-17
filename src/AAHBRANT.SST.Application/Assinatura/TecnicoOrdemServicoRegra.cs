using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura;

// Quem pode assinar como técnico na Ordem de Serviço (decisão de 16/11/2026): só por biometria e só pessoa certa.
//  (a) OS emitida dentro da Integração em etapas (o dono da OS é participante de um atendimento em andamento, na
//      obra da OS, com a etapa da OS aberta): assina SOMENTE o instrutor da integração.
//  (b) OS fora da integração (perfil do trabalhador): Técnico ou Engenheiro de Segurança da PRÓPRIA obra da OS.
// Nunca o dono da OS. As consultas ignoram o filtro de escopo (o instrutor da integração pode ser de outra obra) e
// comparam a obra e o "Ativo" explicitamente.
public static class TecnicoOrdemServicoRegra
{
    public const string MensagemNaoEhInstrutorDigital = "A digital lida não é a do instrutor desta integração.";
    public const string MensagemNaoEhInstrutorFace = "A face lida não é a do instrutor desta integração.";
    public const string MensagemNaoEhTecnicoDigital = "A digital lida não é de um técnico ou engenheiro de segurança desta obra.";
    public const string MensagemNaoEhTecnicoFace = "A face lida não é de um técnico ou engenheiro de segurança desta obra.";
    public const string MensagemLoginRecusado =
        "A assinatura do técnico na Ordem de Serviço é feita só por digital ou reconhecimento facial. Use a leitura biométrica para assinar.";

    // ObraId da OS; Funcionario = dono; InstrutorIntegracaoId != null quando a OS está dentro de uma integração em andamento.
    public sealed record ContextoOs(Guid OrdemServicoId, Guid ObraId, Guid FuncionarioId, Guid? InstrutorIntegracaoId);

    public static async Task<ContextoOs> CarregarContextoAsync(IAppDbContext db, Guid documentoAssinaturaId, CancellationToken ct)
    {
        var entidadeId = await db.DocumentosAssinatura
            .Where(d => d.Id == documentoAssinaturaId && d.EntidadeTipo == OrdemServico.EntidadeTipoAssinatura)
            .Select(d => (Guid?)d.EntidadeId)
            .FirstOrDefaultAsync(ct);
        if (entidadeId is null)
            throw new KeyNotFoundException("Documento de assinatura da Ordem de Serviço não encontrado.");
        return await CarregarContextoPorOsAsync(db, entidadeId.Value, ct);
    }

    public static async Task<ContextoOs> CarregarContextoPorOsAsync(IAppDbContext db, Guid ordemServicoId, CancellationToken ct)
    {
        var os = await db.OrdensServico.IgnoreQueryFilters()
            .Where(o => o.Id == ordemServicoId)
            .Select(o => new { o.Id, o.ObraId, o.TrabalhadorId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Ordem de Serviço não encontrada.");

        var instrutorId = await (
            from p in db.ParticipantesIntegracao.IgnoreQueryFilters()
            join a in db.IntegracoesAtendimento.IgnoreQueryFilters() on p.IntegracaoAtendimentoId equals a.Id
            join e in db.IntegracaoEtapas.IgnoreQueryFilters() on a.Id equals e.IntegracaoAtendimentoId
            where p.Ativo && a.Ativo && e.Ativo
                && p.TrabalhadorId == os.TrabalhadorId
                && a.ObraId == os.ObraId
                && a.Status == StatusIntegracaoAtendimento.EmAndamento
                && e.Tipo == TipoEtapaIntegracao.OrdemServico
                && e.Status == StatusEtapaIntegracao.EmAndamento
            orderby a.IniciadaEmUtc descending
            select (Guid?)a.InstrutorTrabalhadorId)
            .FirstOrDefaultAsync(ct);

        return new ContextoOs(os.Id, os.ObraId, os.TrabalhadorId, instrutorId);
    }

    // Confere a pessoa identificada pela biometria contra a regra. Lança InvalidOperationException (400) com o texto pedido.
    public static void GarantirIdentidade(ContextoOs contexto, Trabalhador identificado, MetodoAutenticacaoAssinatura metodo)
    {
        var facial = metodo == MetodoAutenticacaoAssinatura.ReconhecimentoFacial;
        if (identificado.Id == contexto.FuncionarioId)
            throw new InvalidOperationException("O próprio funcionário não pode assinar a OS como técnico de segurança.");

        if (contexto.InstrutorIntegracaoId is { } instrutorId)
        {
            if (identificado.Id != instrutorId)
                throw new InvalidOperationException(facial ? MensagemNaoEhInstrutorFace : MensagemNaoEhInstrutorDigital);
            return;
        }

        var funcaoOk = FuncaoSstClassifier.EhTecnicoSeguranca(identificado.Funcao?.Nome)
            || FuncaoSstClassifier.EhEngenheiroSeguranca(identificado.Funcao?.Nome);
        var daObra = identificado.ObraId == contexto.ObraId
            && identificado.Ativo && identificado.Situacao != SituacaoTrabalhador.Desligado;
        if (!funcaoOk || !daObra)
            throw new InvalidOperationException(facial ? MensagemNaoEhTecnicoFace : MensagemNaoEhTecnicoDigital);
    }
}
