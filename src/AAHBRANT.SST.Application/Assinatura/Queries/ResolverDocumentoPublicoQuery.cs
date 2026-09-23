using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record DocumentoPublicoSignatarioDto(
    string TrabalhadorNome,
    MetodoAutenticacaoAssinatura MetodoAutenticacao,
    DateTime AssinadoEm,
    // Origem de rede da assinatura, mascarada (ver IpMascarador) — atesta que há registro de onde a
    // assinatura partiu sem publicar o endereço completo numa página anônima.
    string? OrigemRede);

// DTO da página pública de validação (/#/validar/{token}). Deliberadamente sem DocumentoAssinaturaId/
// EntidadeId (ver comentário em DocumentoAssinatura.cs: "Nunca expor Id/EntidadeId/dado pessoal na
// página pública") — só o que o próprio token já revela: tipo do documento, quando foi emitido, hash
// de integridade, se tem assinatura registrada e quem assinou (se houver).
public record DocumentoPublicoDto(
    string EntidadeTipo,
    DateTime EmitidoEm,
    string ConteudoHash,
    bool Assinado,
    List<DocumentoPublicoSignatarioDto> Signatarios,
    // Rótulo institucional do tipo (ver TipoDocumentoAssinatura) — o nome técnico da entidade
    // ("FichaEpiTrabalhador") não significa nada para o fiscal/cliente que escaneia o QR.
    string EntidadeTipoRotulo,
    // Documento que agrega N registros individuais e não recebe assinatura própria; Signatarios,
    // nesse caso, vem dos registros agregados.
    bool Consolidado,
    string? OrigemAssinaturas,
    // Integridade do conteúdo: SHA-256 do PDF exato que foi emitido. ConteudoHash acima cobre só a
    // lista de signatários — quem precisa provar que o documento em mãos não foi adulterado compara
    // este valor com o SHA-256 do arquivo.
    string? HashPdf,
    DateTime? ArquivoAtualizadoEm);

public record ResolverDocumentoPublicoQuery(string Token) : IRequest<DocumentoPublicoDto?>;

public class ResolverDocumentoPublicoQueryValidator : AbstractValidator<ResolverDocumentoPublicoQuery>
{
    public ResolverDocumentoPublicoQueryValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
    }
}

public class ResolverDocumentoPublicoQueryHandler : IRequestHandler<ResolverDocumentoPublicoQuery, DocumentoPublicoDto?>
{
    private readonly IAppDbContext _db;

    public ResolverDocumentoPublicoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DocumentoPublicoDto?> Handle(ResolverDocumentoPublicoQuery request, CancellationToken ct)
    {
        // Resolve por token, independente de Status: rastreabilidade (Task 2) gera token/hash sem
        // exigir finalização, então um documento ainda EmAndamento (ou que nunca finaliza — CIPA,
        // DDS Semanal) também precisa ser validável publicamente.
        // Projeção explícita sem PdfConteudo: o arquivo pode ter megabytes, esta query roda a cada
        // leitura da página pública e o PDF não é servido por aqui — só o SHA-256 dele.
        var documento = await _db.DocumentosAssinatura
            .Where(d => d.TokenValidacaoPublica == request.Token)
            .Select(d => new
            {
                d.Id,
                d.EntidadeTipo,
                d.EntidadeId,
                d.ConteudoHash,
                d.HashPdf,
                d.ArquivoAtualizadoEm,
                d.FinalizadoEm,
                d.RastreadoEm,
                d.CreatedAtUtc,
            })
            .FirstOrDefaultAsync(ct);
        if (documento is null)
            return null;

        // IgnoreQueryFilters() no lado de Trabalhadores: essa página é pública (sem usuário logado),
        // então o filtro global de RBAC por obra (Trabalhador.ObraId ∈ ObrasPermitidas do usuário
        // atual) não tem como ser avaliado — e o EF falha ao tentar traduzir a query (erro só visível
        // em produção com autenticação real; em dev, com Entra ID desligado, o filtro degenera para
        // uma constante e mascara o problema). Ativo é reaplicado manualmente para preservar o
        // soft-delete. OrderBy antes do Select (não depois) evita depender de ordenar pelo DTO já
        // construído, mais uma fonte comum de falha de tradução do EF.
        var signatarios = await CarregarSignatariosAsync(new List<Guid> { documento.Id }, ct);

        // Documento consolidado (Ficha de EPI, Ata de Sessão, DDS Semanal) nunca tem signatário
        // próprio — quem assina são os registros que ele agrega. Sem isto, a página pública
        // afirmava "nenhuma assinatura" sobre um PDF que mostra assinatura em cada linha.
        var consolidado = TipoDocumentoAssinatura.EhConsolidado(documento.EntidadeTipo);
        if (consolidado && signatarios.Count == 0)
            signatarios = await CarregarSignatariosAgregadosAsync(documento.EntidadeTipo, documento.EntidadeId, ct);

        var emitidoEm = documento.FinalizadoEm ?? documento.RastreadoEm ?? documento.CreatedAtUtc;
        return new DocumentoPublicoDto(
            documento.EntidadeTipo,
            emitidoEm,
            documento.ConteudoHash!,
            signatarios.Count > 0,
            signatarios,
            TipoDocumentoAssinatura.Rotulo(documento.EntidadeTipo),
            consolidado,
            TipoDocumentoAssinatura.OrigemAssinaturas(documento.EntidadeTipo),
            documento.HashPdf,
            documento.ArquivoAtualizadoEm);
    }

