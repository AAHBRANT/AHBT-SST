using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Treinamentos.Commands;

// Anexa (ou substitui) o certificado digitalizado de um treinamento — pedido do usuário em 22/09
// para o lançamento retroativo da obra em andamento. Aceita o PDF original ou a foto do papel,
// porque boa parte dos certificados antigos só existe impresso.
public record AnexarArquivoCertificadoTreinamentoCommand(
    Guid TreinamentoId,
    string NomeArquivo,
    byte[] Conteudo,
    string ContentType,
    int Rotacao = 0) : IRequest<Guid>;

public class AnexarArquivoCertificadoTreinamentoCommandValidator : AbstractValidator<AnexarArquivoCertificadoTreinamentoCommand>
{
    private static readonly string[] TiposPermitidos = { "application/pdf", "image/jpeg", "image/png" };

    // 10 MB: acomoda um PDF escaneado de 2 páginas em 300 dpi, que é o pior caso realista. O front
    // ainda comprime foto antes de enviar, então o limite raramente é alcançado por foto de câmera.
    private const int TamanhoMaximoBytes = 10 * 1024 * 1024;

    public AnexarArquivoCertificadoTreinamentoCommandValidator()
    {
        RuleFor(x => x.TreinamentoId).NotEmpty();
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType)
            .Must(t => TiposPermitidos.Contains(t))
            .WithMessage("O certificado deve ser um arquivo PDF, JPEG ou PNG.");
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O arquivo do certificado é obrigatório.")
            .Must(c => c.Length <= TamanhoMaximoBytes).WithMessage("O arquivo deve ter no máximo 10 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
    }
}

public class AnexarArquivoCertificadoTreinamentoCommandHandler : IRequestHandler<AnexarArquivoCertificadoTreinamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly AAHBRANT.SST.Application.Treinamentos.LeituraCertificado.IProcessadorArquivoCertificado? _processador;

    public AnexarArquivoCertificadoTreinamentoCommandHandler(IAppDbContext db,
        AAHBRANT.SST.Application.Treinamentos.LeituraCertificado.IProcessadorArquivoCertificado? processador = null)
    {
        _db = db;
        _processador = processador;
    }

    public async Task<Guid> Handle(AnexarArquivoCertificadoTreinamentoCommand original, CancellationToken ct)
    {
        // Rotação indicada pela leitura da IA (07/11/2026): o arquivo é guardado já em pé; o original não fica.
        var request = original;
        if (_processador is not null && original.Rotacao is 90 or 180 or 270)
        {
            var (conteudo, tipo) = _processador.Girar(original.Conteudo, original.ContentType, original.Rotacao);
            request = original with { Conteudo = conteudo, ContentType = tipo };
        }
        var treinamento = await _db.Treinamentos.NoEscopoDoTrabalhador(_db, t => t.TrabalhadorId)
            .Where(t => t.Id == request.TreinamentoId)
            .Select(t => new { t.OrigemCertificado, t.SessaoTreinamentoId })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"Treinamento {request.TreinamentoId} não encontrado.");
        // Certificado de terceiro não pode ficar marcado como AAHBRANT (09/11/2026): o sistema geraria o modelo da empresa
        // com os dados de outro curso. Anexo só em certificado Externo ou em treinamento de turma da AAHBRANT.
        if (treinamento.OrigemCertificado == Domain.Enums.OrigemCertificadoTreinamento.Aahbrant && treinamento.SessaoTreinamentoId is null)
            throw new InvalidOperationException(
                "Este treinamento está como curso da AAHBRANT. Para anexar o certificado de outra empresa, marque a origem como Externo.");

        // Substituição no lugar de acumular versões: a decisão do usuário é um arquivo por
        // certificado, e reenviar é o caminho natural quando a primeira foto sai tremida/ilegível.
        var arquivoExistente = await _db.ArquivosCertificadoTreinamento
            .FirstOrDefaultAsync(a => a.TreinamentoId == request.TreinamentoId, ct);

        if (arquivoExistente is not null)
        {
            arquivoExistente.NomeArquivo = request.NomeArquivo;
            arquivoExistente.ContentType = request.ContentType;
            arquivoExistente.Conteudo = request.Conteudo;
            arquivoExistente.TamanhoBytes = request.Conteudo.LongLength;
            await _db.SaveChangesAsync(ct);
            return arquivoExistente.Id;
        }

        var arquivo = new ArquivoCertificadoTreinamento
        {
            TreinamentoId = request.TreinamentoId,
            NomeArquivo = request.NomeArquivo,
            ContentType = request.ContentType,
            Conteudo = request.Conteudo,
            TamanhoBytes = request.Conteudo.LongLength,
        };
        _db.ArquivosCertificadoTreinamento.Add(arquivo);
        await _db.SaveChangesAsync(ct);
        return arquivo.Id;
    }
}
