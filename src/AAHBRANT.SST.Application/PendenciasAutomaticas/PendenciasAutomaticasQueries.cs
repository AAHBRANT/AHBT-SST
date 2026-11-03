using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.PendenciasAutomaticas;

public record ConfiguracaoPendenciaDto(TipoPendenciaAutomatica Tipo, bool Ligada, FrequenciaPendenciaAutomatica Frequencia, bool PodeFechar);
public record TrabalhadorPendenteDto(Guid TrabalhadorId, string Nome, string Funcao, string Falta);
public record ObraPendenciaDto(Guid ObraId, string ObraNome, int TotalTrabalhadores, IReadOnlyList<TrabalhadorPendenteDto> Pendentes);
public record PendenciaAutomaticaDto(ConfiguracaoPendenciaDto Configuracao, IReadOnlyList<ObraPendenciaDto> Obras);

public static class PadraoPendenciaAutomatica
{
    public static readonly TipoPendenciaAutomatica[] Tipos =
        { TipoPendenciaAutomatica.RecadastroFacial, TipoPendenciaAutomatica.DigitaisIncompletas };

    // Tipo sem linha na tabela vale o padrão: ligada, toda vez que entrar, pode fechar.
    public static async Task<List<ConfiguracaoPendenciaDto>> ListarConfiguracoesAsync(IAppDbContext db, CancellationToken ct)
    {
        var salvas = await db.ConfiguracoesPendenciaAutomatica.ToListAsync(ct);
        return Tipos.Select(tipo => salvas.FirstOrDefault(c => c.Tipo == tipo) is { } c
                ? new ConfiguracaoPendenciaDto(tipo, c.Ligada, c.Frequencia, c.PodeFechar)
                : new ConfiguracaoPendenciaDto(tipo, true, FrequenciaPendenciaAutomatica.TodaEntrada, true))
            .ToList();
    }

    // Regra das digitais (03/11): 2 por funcionário, uma de cada mão. A digital antiga, cadastrada
    // antes de existir o dedo, conta como uma das duas até ser confirmada. Null = completo.
    public static string? DigitalFaltante(IReadOnlyCollection<DedoBiometria> dedos)
    {
        var direita = dedos.Contains(DedoBiometria.PolegarDireito);
        var esquerda = dedos.Contains(DedoBiometria.PolegarEsquerdo);
        var antiga = dedos.Contains(DedoBiometria.NaoInformado);
        var maos = (direita ? 1 : 0) + (esquerda ? 1 : 0);
        if (maos == 2 || (maos == 1 && antiga)) return null;
        if (direita) return "Falta: mão esquerda";
        if (esquerda) return "Falta: mão direita";
        if (antiga) return "1 digital antiga sem mão informada · falta a outra mão";
        return "Falta: as duas mãos";
    }
}

// Pop-up de pendências automáticas (pedido do usuário, 03/11). Recebem Técnico de Segurança e
// Administrador (mesmo critério do pop-up "Funcionário novo"); o escopo por obra vem do filtro global
// de Trabalhador — o técnico só vê as obras dele, o Admin vê todas. Só funcionários com situação Ativo.
public record ListarPendenciasAutomaticasQuery(string? AzureAdObjectId, bool AutenticacaoHabilitada) : IRequest<List<PendenciaAutomaticaDto>>;

public class ListarPendenciasAutomaticasQueryHandler : IRequestHandler<ListarPendenciasAutomaticasQuery, List<PendenciaAutomaticaDto>>
{
    private readonly IAppDbContext _db;
    public ListarPendenciasAutomaticasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<PendenciaAutomaticaDto>> Handle(ListarPendenciasAutomaticasQuery request, CancellationToken ct)
    {
        if (!await PerfilTecnicoSeguranca.PodeEditarDadosTreinamentoAsync(_db, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct))
            return new List<PendenciaAutomaticaDto>();

        var ligadas = (await PadraoPendenciaAutomatica.ListarConfiguracoesAsync(_db, ct)).Where(c => c.Ligada).ToList();
        if (ligadas.Count == 0) return new List<PendenciaAutomaticaDto>();

        var situacao = await SituacaoBiometricaAsync(_db, ct);
        var resultado = new List<PendenciaAutomaticaDto>();
        foreach (var config in ligadas)
        {
            var obras = situacao
                .GroupBy(t => new { t.ObraId, t.ObraNome })
                .Select(obra =>
                {
                    var pendentes = obra
                        .Select(t => new { t, Falta = t.Falta(config.Tipo) })
                        .Where(x => x.Falta is not null)
                        .OrderBy(x => x.t.Nome)
                        .Select(x => new TrabalhadorPendenteDto(x.t.Id, x.t.Nome, x.t.Funcao, x.Falta!))
                        .ToList();
                    return new ObraPendenciaDto(obra.Key.ObraId, obra.Key.ObraNome, obra.Count(), pendentes);
                })
                .Where(o => o.Pendentes.Count > 0)
                .OrderBy(o => o.ObraNome)
                .ToList();
            if (obras.Count > 0) resultado.Add(new PendenciaAutomaticaDto(config, obras));
        }
        return resultado;
    }

