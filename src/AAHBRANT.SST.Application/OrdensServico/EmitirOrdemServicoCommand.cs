using System.Text.Json;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.OrdensServico;

// Emite a OS a partir da minuta (etapa 4 — 03/11/2026): número, revisão, cópia congelada do conteúdo e
// o documento de assinatura. Só com a minuta sem pendências. Se o trabalhador já tem uma OS da obra
// esperando assinatura, devolve essa mesma em vez de criar outra — a tela segue para a assinatura.
public record EmitirOrdemServicoCommand(Guid TrabalhadorId) : IRequest<OrdemServicoEmitidaDto>;

public class EmitirOrdemServicoCommandHandler : IRequestHandler<EmitirOrdemServicoCommand, OrdemServicoEmitidaDto>
{
    private readonly IAppDbContext _db;
    private readonly IMediator _mediator;
    private readonly IGeradorNumeroDocumentoService _numero;
    private readonly IAuditoriaService _auditoria;

    public EmitirOrdemServicoCommandHandler(IAppDbContext db, IMediator mediator, IGeradorNumeroDocumentoService numero, IAuditoriaService auditoria)
    {
        _db = db;
        _mediator = mediator;
        _numero = numero;
        _auditoria = auditoria;
    }

    public async Task<OrdemServicoEmitidaDto> Handle(EmitirOrdemServicoCommand request, CancellationToken ct)
    {
        var minuta = await _mediator.Send(new ObterMinutaOrdemServicoQuery(request.TrabalhadorId), ct)
            ?? throw new KeyNotFoundException("Trabalhador não encontrado.");
        if (!minuta.PodeEmitir)
            throw new InvalidOperationException(
                $"A Ordem de Serviço ainda tem {minuta.Pendencias.Count} pendência(s). Resolva as pendências antes de emitir.");

        var obraId = minuta.Obra.Id;
        var anteriores = await _db.OrdensServico
            .Where(o => o.TrabalhadorId == request.TrabalhadorId && o.ObraId == obraId)
            .ToListAsync(ct);
        var idsAnteriores = anteriores.Select(o => o.Id).ToList();
        var aguardando = await _db.DocumentosAssinatura
            .Where(d => d.EntidadeTipo == OrdemServico.EntidadeTipoAssinatura && idsAnteriores.Contains(d.EntidadeId)
                && d.Status == StatusDocumentoAssinatura.EmAndamento)
            .Select(d => (Guid?)d.EntidadeId)
            .FirstOrDefaultAsync(ct);
        if (aguardando is not null)
        {
            var lista = await _mediator.Send(new ListarOrdensServicoQuery(request.TrabalhadorId), ct);
            return lista.First(o => o.Id == aguardando.Value);
        }

        var os = new OrdemServico
        {
            TrabalhadorId = request.TrabalhadorId,
            ObraId = obraId,
            Numero = await _numero.GerarAsync("OS", ct),
            Revisao = anteriores.Count == 0 ? 0 : anteriores.Max(o => o.Revisao) + 1,
            EmitidaEm = DateTime.UtcNow,
            ConteudoJson = JsonSerializer.Serialize(minuta),
        };
        _db.OrdensServico.Add(os);
        _db.DocumentosAssinatura.Add(new DocumentoAssinatura
        {
            EntidadeTipo = OrdemServico.EntidadeTipoAssinatura,
            EntidadeId = os.Id,
            ObraId = obraId,
        });

        await _auditoria.RegistrarAsync(
            "OrdemServico.Emitida",
            OrdemServico.EntidadeTipoAssinatura,
            os.Id,
            usuarioId: null,
            trabalhadorId: request.TrabalhadorId,
            dadosDepois: new { os.Numero, os.Revisao, ObraId = obraId },
            ct);
        await _db.SaveChangesAsync(ct);

        var emitidas = await _mediator.Send(new ListarOrdensServicoQuery(request.TrabalhadorId), ct);
        return emitidas.First(o => o.Id == os.Id);
    }
}

// Histórico das OS do trabalhador (todas as obras visíveis para quem consulta), da mais recente para a
// mais antiga. A mais recente de cada obra é a vigente.
public record ListarOrdensServicoQuery(Guid TrabalhadorId) : IRequest<List<OrdemServicoEmitidaDto>>;

public class ListarOrdensServicoQueryHandler : IRequestHandler<ListarOrdensServicoQuery, List<OrdemServicoEmitidaDto>>
{
    private readonly IAppDbContext _db;

    public ListarOrdensServicoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<OrdemServicoEmitidaDto>> Handle(ListarOrdensServicoQuery request, CancellationToken ct)
    {
        var ordens = await _db.OrdensServico
            .Where(o => o.TrabalhadorId == request.TrabalhadorId)
            .OrderByDescending(o => o.EmitidaEm)
            .ToListAsync(ct);
        var assinaturas = await AssinaturasOrdemServico.CarregarAsync(_db, ordens, ct);
        var vigentes = ordens.GroupBy(o => o.ObraId).Select(g => g.MaxBy(o => o.Revisao)!.Id).ToHashSet();

        return ordens.Select(o =>
        {
            var (documentoId, funcionario, tecnico) = assinaturas[o.Id];
            return new OrdemServicoEmitidaDto
            {
                Id = o.Id,
                Numero = o.Numero,
                Revisao = o.Revisao,
                EmitidaEm = o.EmitidaEm,
                ObraId = o.ObraId,
                DocumentoAssinaturaId = documentoId,
                Situacao = AssinaturasOrdemServico.Situacao(funcionario, tecnico),
                Vigente = vigentes.Contains(o.Id),
                Funcionario = funcionario,
                Tecnico = tecnico,
            };
        }).ToList();
    }
}
