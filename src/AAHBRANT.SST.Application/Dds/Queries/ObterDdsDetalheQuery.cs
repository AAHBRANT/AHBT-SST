using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Dds.Queries;

public record ObterDdsDetalheQuery(Guid Id) : IRequest<DdsDetalheDto?>;

public class ObterDdsDetalheQueryHandler : IRequestHandler<ObterDdsDetalheQuery, DdsDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterDdsDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<DdsDetalheDto?> Handle(ObterDdsDetalheQuery request, CancellationToken ct)
    {
        var dds = await _db.Dds
            .Include(d => d.Obra)
            .Include(d => d.ResponsavelUsuario)
            .Include(d => d.Atividades).ThenInclude(a => a.Atividade)
            .FirstOrDefaultAsync(d => d.Id == request.Id, ct);
        if (dds is null) return null;

        var itens = await _db.DdsItensChecklist
            .Where(i => i.DdsId == dds.Id && i.Ativo)
            .ToListAsync(ct);

        var participantes = await _db.DdsParticipantes
            .Where(p => p.DdsId == dds.Id && p.Ativo)
            .Include(p => p.Trabalhador)
            .ToListAsync(ct);

        var fotosEvidencia = await _db.DdsFotosEvidencia
            .Where(f => f.DdsId == dds.Id && f.Ativo)
            .OrderBy(f => f.Ordem)
            .ToListAsync(ct);

        var selecionados = await _db.DdsFuncionariosSelecionados.AsNoTracking()
            .Where(s => s.DdsId == dds.Id)
            .OrderBy(s => s.Trabalhador!.Nome)
            .Select(s => new DdsFuncionarioDto(s.TrabalhadorId, s.Trabalhador!.Nome, s.Trabalhador.Matricula))
            .ToListAsync(ct);

        // Assinatura por trabalhador (04/09) — a presença biométrica já vale como assinatura (ver
        // RegistrarParticipanteCommand); busca aqui só pra exibir na lista de presença, sem duplicar
        // a lógica de quem pode assinar (isso continua no Motor de Assinatura).
        var assinadosEmPorTrabalhador = (await _db.DocumentoSignatarios
            .Where(s => s.DocumentoAssinatura!.EntidadeTipo == nameof(Domain.Entidades.Dds) && s.DocumentoAssinatura!.EntidadeId == dds.Id
                && s.Ativo && s.DocumentoAssinatura.Status != Domain.Enums.StatusDocumentoAssinatura.Cancelado)
            .Select(s => new { s.TrabalhadorId, s.AssinadoEm, s.MetodoAutenticacao })
            .ToListAsync(ct))
            .GroupBy(s => s.TrabalhadorId)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.AssinadoEm).Select(s => new { s.AssinadoEm, s.MetodoAutenticacao }).First());

        // 06/11: quem assinou pela tela "Assinar DDS" (Motor de Assinatura) não passava pela presença e
        // ficava fora da lista — e do PDF. Assinatura no DDS vale como presença; o responsável não entra.
        var trabalhadorResponsavelId = await _db.Usuarios.Where(u => u.Id == dds.ResponsavelUsuarioId)
            .Select(u => u.TrabalhadorId).FirstOrDefaultAsync(ct);
        var semPresenca = assinadosEmPorTrabalhador.Keys
            .Where(t => t != trabalhadorResponsavelId && participantes.All(p => p.TrabalhadorId != t)).ToList();
        if (semPresenca.Count > 0)
        {
            var trabalhadores = await _db.Trabalhadores.AsNoTracking().Where(t => semPresenca.Contains(t.Id)).ToListAsync(ct);
            participantes.AddRange(trabalhadores.Select(t => new Domain.Entidades.DdsParticipante
            {
                Id = t.Id, DdsId = dds.Id, TrabalhadorId = t.Id, Trabalhador = t,
                FotoTipo = Domain.Enums.TipoFotoParticipante.Biometria,
            }));
            participantes = participantes.OrderBy(p => p.Trabalhador?.Nome).ToList();
        }
        var selecionadosSemPresenca = selecionados.Where(s => participantes.All(p => p.TrabalhadorId != s.TrabalhadorId)
            && s.TrabalhadorId != trabalhadorResponsavelId).ToList();
        // 06/11: o técnico responsável assina na mesma fila, mas a assinatura dele é a do responsável — não
        // entra na lista de presença (nem no PDF, que já tem o bloco próprio do responsável).
        participantes = participantes.Where(p => p.TrabalhadorId != trabalhadorResponsavelId).ToList();
        assinadosEmPorTrabalhador.TryGetValue(trabalhadorResponsavelId ?? Guid.Empty, out var assinaturaResponsavel);

        dds.ItensChecklist = itens;
        dds.Participantes = participantes;
        dds.FotosEvidencia = fotosEvidencia;

        return new DdsDetalheDto
        {
            Dds = ListarDdsQueryHandler.MapearParaDto(dds),
            FuncionariosSelecionados = selecionadosSemPresenca,
            ResponsavelTrabalhadorId = trabalhadorResponsavelId,
            ResponsavelAssinadoEm = assinaturaResponsavel?.AssinadoEm,
            ResponsavelMetodoAssinatura = assinaturaResponsavel?.MetodoAutenticacao,
            ItensChecklist = itens.Select(i => new DdsItemChecklistDto
            {
                Id = i.Id,
                DdsId = i.DdsId,
                RiscoId = i.RiscoId,
                Descricao = i.Descricao,
                Verificado = i.Verificado,
            }).ToList(),
            Participantes = participantes.Select(p =>
            {
                var assinou = assinadosEmPorTrabalhador.TryGetValue(p.TrabalhadorId, out var assinatura);
                return new DdsParticipanteDto
                {
                    Id = p.Id,
                    TrabalhadorId = p.TrabalhadorId,
                    TrabalhadorNome = p.Trabalhador?.Nome ?? string.Empty,
                    FotoTipo = p.FotoTipo,
                    ScoreConfianca = p.ScoreConfianca,
                    AssinadoEm = assinou ? assinatura!.AssinadoEm : null,
                    MetodoAssinatura = assinou ? assinatura!.MetodoAutenticacao : null,
                };
            }).ToList(),
            FotosEvidencia = fotosEvidencia.Select(f => new DdsFotoEvidenciaDto { Id = f.Id, Ordem = f.Ordem, DadosFoto = AAHBRANT.SST.Application.Common.DadosCapturaFoto.Ler(f.FotoMetadadosJson) }).ToList(),
        };
    }
}
