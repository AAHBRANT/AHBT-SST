using System.Security.Cryptography;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Evidência fotográfica da turma (pedido do usuário: mínimo 3 fotos) — mesmo padrão de
// AnexarFotoEvidenciaDdsCommand, obrigatórias para encerrar (ver EncerrarSessaoTreinamentoCommand).
//
// Slot fixo por Ordem (04/09, pedido do usuário — grade de 3 quadros individuais, cada um
// substituível): reanexar no mesmo quadro SUBSTITUI o conteúdo em vez de criar um novo registro.
public record AnexarFotoEvidenciaSessaoTreinamentoCommand(
    Guid SessaoTreinamentoId,
    int Ordem,
    byte[] FotoConteudo,
    string FotoContentType, string? Metadados = null,
    // Turma de etapa de integração: token de captura emitido pelo servidor (ver EmitirTokenCapturaFotoCommand).
    string? TokenCaptura = null) : IRequest<Guid>;

public class AnexarFotoEvidenciaSessaoTreinamentoCommandValidator : AbstractValidator<AnexarFotoEvidenciaSessaoTreinamentoCommand>
{
    private static readonly string[] TiposPermitidos = { "image/jpeg", "image/png" };
    private const int TamanhoMaximoBytes = 5 * 1024 * 1024;
    private const int TotalFotosMaximo = 3;

    public AnexarFotoEvidenciaSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.Ordem).InclusiveBetween(1, TotalFotosMaximo);
        RuleFor(x => x.FotoConteudo)
            .NotEmpty().WithMessage("A foto é obrigatória.")
            .Must(f => f.Length <= TamanhoMaximoBytes).WithMessage("A foto deve ter no máximo 5 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.FotoContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.FotoContentType)
            .Must(t => TiposPermitidos.Contains(t)).WithMessage("A foto deve ser um arquivo JPEG ou PNG.");
    }
}

public class AnexarFotoEvidenciaSessaoTreinamentoCommandHandler : IRequestHandler<AnexarFotoEvidenciaSessaoTreinamentoCommand, Guid>
{
    private readonly IAppDbContext _db;

    private readonly AAHBRANT.SST.Application.Common.Interfaces.ICurrentUserService? _usuario;

    public AnexarFotoEvidenciaSessaoTreinamentoCommandHandler(IAppDbContext db, AAHBRANT.SST.Application.Common.Interfaces.ICurrentUserService? usuario = null)
    {
        _db = db;
        _usuario = usuario;
    }

    public async Task<Guid> Handle(AnexarFotoEvidenciaSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var registro = await _db.SessoesTreinamento.FirstOrDefaultAsync(x => x.Id == request.SessaoTreinamentoId, ct)
            ?? throw new KeyNotFoundException("Registro não encontrado.");
        if (registro.Status == AAHBRANT.SST.Domain.Enums.StatusSessaoTreinamento.Concluida)
            throw new InvalidOperationException("O registro já foi finalizado. As fotos não podem ser alteradas.");
        var metadados = await DadosCapturaFoto.PrepararAsync(request.Metadados, registro.Id, registro.ObraId, _db, ct);

        // SHA-256 da foto: guardado sempre; recusado quando já existe em OUTRA foto (qualquer turma/etapa) ou numa
        // foto ativa de outro quadro da mesma turma (a mesma foto nunca vale para dois cursos).
        var hash = Convert.ToHexString(SHA256.HashData(request.FotoConteudo));
        var etapa = await AAHBRANT.SST.Application.IntegracoesAtendimento.ProtecaoTurmaIntegracao
            .EtapaEmAndamentoDaTurmaAsync(_db, registro.Id, ct);
        TokenCapturaFoto? token = null;
        if (etapa is not null)
        {
            // Etapa de integração: a hora da captura é a do servidor, no consumo de um token de uso único (5 min, desta turma e
            // deste usuário). O relógio do aparelho não conta, então relógio atrasado não barra a foto nem adiantado a antecipa.
            var agora = DateTime.UtcNow;
            token = string.IsNullOrWhiteSpace(request.TokenCaptura) ? null
                : await _db.TokensCapturaFoto.FirstOrDefaultAsync(t => t.Token == request.TokenCaptura && t.SessaoTreinamentoId == registro.Id, ct);
            if (token is null || token.UsadoEmUtc is not null || token.ExpiraEmUtc < agora || token.UsuarioId != _usuario?.UsuarioId)
                throw new InvalidOperationException(
                    "A captura precisa de um token válido emitido para esta turma (uso único, 5 minutos). Abra a câmera de novo para tirar a foto.");
            var dados = DadosCapturaFoto.Ler(metadados)! with { CapturadaEm = agora, LocalizacaoObtidaEm = agora };
            metadados = DadosCapturaFoto.Serializar(dados);
            if (agora < etapa.IniciadaEmUtc)
                throw new InvalidOperationException("A foto precisa ser tirada depois do início desta etapa da integração.");

            var repetida = await _db.FotosEvidenciaSessaoTreinamento.IgnoreQueryFilters()
                .AnyAsync(f => f.FotoHash == hash
                    && (f.SessaoTreinamentoId != registro.Id || (f.Ativo && f.Ordem != request.Ordem)), ct);
            if (repetida)
                throw new InvalidOperationException(
                    "Esta foto já foi usada como evidência em outra etapa ou turma. Cada etapa precisa de fotos próprias, tiradas na hora.");
        }

        var fotoExistente = await _db.FotosEvidenciaSessaoTreinamento
            .FirstOrDefaultAsync(f => f.SessaoTreinamentoId == request.SessaoTreinamentoId && f.Ordem == request.Ordem && f.Ativo, ct);

        if (fotoExistente is not null) fotoExistente.Ativo = false;

        var foto = new FotoEvidenciaSessaoTreinamento
        {
            SessaoTreinamentoId = request.SessaoTreinamentoId,
            Ordem = request.Ordem,
            FotoConteudo = request.FotoConteudo,
            FotoContentType = request.FotoContentType,
            FotoMetadadosJson = metadados,
            FotoHash = hash,
        };
        _db.FotosEvidenciaSessaoTreinamento.Add(foto);
        if (token is not null) token.UsadoEmUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return foto.Id;
    }
}
