using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Integracoes;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// Inicia o atendimento "Realizar integração" de uma obra (decisão de 11/11/2026): calcula as 5 etapas de ordem fixa,
// marca como Puladas as de curso sem ninguém, abre a primeira que tem gente (cria a turma da etapa) e grava os
// participantes. Um atendimento em andamento por obra. AzureAdObjectId/AutenticacaoHabilitada vêm do controller.
public record IniciarIntegracaoCommand(
    Guid ObraId,
    List<Guid> TrabalhadoresIds,
    Guid? InstrutorTrabalhadorId = null,
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<Guid>;

public class IniciarIntegracaoCommandValidator : AbstractValidator<IniciarIntegracaoCommand>
{
    public IniciarIntegracaoCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.TrabalhadoresIds).NotEmpty().WithMessage("Selecione ao menos uma pessoa para a integração.");
    }
}

public class IniciarIntegracaoCommandHandler : IRequestHandler<IniciarIntegracaoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;
    private readonly IOptions<OpcoesIntegracao> _opcoes;

    public IniciarIntegracaoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero, IOptions<OpcoesIntegracao> opcoes)
    {
        _db = db;
        _geradorNumero = geradorNumero;
        _opcoes = opcoes;
    }

    public async Task<Guid> Handle(IniciarIntegracaoCommand request, CancellationToken ct)
    {
        var responsavel = await AutorizacaoIntegracaoAtendimento.AutorizarAsync(
            _db, request.ObraId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        if (!_opcoes.Value.AtendimentoHabilitado)
            throw new InvalidOperationException("O atendimento de integração ainda não está habilitado.");

        if (await _db.IntegracoesAtendimento.AnyAsync(a => a.ObraId == request.ObraId && a.Status == StatusIntegracaoAtendimento.EmAndamento, ct))
            throw new IntegracaoConflitoException(IntegracaoConflitoException.Padrao);

        var ids = request.TrabalhadoresIds.Where(i => i != Guid.Empty).Distinct().ToList();
        var trabalhadores = await _db.Trabalhadores
            .Where(t => ids.Contains(t.Id) && t.ObraId == request.ObraId && t.Situacao == SituacaoTrabalhador.Ativo
                && t.Vinculo != TipoVinculo.PrestadorServico)
            .ToListAsync(ct);
        if (trabalhadores.Count != ids.Count)
            throw new InvalidOperationException("Há pessoas que não estão ativas nesta obra (ou não participam da integração). Atualize a lista e tente de novo.");

        var instrutorId = request.InstrutorTrabalhadorId
            ?? await InstrutorDoUsuarioAsync(request, ct)
            ?? throw new InvalidOperationException("Escolha o instrutor da integração.");
        await GarantirInstrutorAsync(instrutorId, request.ObraId, ct);

        var obraNome = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct);
        var cursos = await ExigenciasEtapasIntegracao.CarregarCursosAsync(_db, obraNome, ct);
        var pessoas = await ExigenciasEtapasIntegracao.CarregarPessoasAsync(_db, trabalhadores, request.ObraId, ct);
        var calculadas = CalculadoraEtapasIntegracao.Calcular(pessoas, cursos);

        var semCurso = calculadas.FirstOrDefault(e => !e.Pulada && e.Tipo != TipoEtapaIntegracao.OrdemServico && e.CursoId is null);
        if (semCurso is not null)
            throw new InvalidOperationException(
                $"O curso {CalculadoraEtapasIntegracao.NormaDoTipo(semCurso.Tipo)} não está cadastrado no catálogo de cursos. Cadastre o curso antes de iniciar a integração.");

        var atendimento = new IntegracaoAtendimento
        {
            ObraId = request.ObraId,
            Numero = await _geradorNumero.GerarAsync("INT", ct),
            ResponsavelUsuarioId = responsavel.UsuarioId,
            InstrutorTrabalhadorId = instrutorId,
            IniciadaEmUtc = DateTime.UtcNow,
        };
        _db.IntegracoesAtendimento.Add(atendimento);

        var funcaoIds = trabalhadores.Select(t => t.FuncaoId).Distinct().ToList();
        var funcoes = await _db.Funcoes.Where(f => funcaoIds.Contains(f.Id))
            .Select(f => new { f.Id, f.Nome }).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        var planosAbertos = await _db.PlanosIntegracao
            .Where(p => p.ObraId == request.ObraId && p.Status == StatusPlanoIntegracao.Aberto && ids.Contains(p.TrabalhadorId))
            .Select(p => new { p.Id, p.TrabalhadorId }).ToListAsync(ct);
        foreach (var t in trabalhadores.OrderBy(t => t.Nome))
        {
            _db.ParticipantesIntegracao.Add(new ParticipanteIntegracao
            {
                ObraId = request.ObraId,
                IntegracaoAtendimentoId = atendimento.Id,
                TrabalhadorId = t.Id,
                PlanoIntegracaoId = planosAbertos.FirstOrDefault(p => p.TrabalhadorId == t.Id)?.Id,
                FuncaoId = t.FuncaoId,
                FuncaoNome = funcoes.GetValueOrDefault(t.FuncaoId, string.Empty),
                Nome = t.Nome,
            });
        }

        var abriu = false;
        foreach (var c in calculadas)
        {
            var etapa = new IntegracaoEtapa
            {
                ObraId = request.ObraId,
                IntegracaoAtendimentoId = atendimento.Id,
                Ordem = c.Ordem,
                Tipo = c.Tipo,
                CursoTreinamentoId = c.CursoId,
                CargaMinimaHoras = c.CargaMinimaHoras,
                Status = c.Pulada ? StatusEtapaIntegracao.Pulada : StatusEtapaIntegracao.Pendente,
            };
            _db.IntegracaoEtapas.Add(etapa);
            if (!abriu && !c.Pulada)
            {
                await AberturaEtapaIntegracao.AbrirAsync(_db, _geradorNumero, atendimento, etapa, c.Participantes, ct);
                abriu = true;
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Corrida: outro atendimento da obra entrou antes (índice único). Mensagem amigável, não 500.
            if (await OutroEmAndamentoAsync(request.ObraId, atendimento.Id, ct))
                throw new IntegracaoConflitoException(IntegracaoConflitoException.Padrao);
            throw;
        }
        return atendimento.Id;
    }

    private async Task<bool> OutroEmAndamentoAsync(Guid obraId, Guid meuId, CancellationToken ct) =>
        await _db.IntegracoesAtendimento.IgnoreQueryFilters()
            .AnyAsync(a => a.ObraId == obraId && a.Id != meuId && a.Ativo && a.Status == StatusIntegracaoAtendimento.EmAndamento, ct);

    private async Task<Guid?> InstrutorDoUsuarioAsync(IniciarIntegracaoCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AzureAdObjectId)) return null;
        var trabalhadorId = await _db.Usuarios.Where(u => u.AzureAdObjectId == request.AzureAdObjectId)
            .Select(u => u.TrabalhadorId).FirstOrDefaultAsync(ct);
        return trabalhadorId is { } id && await _db.PerfisInstrutor.AnyAsync(p => p.TrabalhadorId == id, ct) ? id : null;
    }

    // Instrutor = pessoa com perfil de instrutor, ativa; de outra obra só com a autorização que o cadastro já guarda.
    private async Task GarantirInstrutorAsync(Guid trabalhadorId, Guid obraId, CancellationToken ct)
    {
        var perfil = await _db.PerfisInstrutor.FirstOrDefaultAsync(p => p.TrabalhadorId == trabalhadorId, ct)
            ?? throw new InvalidOperationException("Este trabalhador não tem perfil de instrutor cadastrado.");
        var instrutor = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Id == trabalhadorId && t.Ativo && t.Situacao == SituacaoTrabalhador.Ativo)
            .Select(t => new { t.ObraId }).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("O instrutor escolhido não está ativo.");
        if (instrutor.ObraId != obraId && !(perfil.PodeAssinarEmOutrasObras && perfil.AutorizadoOutrasObrasEm is not null))
            throw new InvalidOperationException("Este instrutor não está autorizado a assinar nesta obra.");
    }
}
