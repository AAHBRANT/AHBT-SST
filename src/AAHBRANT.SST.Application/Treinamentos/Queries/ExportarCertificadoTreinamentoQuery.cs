using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Queries;

public record ExportarCertificadoTreinamentoQuery(Guid TreinamentoId) : IRequest<byte[]?>;

public class ExportarCertificadoTreinamentoQueryHandler : IRequestHandler<ExportarCertificadoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly ICertificadoTreinamentoPdfService _pdf;

    public ExportarCertificadoTreinamentoQueryHandler(IAppDbContext db, ICertificadoTreinamentoPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    public async Task<byte[]?> Handle(ExportarCertificadoTreinamentoQuery request, CancellationToken ct)
    {
        var treinamento = await _db.Treinamentos
            .Include(t => t.CursoTreinamento)
            .Include(t => t.Trabalhador)
                .ThenInclude(t => t!.Obra)
            .Include(t => t.Trabalhador)
                .ThenInclude(t => t!.Funcao)
            .FirstOrDefaultAsync(t => t.Id == request.TreinamentoId, ct);
        if (treinamento is null || treinamento.Trabalhador is null || treinamento.CursoTreinamento is null) return null;

        // Um DocumentoAssinatura por treinamento (EntidadeTipo="Treinamento", EntidadeId=Treinamento.Id) —
        // ver docs/Motor-Assinatura-Eletronica.md, mesmo padrão de ExportarFichaEpiTrabalhadorQuery.
        var documento = await _db.DocumentosAssinatura
            .Include(d => d.Signatarios)
                .ThenInclude(s => s.Trabalhador)
            .Where(d => d.EntidadeTipo == "Treinamento" && d.EntidadeId == request.TreinamentoId)
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        var signatarios = documento?.Signatarios
            .Select(s => new CertificadoTreinamentoPdfSignatarioModelo(s.Trabalhador?.Nome ?? string.Empty, s.AssinadoEm))
            .ToList() ?? new List<CertificadoTreinamentoPdfSignatarioModelo>();

        var modelo = new CertificadoTreinamentoPdfModelo(
            treinamento.Trabalhador.Obra?.Nome ?? string.Empty,
            treinamento.Trabalhador.Obra?.Cnpj,
            treinamento.Trabalhador.Obra?.Endereco,
            treinamento.Trabalhador.Obra?.Cidade,
            treinamento.Trabalhador.Obra?.Uf,
            treinamento.Trabalhador.Nome,
            CpfMascarador.Mascarar(treinamento.Trabalhador.Cpf),
            treinamento.Trabalhador.Rg,
            treinamento.Trabalhador.Funcao?.Nome ?? string.Empty,
            treinamento.CursoTreinamento.Nome,
            treinamento.CursoTreinamento.NormaReferencia,
            treinamento.CursoTreinamento.CargaHorariaMinima,
            treinamento.CargaHorariaRealizada,
            treinamento.DataRealizacao,
            treinamento.DataValidade,
            treinamento.InstituicaoInstrutor,
            treinamento.NumeroCertificado,
            treinamento.CursoTreinamento.ConteudoProgramatico,
            signatarios);

        return _pdf.Gerar(modelo);
    }
}
