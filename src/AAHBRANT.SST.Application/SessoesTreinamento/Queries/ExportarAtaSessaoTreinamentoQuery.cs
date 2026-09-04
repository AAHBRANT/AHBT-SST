using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Queries;

public record ExportarAtaSessaoTreinamentoQuery(Guid SessaoTreinamentoId) : IRequest<byte[]?>;

public class ExportarAtaSessaoTreinamentoQueryHandler : IRequestHandler<ExportarAtaSessaoTreinamentoQuery, byte[]?>
{
    private readonly IAppDbContext _db;
    private readonly IAtaSessaoTreinamentoPdfService _pdf;
    private readonly IRegistradorRastreabilidadeService _rastreabilidade;

    public ExportarAtaSessaoTreinamentoQueryHandler(IAppDbContext db, IAtaSessaoTreinamentoPdfService pdf, IRegistradorRastreabilidadeService rastreabilidade)
    {
        _db = db;
        _pdf = pdf;
        _rastreabilidade = rastreabilidade;
    }

    public async Task<byte[]?> Handle(ExportarAtaSessaoTreinamentoQuery request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.Obra)
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.SessaoTreinamentoId, ct);
        if (sessao is null || sessao.Obra is null || sessao.CursoTreinamento is null) return null;

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .Include(p => p.Trabalhador)
            .OrderBy(p => p.Trabalhador!.Nome)
            .Select(p => new AtaSessaoTreinamentoPdfParticipanteModelo(
                p.Trabalhador!.Nome, p.Trabalhador.Matricula, p.PresencaConfirmadaEm))
            .ToListAsync(ct);

        var fotos = await _db.FotosEvidenciaSessaoTreinamento
            .Where(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .Select(f => f.FotoConteudo)
            .ToListAsync(ct);

        // Chave sintética "SessaoTreinamento"/SessaoTreinamentoId: a Ata agrega N participantes, cada
        // um já individualmente assinado via seu próprio DocumentoAssinatura("Treinamento", Id) —
        // ninguém assina a Ata em si, então TemAssinatura nunca é usado aqui (RodapeDocumentoPadrao
        // recebe temAssinatura: false diretamente no PdfService).
        var rastreio = await _rastreabilidade.GarantirAsync("SessaoTreinamento", sessao.Id, ct);

        var modelo = new AtaSessaoTreinamentoPdfModelo(
            sessao.Obra.Nome,
            sessao.Obra.LogoConteudo,
            sessao.CursoTreinamento.Nome,
            sessao.CursoTreinamento.NormaReferencia,
            sessao.DataRealizacao,
            sessao.CargaHorariaRealizada,
            sessao.InstituicaoInstrutor,
            sessao.NumeroCertificado,
            sessao.DataEncerramento,
            participantes,
            fotos,
            rastreio.ConteudoHash,
            rastreio.UrlValidacaoPublica,
            rastreio.QrCodePng);

        return _pdf.Gerar(modelo);
    }
}
