using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Criação da turma (pedido do usuário, 04/09) — o responsável define curso/data/carga horária e já
// seleciona os participantes (diferente do DDS, onde o participante só aparece quando a biometria
// confirma presença). A lista de trabalhadores disponíveis para seleção é filtrada por Obra no
// frontend (mesmo princípio de DdsDetalhePage.tsx) — não reforçado aqui no backend.
//
// Número do certificado (04/09, pedido do usuário): passou a ser gerado automaticamente aqui
// (mesmo padrão "PREFIXO-ANO-0001" de APR/DDS/PT/PCMSO — ver GeradorNumeroDocumentoService), não
// mais digitado manualmente. Isso é uma exceção deliberada ao princípio geral de
// IGeradorNumeroDocumentoService de não numerar automaticamente números que vêm de fora do sistema
// (CAT, CA de EPI): o usuário confirmou explicitamente que este número é um controle interno da
// empresa, não um número emitido por terceiro.
public record CriarSessaoTreinamentoCommand(
    Guid ObraId,
    Guid CursoTreinamentoId,
    DateTime DataRealizacao,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    List<Guid> TrabalhadoresIds) : IRequest<Guid>;

public class CriarSessaoTreinamentoCommandValidator : AbstractValidator<CriarSessaoTreinamentoCommand>
{
    public CriarSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.CursoTreinamentoId).NotEmpty();
        RuleFor(x => x.DataRealizacao).NotEmpty();
        RuleFor(x => x.CargaHorariaRealizada).GreaterThan(0);
        RuleFor(x => x.TrabalhadoresIds).NotEmpty().WithMessage("Selecione ao menos um participante.");
    }
}

public class CriarSessaoTreinamentoCommandHandler : IRequestHandler<CriarSessaoTreinamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarSessaoTreinamentoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<Guid> Handle(CriarSessaoTreinamentoCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");

        if (!await _db.CursosTreinamento.AnyAsync(c => c.Id == request.CursoTreinamentoId, ct))
            throw new KeyNotFoundException("Curso de treinamento não encontrado.");

        var sessao = new SessaoTreinamento
        {
            ObraId = request.ObraId,
            CursoTreinamentoId = request.CursoTreinamentoId,
            DataRealizacao = request.DataRealizacao,
            CargaHorariaRealizada = request.CargaHorariaRealizada,
            InstituicaoInstrutor = request.InstituicaoInstrutor,
            NumeroCertificado = await _geradorNumero.GerarAsync("CERT", ct),
        };

        foreach (var trabalhadorId in request.TrabalhadoresIds.Distinct())
        {
            sessao.Participantes.Add(new ParticipanteSessaoTreinamento
            {
                SessaoTreinamentoId = sessao.Id,
                TrabalhadorId = trabalhadorId,
            });
        }

        _db.SessoesTreinamento.Add(sessao);
        await _db.SaveChangesAsync(ct);
        return sessao.Id;
    }
}
