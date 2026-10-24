using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Pcmsos.Revisoes;

// PCMSO: mesmo fluxo de NovaRevisaoPcmsoCommand (aprovado em 10/10/2026). O PDF novo vira uma revisão e
// o anterior não é apagado. Se o PDF atual do PCMSO ainda não está guardado em nenhuma revisão (anexado
// antes desta mudança), ele é arquivado primeiro na revisão mais recente sem PDF — ou numa revisão
// "documento anterior" criada para isso — para que nada se perca na troca.
public record NovaRevisaoPcmsoCommand(
    Guid PcmsoId,
    int? NumeroRevisao,
    DateTime DataRevisao,
    string Motivo,
    byte[] Conteudo,
    string ContentType,
    string? NomeArquivo) : IRequest<Guid>;

public class NovaRevisaoPcmsoCommandValidator : AbstractValidator<NovaRevisaoPcmsoCommand>
{
    public NovaRevisaoPcmsoCommandValidator()
    {
        RuleFor(x => x.PcmsoId).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
        RuleFor(x => x.DataRevisao).NotEmpty();
        RuleFor(x => x.NumeroRevisao).GreaterThanOrEqualTo(0).When(x => x.NumeroRevisao.HasValue);
        RuleFor(x => x.NomeArquivo).MaximumLength(260);
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O documento é obrigatório.")
            .Must(c => c.Length <= RevisaoDocumento.TamanhoMaximoBytes).WithMessage("O documento deve ter no máximo 20 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.ContentType).Equal("application/pdf").WithMessage("O documento deve ser um arquivo PDF.");
    }
}

public class NovaRevisaoPcmsoCommandHandler : IRequestHandler<NovaRevisaoPcmsoCommand, Guid>
{
    private readonly IAppDbContext _db;

    public NovaRevisaoPcmsoCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(NovaRevisaoPcmsoCommand request, CancellationToken ct)
    {
        // PcmsoDetalhes tem filtro por obra: PCMSO de obra de fora não é encontrado (404).
        var pcmso = await _db.PcmsoDetalhes.FirstOrDefaultAsync(p => p.Id == request.PcmsoId, ct)
            ?? throw new KeyNotFoundException($"PCMSO {request.PcmsoId} não encontrado.");

        var revisoes = await _db.PcmsoRevisoes
            .Where(r => r.PcmsoDetalheId == pcmso.Id)
            .Select(r => new { r.Id, r.NumeroRevisao, TemPdf = r.DocumentoConteudo != null })
            .ToListAsync(ct);

        var precisaArquivar = pcmso.DocumentoConteudo is { Length: > 0 } && !revisoes.Any(r => r.TemPdf);
        // Sem número informado: a seguinte à última; sem revisões e com PDF a arquivar, começa na 01
        // para o PDF atual ficar como 00.
        var numero = request.NumeroRevisao
            ?? (revisoes.Count == 0 ? (precisaArquivar ? 1 : 0) : revisoes.Max(r => r.NumeroRevisao) + 1);
        if (revisoes.Any(r => r.NumeroRevisao == numero))
            throw new InvalidOperationException($"A revisão {numero:00} já existe neste PCMSO. Informe outro número.");

        if (precisaArquivar)
        {
            var semPdf = revisoes.Where(r => r.NumeroRevisao < numero).OrderByDescending(r => r.NumeroRevisao).FirstOrDefault();
            if (semPdf is not null)
            {
                var antiga = await _db.PcmsoRevisoes.FirstAsync(r => r.Id == semPdf.Id, ct);
                antiga.DocumentoConteudo = pcmso.DocumentoConteudo;
                antiga.DocumentoContentType = pcmso.DocumentoContentType;
                antiga.DocumentoNomeArquivo = RevisaoDocumento.NomeDocumentoAnterior;
            }
            else
            {
                var numeroAnterior = RevisaoDocumento.NumeroLivreAbaixo(numero, revisoes.Select(r => r.NumeroRevisao))
                    ?? throw new InvalidOperationException(
                        "O PDF atual precisa ser guardado como revisão anterior. Informe um número de revisão maior.");
                _db.PcmsoRevisoes.Add(new PcmsoRevisao
                {
                    PcmsoDetalheId = pcmso.Id,
                    NumeroRevisao = numeroAnterior,
                    DataRevisao = pcmso.DataEmissao,
                    Motivo = RevisaoDocumento.MotivoDocumentoAnterior,
                    DocumentoConteudo = pcmso.DocumentoConteudo,
                    DocumentoContentType = pcmso.DocumentoContentType,
                    DocumentoNomeArquivo = RevisaoDocumento.NomeDocumentoAnterior,
                });
            }
        }

        var revisao = new PcmsoRevisao
        {
            PcmsoDetalheId = pcmso.Id,
            NumeroRevisao = numero,
            DataRevisao = request.DataRevisao,
            Motivo = request.Motivo.Trim(),
            DocumentoConteudo = request.Conteudo,
            DocumentoContentType = request.ContentType,
            DocumentoNomeArquivo = request.NomeArquivo,
        };
        _db.PcmsoRevisoes.Add(revisao);

        pcmso.DocumentoConteudo = request.Conteudo;
        pcmso.DocumentoContentType = request.ContentType;
        pcmso.Versao = numero.ToString("00");

        await _db.SaveChangesAsync(ct);
        return revisao.Id;
    }
}