    internal record SituacaoBiometrica(Guid Id, string Nome, Guid ObraId, string ObraNome, string Funcao,
        bool TemFacial, IReadOnlyCollection<DedoBiometria> Dedos)
    {
        public string? Falta(TipoPendenciaAutomatica tipo) => tipo == TipoPendenciaAutomatica.RecadastroFacial
            ? (TemFacial ? null : "Sem cadastro facial")
            : PadraoPendenciaAutomatica.DigitalFaltante(Dedos);
    }

    // Uma consulta para trabalhadores (sem a foto de perfil), uma para digitais e uma para facial.
    internal static async Task<List<SituacaoBiometrica>> SituacaoBiometricaAsync(IAppDbContext db, CancellationToken ct)
    {
        var trabalhadores = await db.Trabalhadores
            .Where(t => t.Situacao == Domain.Enums.SituacaoTrabalhador.Ativo)
            .Select(t => new { t.Id, t.Nome, t.ObraId, ObraNome = t.Obra!.Nome, Funcao = t.Funcao != null ? t.Funcao.Nome : "" })
            .ToListAsync(ct);
        if (trabalhadores.Count == 0) return new List<SituacaoBiometrica>();

        var ids = trabalhadores.Select(t => t.Id).ToList();
        var dedos = (await db.TemplatesBiometricoFutronic
                .Where(b => ids.Contains(b.TrabalhadorId))
                .Select(b => new { b.TrabalhadorId, b.Dedo })
                .ToListAsync(ct))
            .GroupBy(b => b.TrabalhadorId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<DedoBiometria>)g.Select(b => b.Dedo).ToList());
        var comFacial = (await db.FotosCadastroFacial
                .Where(f => ids.Contains(f.TrabalhadorId))
                .Select(f => f.TrabalhadorId)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        return trabalhadores.Select(t => new SituacaoBiometrica(t.Id, t.Nome, t.ObraId, t.ObraNome, t.Funcao,
                comFacial.Contains(t.Id), dedos.GetValueOrDefault(t.Id) ?? Array.Empty<DedoBiometria>()))
            .ToList();
    }
}

// Tela de configuração (só Administrador): cada tipo com o total pendente de hoje.
public record ConfiguracaoPendenciaComTotalDto(TipoPendenciaAutomatica Tipo, bool Ligada, FrequenciaPendenciaAutomatica Frequencia, bool PodeFechar, int TotalPendentes);
public record ListarConfiguracoesPendenciaQuery : IRequest<List<ConfiguracaoPendenciaComTotalDto>>;

public class ListarConfiguracoesPendenciaQueryHandler : IRequestHandler<ListarConfiguracoesPendenciaQuery, List<ConfiguracaoPendenciaComTotalDto>>
{
    private readonly IAppDbContext _db;
    public ListarConfiguracoesPendenciaQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ConfiguracaoPendenciaComTotalDto>> Handle(ListarConfiguracoesPendenciaQuery request, CancellationToken ct)
    {
        var configuracoes = await PadraoPendenciaAutomatica.ListarConfiguracoesAsync(_db, ct);
        var situacao = await ListarPendenciasAutomaticasQueryHandler.SituacaoBiometricaAsync(_db, ct);
        return configuracoes.Select(c => new ConfiguracaoPendenciaComTotalDto(c.Tipo, c.Ligada, c.Frequencia, c.PodeFechar,
            situacao.Count(t => t.Falta(c.Tipo) is not null))).ToList();
    }
}

public record SalvarConfiguracaoPendenciaCommand(TipoPendenciaAutomatica Tipo, bool Ligada, FrequenciaPendenciaAutomatica Frequencia, bool PodeFechar) : IRequest;

public class SalvarConfiguracaoPendenciaCommandHandler : IRequestHandler<SalvarConfiguracaoPendenciaCommand>
{
    private readonly IAppDbContext _db;
    public SalvarConfiguracaoPendenciaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(SalvarConfiguracaoPendenciaCommand request, CancellationToken ct)
    {
        if (!PadraoPendenciaAutomatica.Tipos.Contains(request.Tipo))
            throw new InvalidOperationException("Tipo de pendência desconhecido.");
        if (!Enum.IsDefined(request.Frequencia))
            throw new InvalidOperationException("Frequência inválida.");

        var config = await _db.ConfiguracoesPendenciaAutomatica.FirstOrDefaultAsync(c => c.Tipo == request.Tipo, ct);
        if (config is null)
        {
            config = new ConfiguracaoPendenciaAutomatica { Tipo = request.Tipo };
            _db.ConfiguracoesPendenciaAutomatica.Add(config);
        }
        config.Ligada = request.Ligada;
        config.Frequencia = request.Frequencia;
        config.PodeFechar = request.PodeFechar;
        await _db.SaveChangesAsync(ct);
    }
}