    private async Task<List<DocumentoPublicoSignatarioDto>> CarregarSignatariosAsync(
        List<Guid> documentoIds, CancellationToken ct)
    {
        // Mascaramento em memória (IpMascarador não tem tradução para SQL), depois do OrderBy.
        var linhas = await _db.DocumentoSignatarios
            .Where(s => documentoIds.Contains(s.DocumentoAssinaturaId))
            .Join(_db.Trabalhadores.IgnoreQueryFilters().Where(t => t.Ativo), s => s.TrabalhadorId, t => t.Id,
                (s, t) => new { t.Nome, s.MetodoAutenticacao, s.AssinadoEm, s.IpAddress })
            .OrderBy(x => x.AssinadoEm)
            .ToListAsync(ct);

        return linhas
            .Select(x => new DocumentoPublicoSignatarioDto(
                x.Nome, x.MetodoAutenticacao, x.AssinadoEm, IpMascarador.Mascarar(x.IpAddress)))
            .ToList();
    }

    // Resolve os registros individuais que um documento consolidado agrega e devolve as assinaturas
    // deles. IgnoreQueryFilters em todo lado pelo mesmo motivo do Join acima: rota anônima, sem
    // usuário para o filtro global de RBAC por obra avaliar — o EF só falha a tradução em produção.
    private async Task<List<DocumentoPublicoSignatarioDto>> CarregarSignatariosAgregadosAsync(
        string entidadeTipo, Guid entidadeId, CancellationToken ct)
    {
        List<Guid> idsAgregados;
        string[] tiposAgregados;

        switch (entidadeTipo)
        {
            case "FichaEpiTrabalhador":
                // EntidadeId da Ficha é o TrabalhadorId (chave sintética — ver ExportarFichaEpiTrabalhadorQuery).
                idsAgregados = await _db.EntregasEpi.IgnoreQueryFilters()
                    .Where(e => e.Ativo && e.TrabalhadorId == entidadeId)
                    .Select(e => e.Id)
                    .ToListAsync(ct);
                tiposAgregados = new[] { "EntregaEpi", "DevolucaoEpi" };
                break;

            case "SessaoTreinamento":
                idsAgregados = await _db.ParticipantesSessaoTreinamento.IgnoreQueryFilters()
                    .Where(p => p.Ativo && p.SessaoTreinamentoId == entidadeId && p.TreinamentoGeradoId != null)
                    .Select(p => p.TreinamentoGeradoId!.Value)
                    .ToListAsync(ct);
                tiposAgregados = new[] { "Treinamento" };
                break;

            case "DdsSemanal":
                idsAgregados = await _db.Dds.IgnoreQueryFilters()
                    .Where(d => d.Ativo && d.DdsSemanalId == entidadeId)
                    .Select(d => d.Id)
                    .ToListAsync(ct);
                tiposAgregados = new[] { "Dds" };
                break;

            default:
                return new List<DocumentoPublicoSignatarioDto>();
        }

        if (idsAgregados.Count == 0)
            return new List<DocumentoPublicoSignatarioDto>();

        var documentosAgregados = await _db.DocumentosAssinatura
            .Where(d => tiposAgregados.Contains(d.EntidadeTipo) && idsAgregados.Contains(d.EntidadeId))
            .Select(d => d.Id)
            .ToListAsync(ct);

        return await CarregarSignatariosAsync(documentosAgregados, ct);
    }
}
