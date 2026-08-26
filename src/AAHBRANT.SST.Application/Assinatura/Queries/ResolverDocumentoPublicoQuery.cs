using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Queries;

public record DocumentoPublicoSignatarioDto(string TrabalhadorNome, MetodoAutenticacaoAssinatura MetodoAutenticacao, DateTime AssinadoEm);

// DTO da página pública de validação (/#/validar/{token} — docs/Motor-Assinatura-Eletronica.md §5,
// etapa 11). Deliberadamente sem DocumentoAssinaturaId/EntidadeId (ver comentário em
// DocumentoAssinatura.cs: "Nunca expor Id/EntidadeId/dado pessoal na página pública") — só o que o
// próprio token já revela: tipo do documento, quando foi finalizado, hash de integridade e quem assinou.
public record DocumentoPublicoDto(
    string EntidadeTipo,
    DateTime FinalizadoEm,
    string ConteudoHash,
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
        // Só documentos finalizados resolvem — ConteudoHash/FinalizadoEm só existem a partir da
        // finalização (FinalizarDocumentoCommand), mas o filtro explícito por Status deixa a regra
        // clara mesmo se algum dia esses campos virarem opcionais por outro motivo.
        var documento = await _db.DocumentosAssinatura
            .Where(d => d.TokenValidacaoPublica == request.Token && d.Status == StatusDocumentoAssinatura.Finalizado)
            .FirstOrDefaultAsync(ct);
        if (documento is null)
            return null;

        var signatarios = await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinaturaId == documento.Id)
            .Join(_db.Trabalhadores, s => s.TrabalhadorId, t => t.Id,
                (s, t) => new DocumentoPublicoSignatarioDto(t.Nome, s.MetodoAutenticacao, s.AssinadoEm))
            .OrderBy(s => s.AssinadoEm)
            .ToListAsync(ct);

        return new DocumentoPublicoDto(documento.EntidadeTipo, documento.FinalizadoEm!.Value, documento.ConteudoHash!, signatarios);
    }
}
