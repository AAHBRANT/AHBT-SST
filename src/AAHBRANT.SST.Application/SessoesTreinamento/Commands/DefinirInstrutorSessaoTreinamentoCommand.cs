using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Instrutores;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Escolhe o instrutor da turma entre os perfis de instrutor cadastrados (pedido do usuário, 28/10: o
// instrutor é selecionado antes do curso). O nome impresso no certificado passa a ser o dele. Vale para a
// turma em andamento e também para turmas concluídas cujo instrutor ainda não assinou.
public record DefinirInstrutorSessaoTreinamentoCommand(Guid SessaoTreinamentoId, Guid InstrutorTrabalhadorId) : IRequest;

public class DefinirInstrutorSessaoTreinamentoCommandValidator : AbstractValidator<DefinirInstrutorSessaoTreinamentoCommand>
{
    public DefinirInstrutorSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.InstrutorTrabalhadorId).NotEmpty();
    }
}

public class DefinirInstrutorSessaoTreinamentoCommandHandler : IRequestHandler<DefinirInstrutorSessaoTreinamentoCommand>
{
    private readonly IAppDbContext _db;

    public DefinirInstrutorSessaoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(DefinirInstrutorSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento.FirstOrDefaultAsync(s => s.Id == request.SessaoTreinamentoId, ct)
            ?? throw new KeyNotFoundException("Turma não encontrada.");

        var perfil = await _db.PerfisInstrutor.FirstOrDefaultAsync(p => p.TrabalhadorId == request.InstrutorTrabalhadorId, ct)
            ?? throw new InvalidOperationException("Este trabalhador não tem perfil de instrutor cadastrado.");

        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == perfil.TrabalhadorId && t.Ativo, ct)
            ?? throw new KeyNotFoundException("Instrutor não encontrado.");

        // Trocar o instrutor depois que ele já assinou apagaria a evidência de quem assinou o quê.
        var certificadosDaTurma = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.TreinamentoGeradoId != null)
            .Select(p => p.TreinamentoGeradoId!.Value)
            .ToListAsync(ct);
        var jaAssinou = await _db.DocumentoSignatarios.AnyAsync(s =>
            s.Papel == PapelAssinatura.Instrutor
            && _db.DocumentosAssinatura.Any(d => d.Id == s.DocumentoAssinaturaId && d.EntidadeTipo == "Treinamento"
                && certificadosDaTurma.Contains(d.EntidadeId)), ct);
        if (jaAssinou && sessao.InstrutorTrabalhadorId != trabalhador.Id)
            throw new InvalidOperationException("O instrutor desta turma já assinou os certificados e não pode ser trocado.");

        sessao.InstrutorTrabalhadorId = trabalhador.Id;
        sessao.InstituicaoInstrutor = trabalhador.Nome;
        var registro = DescricaoInstrutor.Registro(perfil.Conselho, perfil.NumeroRegistro, perfil.UfRegistro);
        if (registro is not null)
            sessao.InstrutorRegistroProfissional = registro;

        await _db.SaveChangesAsync(ct);
    }
}
