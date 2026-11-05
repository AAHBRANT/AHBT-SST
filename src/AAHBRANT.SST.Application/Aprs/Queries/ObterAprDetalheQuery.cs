using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Aprs.Queries;

public record ObterAprDetalheQuery(Guid Id) : IRequest<AprDetalheDto?>;

public class ObterAprDetalheQueryHandler : IRequestHandler<ObterAprDetalheQuery, AprDetalheDto?>
{
    private readonly IAppDbContext _db;

    public ObterAprDetalheQueryHandler(IAppDbContext db) => _db = db;

    public async Task<AprDetalheDto?> Handle(ObterAprDetalheQuery request, CancellationToken ct)
    {
        var apr = await _db.Aprs
            .NoEscopoDaAtividade(_db, a => a.AtividadeId)
            .Include(a => a.Atividade!).ThenInclude(at => at.Obra)
            .Include(a => a.Equipe)
            .Include(a => a.AprovadoPorUsuario)
            .Include(a => a.Elaborador)
            .Include(a => a.Supervisor)
            .FirstOrDefaultAsync(a => a.Id == request.Id, ct);
        if (apr is null) return null;

        var etapas = await _db.AprEtapas
            .Where(e => e.AprId == apr.Id)
            .Include(e => e.Riscos)
            .OrderBy(e => e.Ordem)
            .ToListAsync(ct);

        var responsaveis = await _db.AprResponsaveis
            .Where(r => r.AprId == apr.Id)
            .Include(r => r.Trabalhador!).ThenInclude(t => t.Funcao)
            .ToListAsync(ct);

        var assinaturas = await _db.AprAssinaturas
            .Where(s => s.AprId == apr.Id)
            .Include(s => s.Trabalhador!).ThenInclude(t => t.Funcao)
            .OrderByDescending(s => s.DataAssinatura)
            .ToListAsync(ct);

        // 05/11: assinaturas biométricas do quiosque (Motor de Assinatura, documento "Apr") entram na mesma
        // lista, com o papel da APR — o PDF e a aba Assinaturas preferem elas ao registro antigo por clique.
        var biometricas = await _db.DocumentoSignatarios.AsNoTracking()
            .Where(x => x.DocumentoAssinatura!.EntidadeTipo == nameof(Apr) && x.DocumentoAssinatura.EntidadeId == apr.Id
                && x.DocumentoAssinatura.Status != StatusDocumentoAssinatura.Cancelado)
            .Select(x => new AprAssinaturaDto
            {
                Id = x.Id,
                AprId = apr.Id,
                TrabalhadorId = x.TrabalhadorId,
                TrabalhadorNome = x.Trabalhador != null ? x.Trabalhador.Nome : string.Empty,
                TrabalhadorFuncaoNome = x.Trabalhador != null && x.Trabalhador.Funcao != null ? x.Trabalhador.Funcao.Nome : null,
                Papel = x.Papel == PapelAssinatura.AprElaboracao ? PapelAssinaturaApr.Elaboracao
                    : x.Papel == PapelAssinatura.AprSupervisao ? PapelAssinaturaApr.Supervisao
                    : PapelAssinaturaApr.Envolvido,
                DataAssinatura = x.AssinadoEm,
                MetodoAutenticacao = x.MetodoAutenticacao,
                Biometrica = true,
            })
            .ToListAsync(ct);

        return new AprDetalheDto
        {
            Apr = new AprDto
            {
                Id = apr.Id,
                NumeroApr = apr.NumeroApr,
                AtividadeId = apr.AtividadeId,
                AtividadeNome = apr.Atividade?.Nome ?? string.Empty,
                ObraId = apr.Atividade?.Obra?.Id,
                ObraNome = apr.Atividade?.Obra?.Nome,
                Local = apr.Local,
                MaquinasEquipamentos = apr.MaquinasEquipamentos,
                PgrReferencia = apr.PgrReferencia,
                EquipeId = apr.EquipeId,
                EquipeNome = apr.Equipe?.Nome,
                Data = apr.Data,
                Validade = apr.Validade,
                Status = apr.Status,
                AprovadoPorUsuarioId = apr.AprovadoPorUsuarioId,
                AprovadoPorUsuarioNome = apr.AprovadoPorUsuario?.Nome,
                DataAprovacao = apr.DataAprovacao,
                MotivoReprovacao = apr.MotivoReprovacao,
                ElaboradorTrabalhadorId = apr.ElaboradorTrabalhadorId,
                ElaboradorNome = apr.Elaborador?.Nome,
                SupervisorTrabalhadorId = apr.SupervisorTrabalhadorId,
                SupervisorNome = apr.Supervisor?.Nome,
            },
            Etapas = etapas.Select(e => new AprEtapaDto
            {
                Id = e.Id,
                AprId = e.AprId,
                Ordem = e.Ordem,
                Descricao = e.Descricao,
                Riscos = e.Riscos.Select(r => new AprEtapaRiscoDto
                {
                    Id = r.Id,
                    AprEtapaId = r.AprEtapaId,
                    PerigoEventoPerigoso = r.PerigoEventoPerigoso,
                    FonteCircunstancia = r.FonteCircunstancia,
                    PossiveisLesoes = r.PossiveisLesoes,
                    TrabalhadoresExpostos = r.TrabalhadoresExpostos,
                    ProbabilidadeInicial = r.ProbabilidadeInicial,
                    SeveridadeInicial = r.SeveridadeInicial,
                    NivelRiscoInicial = r.NivelRiscoInicial,
                    MedidasPrevencao = r.MedidasPrevencao,
                    Responsavel = r.Responsavel,
                    ProbabilidadeResidual = r.ProbabilidadeResidual,
                    SeveridadeResidual = r.SeveridadeResidual,
                    NivelRiscoResidual = r.NivelRiscoResidual,
                }).ToList()
            }).ToList(),
            Responsaveis = responsaveis.Select(r => new AprResponsavelDto
            {
                Id = r.Id,
                AprId = r.AprId,
                TrabalhadorId = r.TrabalhadorId,
                TrabalhadorNome = r.Trabalhador?.Nome ?? string.Empty,
                TrabalhadorFuncaoNome = r.Trabalhador?.Funcao?.Nome
            }).ToList(),
            Assinaturas = assinaturas.Select(s => new AprAssinaturaDto
            {
                Id = s.Id,
                AprId = s.AprId,
                TrabalhadorId = s.TrabalhadorId,
                TrabalhadorNome = s.Trabalhador?.Nome ?? string.Empty,
                TrabalhadorFuncaoNome = s.Trabalhador?.Funcao?.Nome,
                Papel = s.Papel,
                DataAssinatura = s.DataAssinatura,
                MetodoAutenticacao = s.MetodoAutenticacao
            }).Concat(biometricas).OrderByDescending(s => s.DataAssinatura).ToList()
        };
    }
}
