using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record DocumentoPublicoSignatarioDto(string TrabalhadorNome, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm);

// DTO da página pública de validação (/#/validar/{token}). Deliberadamente sem DocumentoAssinaturaId/
// EntidadeId (ver comentário em DocumentoAssinatura.cs: "Nunca expor Id/EntidadeId/dado pessoal na
// página pública") — só o que o próprio token já revela: tipo do documento, quando foi emitido, hash
// de integridade, se tem assinatura registrada e quem assinou (se houver).
public record DocumentoPublicoDto(
    string EntidadeTipo,
    DateTime EmitidoEm,
    string ConteudoHash,
    bool Assinado,
    List<DocumentoPublicoSignatarioDto> Signatarios);

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
        var documento = await _db.DocumentosAssinatura
            .Where(d => d.TokenValidacaoPublica == request.Token)
            .FirstOrDefaultAsync(ct);
        if (documento is null)
            return null;

        var signatarios = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            .Join(_db.Trabalhadores, s => s.TrabalhadorId, t => t.Id,
                (s, t) => new DocumentoPublicoSignatarioDto(t.Nome, s.MetodoAutenticacao, s.AssinadoEm))
            .OrderBy(s => s.AssinadoEm)
            .ToListAsync(ct);

        var emitidoEm = documento.FinalizadoEm ?? documento.RastreadoEm ?? documento.CreatedAtUtc;
        return new DocumentoPublicoDto(documento.EntidadeTipo, emitidoEm, documento.ConteudoHash!, signatarios.Count > 0, signatarios);
    }
}
