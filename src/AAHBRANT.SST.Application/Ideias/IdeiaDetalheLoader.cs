using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ideias;

internal static class IdeiaDetalheLoader
{
    public static async Task<IdeiaDetalheDto> CarregarAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var i = await db.Ideias
            .Include(x => x.IdeiaPrincipal)
            .Include(x => x.IdeiaSemelhante)
            .Include(x => x.Comentarios)
            .Include(x => x.Historico)
            .Include(x => x.Requisitos).ThenInclude(r => r.Demandas)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Ideia {id} não encontrada.");

        // Anexos: só metadados — o conteúdo (bytes) é baixado à parte.
        var anexos = await db.IdeiaAnexos
            .Where(a => a.IdeiaId == id)
            .OrderBy(a => a.CreatedAtUtc)
            .Select(a => new IdeiaAnexoDto
            {
                Id = a.Id, NomeArquivo = a.NomeArquivo, ContentType = a.ContentType, Tamanho = a.Tamanho,
                EnviadoPorNome = a.EnviadoPorNome, CreatedAtUtc = a.CreatedAtUtc
            })
            .ToListAsync(ct);

        var vinculadas = await db.Ideias
            .Where(x => x.IdeiaPrincipalId == id)
            .OrderBy(x => x.Numero)
            .ToListAsync(ct);

        var dto = new IdeiaDetalheDto
        {
            Id = i.Id, Codigo = i.Codigo, Titulo = i.Titulo, Status = i.Status, Modulo = i.Modulo,
            Categoria = i.Categoria, Prioridade = i.Prioridade, Pontuacao = i.Pontuacao, Canal = i.Canal,
            RegistradoPorNome = i.RegistradoPorNome ?? i.TelegramUsuarioNome,
            ResponsavelAnaliseNome = i.ResponsavelAnaliseNome,
            ResponsavelDesenvolvimentoNome = i.ResponsavelDesenvolvimentoNome,
            CreatedAtUtc = i.CreatedAtUtc, IdeiaPrincipalId = i.IdeiaPrincipalId,
            IdeiaPrincipalCodigo = i.IdeiaPrincipal?.Codigo,
            MensagemOriginal = i.MensagemOriginal, Descricao = i.Descricao,
            ProblemaOportunidade = i.ProblemaOportunidade, Objetivo = i.Objetivo,
            SolucaoSugerida = i.SolucaoSugerida, Submodulo = i.Submodulo,
            BeneficioEsperado = i.BeneficioEsperado, PossiveisImpactos = i.PossiveisImpactos,
            IntegracoesNecessarias = i.IntegracoesNecessarias, NecessidadeIa = i.NecessidadeIa,
            Dependencias = i.Dependencias, InformacoesFaltantes = i.InformacoesFaltantes,
            EstruturadoPor = i.EstruturadoPor,
            Impacto = i.Impacto, Urgencia = i.Urgencia, Complexidade = i.Complexidade, Esforco = i.Esforco,
            ValorNegocio = i.ValorNegocio, EsforcoEstimado = i.EsforcoEstimado,
            ViabilidadeTecnica = i.ViabilidadeTecnica, ViabilidadeOperacional = i.ViabilidadeOperacional,
            PrioridadeSugerida = PontuacaoIdeia.SugerirPrioridade(i.Pontuacao),
            Decisao = i.Decisao, Justificativa = i.Justificativa, DecididoPorNome = i.DecididoPorNome,
            DataAprovacaoUtc = i.DataAprovacaoUtc, DataInicioUtc = i.DataInicioUtc,
            DataConclusaoUtc = i.DataConclusaoUtc, DataImplantacaoUtc = i.DataImplantacaoUtc,
            Observacoes = i.Observacoes,
            IdeiaSemelhanteId = i.IdeiaSemelhanteId, IdeiaSemelhanteCodigo = i.IdeiaSemelhante?.Codigo,
            TelegramUsuarioNome = i.TelegramUsuarioNome,
            ProximosStatus = FluxoStatusIdeia.Destinos(i.Status).ToList(),
            Anexos = anexos,
            Comentarios = i.Comentarios.OrderBy(c => c.CreatedAtUtc).Select(c => new IdeiaComentarioDto
            {
                Id = c.Id, AutorNome = c.AutorNome, Texto = c.Texto, CreatedAtUtc = c.CreatedAtUtc
            }).ToList(),
            Historico = i.Historico.OrderBy(h => h.OcorridoEmUtc).Select(h => new IdeiaHistoricoDto
            {
                Id = h.Id, Tipo = h.Tipo, Descricao = h.Descricao, AutorNome = h.AutorNome, OcorridoEmUtc = h.OcorridoEmUtc
            }).ToList(),
            Requisitos = i.Requisitos.OrderBy(r => r.CreatedAtUtc).Select(r => new IdeiaRequisitoDto
            {
                Id = r.Id, Titulo = r.Titulo, Descricao = r.Descricao, CriteriosAceite = r.CriteriosAceite,
                Status = r.Status, AprovadoEmUtc = r.AprovadoEmUtc, AprovadoPorNome = r.AprovadoPorNome,
                Demandas = r.Demandas.Where(d => d.Ativo).OrderBy(d => d.CreatedAtUtc).Select(Demanda).ToList()
            }).ToList(),
            IdeiasVinculadas = vinculadas.Select(IdeiaMapeador.ParaResumo).ToList()
        };
        return dto;
    }

    public static DemandaDesenvolvimentoDto Demanda(DemandaDesenvolvimento d) => new()
    {
        Id = d.Id, Codigo = d.Codigo, IdeiaId = d.IdeiaId, RequisitoId = d.RequisitoId, Titulo = d.Titulo,
        Descricao = d.Descricao, Status = d.Status, ResponsavelNome = d.ResponsavelNome,
        FuncionalidadeEntregue = d.FuncionalidadeEntregue, ConcluidaEmUtc = d.ConcluidaEmUtc
    };
}
