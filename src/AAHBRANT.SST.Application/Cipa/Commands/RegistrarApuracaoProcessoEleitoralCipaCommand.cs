using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Cipa.Commands;

public record VotoApuradoCipa(Guid CandidatoId, int Votos);

// Apuração é sempre manual — este sistema não implementa urna digital (ver disclosure em
// Domain/Entidades/Cipa/Cipa.cs). Classifica os candidatos deferidos por votos (desempate por ordem
// de inscrição), preenche titulares/suplentes conforme o Dimensionamento mais recente da obra, e já
// cria os MembroCipa correspondentes (Cargo Titular/Suplente, Origem Empregado). DataFimMandato é
// sempre informado explicitamente por quem apura — o sistema não assume a duração legal do mandato.
public record RegistrarApuracaoProcessoEleitoralCipaCommand(
    Guid ProcessoEleitoralId,
    List<VotoApuradoCipa> Votos,
    DateTime DataInicioMandato,
    DateTime DataFimMandato) : IRequest<List<Guid>>;

public class RegistrarApuracaoProcessoEleitoralCipaCommandValidator : AbstractValidator<RegistrarApuracaoProcessoEleitoralCipaCommand>
{
    public RegistrarApuracaoProcessoEleitoralCipaCommandValidator()
    {
        RuleFor(x => x.ProcessoEleitoralId).NotEmpty();
        RuleFor(x => x.Votos).NotEmpty().WithMessage("Informe os votos de ao menos um candidato.");
        RuleForEach(x => x.Votos).ChildRules(v => v.RuleFor(y => y.Votos).GreaterThanOrEqualTo(0));
        RuleFor(x => x.DataFimMandato).GreaterThan(x => x.DataInicioMandato);
    }
}

public class RegistrarApuracaoProcessoEleitoralCipaCommandHandler : IRequestHandler<RegistrarApuracaoProcessoEleitoralCipaCommand, List<Guid>>
{
    private readonly IAppDbContext _db;

    public RegistrarApuracaoProcessoEleitoralCipaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<List<Guid>> Handle(RegistrarApuracaoProcessoEleitoralCipaCommand request, CancellationToken ct)
    {
        var processo = await _db.ProcessosEleitoraisCipa.FirstOrDefaultAsync(p => p.Id == request.ProcessoEleitoralId, ct)
            ?? throw new KeyNotFoundException($"Processo eleitoral {request.ProcessoEleitoralId} não encontrado.");

        if (processo.Status == StatusProcessoEleitoralCipa.Apurado || processo.Status == StatusProcessoEleitoralCipa.Encerrado)
            throw new InvalidOperationException("Este processo eleitoral já foi apurado.");

        var candidatos = await _db.CandidatosCipa
            .Where(c => c.ProcessoEleitoralId == request.ProcessoEleitoralId && c.Ativo)
            .ToListAsync(ct);

        var candidatosDeferidos = candidatos.Where(c => c.Status == StatusCandidatoCipa.Deferido).ToList();
        if (candidatosDeferidos.Count == 0)
            throw new InvalidOperationException("Não há candidatos deferidos para apurar.");

        var dimensionamento = await _db.DimensionamentosCipa
            .Where(d => d.ObraId == processo.ObraId)
            .OrderByDescending(d => d.DataCalculo)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "Não há dimensionamento cadastrado para esta obra — cadastre o número de titulares/suplentes antes de apurar.");

        var votosPorCandidato = request.Votos.ToDictionary(v => v.CandidatoId, v => v.Votos);
        foreach (var candidato in candidatosDeferidos)
        {
            if (votosPorCandidato.TryGetValue(candidato.Id, out var votos))
                candidato.VotosRecebidos = votos;
        }

        var ordenados = candidatosDeferidos
            .OrderByDescending(c => c.VotosRecebidos)
            .ThenBy(c => c.DataInscricao)
            .ToList();

        var membrosIds = new List<Guid>();
        for (var i = 0; i < ordenados.Count; i++)
        {
            var candidato = ordenados[i];
            CargoMembroCipa? cargo = i < dimensionamento.NumeroTitulares
                ? CargoMembroCipa.Titular
                : i < dimensionamento.NumeroTitulares + dimensionamento.NumeroSuplentes
                    ? CargoMembroCipa.Suplente
                    : null;

            candidato.Status = cargo switch
            {
                CargoMembroCipa.Titular => StatusCandidatoCipa.Eleito,
                CargoMembroCipa.Suplente => StatusCandidatoCipa.Suplente,
                _ => StatusCandidatoCipa.NaoEleito,
            };

            if (cargo is null) continue;

            var membro = new MembroCipa
            {
                ObraId = processo.ObraId,
                TrabalhadorId = candidato.TrabalhadorId,
                OrigemMembro = OrigemMembroCipa.Empregado,
                Cargo = cargo.Value,
                DataInicioMandato = request.DataInicioMandato,
                DataFimMandato = request.DataFimMandato,
                ProcessoEleitoralId = processo.Id,
                CandidatoCipaId = candidato.Id,
            };
            _db.MembrosCipa.Add(membro);
            membrosIds.Add(membro.Id);
        }

        processo.Status = StatusProcessoEleitoralCipa.Apurado;
        processo.DataApuracao = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return membrosIds;
    }
}
