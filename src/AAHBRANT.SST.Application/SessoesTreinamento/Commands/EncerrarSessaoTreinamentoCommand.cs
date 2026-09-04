using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Encerramento da turma (pedido do usuário: 3 fotos obrigatórias liberam o botão) — gera 1
// Treinamento (e, portanto, 1 certificado — ver AssinaturaCertificadoTreinamentoDialog.tsx, que já
// funciona por Treinamento) para cada participante que confirmou presença por biometria. Quem não
// confirmou fica registrado na turma como ausente, sem certificado — decisão assumida para não
// inventar uma regra de "falta justificada" que não foi pedida.
public record EncerrarSessaoTreinamentoCommand(Guid Id) : IRequest;

public class EncerrarSessaoTreinamentoCommandValidator : AbstractValidator<EncerrarSessaoTreinamentoCommand>
{
    public EncerrarSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public class EncerrarSessaoTreinamentoCommandHandler : IRequestHandler<EncerrarSessaoTreinamentoCommand>
{
    private const int TotalFotosObrigatorias = 3;
    private readonly IAppDbContext _db;

    public EncerrarSessaoTreinamentoCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(EncerrarSessaoTreinamentoCommand request, CancellationToken ct)
    {
        var sessao = await _db.SessoesTreinamento
            .Include(s => s.CursoTreinamento)
            .FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Turma de treinamento {request.Id} não encontrada.");

        if (sessao.Status == StatusSessaoTreinamento.Concluida)
            throw new InvalidOperationException("Esta turma já foi encerrada.");

        var totalFotos = await _db.FotosEvidenciaSessaoTreinamento.CountAsync(f => f.SessaoTreinamentoId == sessao.Id && f.Ativo, ct);
        if (totalFotos < TotalFotosObrigatorias)
            throw new InvalidOperationException(
                $"São necessárias {TotalFotosObrigatorias} fotos de evidência para encerrar a turma (faltam {TotalFotosObrigatorias - totalFotos}).");

        var participantes = await _db.ParticipantesSessaoTreinamento
            .Where(p => p.SessaoTreinamentoId == sessao.Id && p.Ativo)
            .ToListAsync(ct);

        var validadeEmMeses = sessao.CursoTreinamento!.ValidadeEmMeses;
        foreach (var participante in participantes.Where(p => p.PresencaConfirmadaEm is not null && p.TreinamentoGeradoId is null))
        {
            var treinamento = new Treinamento
            {
                TrabalhadorId = participante.TrabalhadorId,
                CursoTreinamentoId = sessao.CursoTreinamentoId,
                DataRealizacao = sessao.DataRealizacao,
                DataValidade = sessao.DataRealizacao.AddMonths(validadeEmMeses),
                CargaHorariaRealizada = sessao.CargaHorariaRealizada,
                InstituicaoInstrutor = sessao.InstituicaoInstrutor,
                NumeroCertificado = sessao.NumeroCertificado,
                SessaoTreinamentoId = sessao.Id,
            };
            _db.Treinamentos.Add(treinamento);
            participante.TreinamentoGeradoId = treinamento.Id;
        }

        sessao.Status = StatusSessaoTreinamento.Concluida;
        sessao.DataEncerramento = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
