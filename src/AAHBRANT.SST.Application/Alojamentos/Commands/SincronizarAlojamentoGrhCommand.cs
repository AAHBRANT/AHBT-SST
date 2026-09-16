using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Alojamentos.Commands;

// Upsert de Alojamento a partir do cadastro do G-RH (Integração G-RH — G-RH é a fonte exclusiva do
// cadastro de alojamentos, nunca criado manualmente no SST, ver docs/superpowers/2026-09-15-pedido-
// integracao-alojamento-grh.md). Usado tanto pelo consumidor de eventos
// (ServiceBusAlojamentoGrhProcessor, um alojamento por vez) quanto pela carga inicial
// (ImportarAlojamentosGrhCommand, em lote) — mesma lógica de correspondência nos dois casos, espelha
// SincronizarColaboradorGrhCommand.
public record SincronizarAlojamentoGrhCommand(
    string GrhAlojamentoId,
    string Nome,
    string? ObraNome,
    string? Endereco,
    bool Ativo,
    IReadOnlyList<AlojamentoMoradorGrhDto> Moradores) : IRequest<Guid>
{
    public static SincronizarAlojamentoGrhCommand DoAlojamentoGrh(AlojamentoGrhDto a) =>
        new(a.GrhAlojamentoId, a.Nome, a.ObraNome, a.Endereco, a.Ativo, a.Moradores);
}

public class SincronizarAlojamentoGrhCommandValidator : AbstractValidator<SincronizarAlojamentoGrhCommand>
{
    public SincronizarAlojamentoGrhCommandValidator()
    {
        RuleFor(x => x.GrhAlojamentoId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Endereco).MaximumLength(300);
    }
}

public class SincronizarAlojamentoGrhCommandHandler : IRequestHandler<SincronizarAlojamentoGrhCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ICpfHashService _cpfHash;

    public SincronizarAlojamentoGrhCommandHandler(IAppDbContext db, ICpfHashService cpfHash)
    {
        _db = db;
        _cpfHash = cpfHash;
    }

    public async Task<Guid> Handle(SincronizarAlojamentoGrhCommand request, CancellationToken ct)
    {
        // IgnoreQueryFilters: Alojamento tem HasQueryFilter de escopo por obra (SstDbContext) — o
        // consumidor de fila/importação em lote não roda no contexto de um usuário logado com obras
        // permitidas, então precisa enxergar todos os alojamentos (inclusive soft-deletados
        // localmente, pra reativar em vez de duplicar — mesmo padrão de SincronizarColaboradorGrhCommand).
        var alojamento = await _db.Alojamentos.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.GrhAlojamentoId == request.GrhAlojamentoId, ct);

        Obra? obra = null;
        if (!string.IsNullOrWhiteSpace(request.ObraNome))
            obra = await _db.Obras.FirstOrDefaultAsync(o => o.Nome == request.ObraNome, ct);

        if (alojamento is null)
        {
            if (obra is null)
                throw new InvalidOperationException(
                    $"Obra '{request.ObraNome}' não encontrada no SST — não é possível criar o alojamento '{request.Nome}' vindo do G-RH sem uma obra correspondente.");

            alojamento = new Alojamento
            {
                GrhAlojamentoId = request.GrhAlojamentoId,
                Nome = request.Nome,
                Endereco = request.Endereco,
                ObraId = obra.Id,
            };
            _db.Alojamentos.Add(alojamento);
        }
        else
        {
            alojamento.Nome = request.Nome;
            alojamento.Endereco = request.Endereco;
            if (obra is not null) alojamento.ObraId = obra.Id;
        }

        // Reflete o status do G-RH diretamente — inclusive desativando quando o G-RH marca o
        // alojamento como não mais ativo (antes só reativava, nunca desativava; ver campo Ativo em
        // AlojamentoGrhDto).
        alojamento.Ativo = request.Ativo;
        alojamento.DataUltimaSincronizacao = DateTime.UtcNow;

        await SincronizarMoradoresAsync(alojamento.Id, request.Moradores, ct);
        await _db.SaveChangesAsync(ct);

        return alojamento.Id;
    }

    // G-RH manda sempre a lista completa e atual de moradores — trata como fonte de verdade:
    // quem não está mais na lista, saiu; quem trocou de alojamento, fecha o vínculo antigo e abre um
    // novo (o índice único garante que nunca haverá dois vínculos ativos simultâneos, ver
    // AlojamentoConfiguracoes.cs).
    private async Task SincronizarMoradoresAsync(
        Guid alojamentoId, IReadOnlyList<AlojamentoMoradorGrhDto> moradoresGrh, CancellationToken ct)
    {
        var hashesInformados = moradoresGrh.Select(m => _cpfHash.CalcularHash(m.Cpf)).ToHashSet();

        var vinculosAtivosDoAlojamento = await _db.AlojamentoMoradores
            .Include(m => m.Trabalhador)
            .Where(m => m.AlojamentoId == alojamentoId && m.DataSaida == null)
            .ToListAsync(ct);

        foreach (var vinculo in vinculosAtivosDoAlojamento)
        {
            var hashAtual = vinculo.Trabalhador?.CpfHash;
            if (hashAtual is null || !hashesInformados.Contains(hashAtual))
                vinculo.DataSaida = DateTime.UtcNow;
        }

        foreach (var moradorGrh in moradoresGrh)
        {
            var hash = _cpfHash.CalcularHash(moradorGrh.Cpf);

            // IgnoreQueryFilters por consistência com a busca de Alojamento acima — trabalhador
            // desligado (Ativo=false) é descartado explicitamente logo abaixo, não pelo filtro.
            var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters()
                .FirstOrDefaultAsync(t => t.CpfHash == hash, ct);
            if (trabalhador is null || !trabalhador.Ativo)
                continue; // ainda não sincronizado do lado de Colaborador, ou desligado — não falha o alojamento inteiro

            var vinculoAtivo = await _db.AlojamentoMoradores
                .FirstOrDefaultAsync(m => m.TrabalhadorId == trabalhador.Id && m.DataSaida == null, ct);

            if (vinculoAtivo is not null && vinculoAtivo.AlojamentoId == alojamentoId)
                continue; // já mora aqui, nada a fazer

            if (vinculoAtivo is not null)
                vinculoAtivo.DataSaida = moradorGrh.Desde; // mudou de alojamento — fecha o vínculo anterior

            _db.AlojamentoMoradores.Add(new AlojamentoMorador
            {
                AlojamentoId = alojamentoId,
                TrabalhadorId = trabalhador.Id,
                DataDesde = moradorGrh.Desde,
            });
        }
    }
}
