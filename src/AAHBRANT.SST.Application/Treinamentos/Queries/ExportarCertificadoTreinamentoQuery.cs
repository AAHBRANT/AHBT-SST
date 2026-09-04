using AAHBRANT.SST.Application.Assinatura;
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
    private readonly IQrCodeDocumentoService _qrCode;

    public ExportarCertificadoTreinamentoQueryHandler(IAppDbContext db, ICertificadoTreinamentoPdfService pdf, IQrCodeDocumentoService qrCode)
    {
        _db = db;
        _pdf = pdf;
        _qrCode = qrCode;
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

        // QR de verificação (item 6 da proposta do usuário, 04/09) — mesmo token público já usado no
        // comprovante genérico de assinatura (DocumentoAssinaturaPdfService); só é gerado se o
        // documento já tiver sido finalizado (token só existe a partir daí).
        var qrCodePng = !string.IsNullOrEmpty(documento?.TokenValidacaoPublica)
            ? _qrCode.Gerar(documento.TokenValidacaoPublica).Png
            : null;

        // Foto da turma (item 6 da proposta) — só existe quando este Treinamento foi gerado pelo
        // encerramento de uma SessaoTreinamento (fluxo antigo, criado manualmente por trabalhador,
        // continua sem foto). Usa a primeira das 3 fotos obrigatórias (Ordem = 1) como representativa.
        byte[]? fotoTurma = null;
        if (treinamento.SessaoTreinamentoId is not null)
        {
            fotoTurma = await _db.FotosEvidenciaSessaoTreinamento
                .Where(f => f.SessaoTreinamentoId == treinamento.SessaoTreinamentoId && f.Ativo)
                .OrderBy(f => f.Ordem)
                .Select(f => f.FotoConteudo)
                .FirstOrDefaultAsync(ct);
        }

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
            signatarios,
            qrCodePng,
            fotoTurma);

        return _pdf.Gerar(modelo);
    }
}
