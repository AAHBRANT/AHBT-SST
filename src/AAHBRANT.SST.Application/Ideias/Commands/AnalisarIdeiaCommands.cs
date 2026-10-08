using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Ideias.Commands;

// §5/§7 — complementa a estruturação e registra a análise. Nunca toca em MensagemOriginal (§3),
// status ou decisão (têm comandos próprios, com permissão própria).
public record AtualizarAnaliseIdeiaCommand(
    Guid IdeiaId,
    string Titulo,
    string Descricao,
    string? ProblemaOportunidade,
    string? Objetivo,
    string? SolucaoSugerida,
    string? Modulo,
    string? Submodulo,
    string? Categoria,
    string? BeneficioEsperado,
    string? PossiveisImpactos,
    string? IntegracoesNecessarias,
    bool? NecessidadeIa,
    string? Dependencias,
    string? InformacoesFaltantes,
    NivelIdeia? Impacto,
    NivelIdeia? Urgencia,
    NivelIdeia? Complexidade,
    NivelIdeia? Esforco,
    NivelIdeia? ValorNegocio,
    string? EsforcoEstimado,
    ViabilidadeIdeia ViabilidadeTecnica,
    ViabilidadeIdeia ViabilidadeOperacional,
    Guid? ResponsavelAnaliseUsuarioId,
    string? ResponsavelAnaliseNome,
    Guid? ResponsavelDesenvolvimentoUsuarioId,
    string? ResponsavelDesenvolvimentoNome,
    string? Observacoes,
    AutorIdeia Autor) : IRequest<IdeiaDetalheDto>;

public class AtualizarAnaliseIdeiaCommandValidator : AbstractValidator<AtualizarAnaliseIdeiaCommand>
{
    public AtualizarAnaliseIdeiaCommandValidator()
    {
        RuleFor(x => x.IdeiaId).NotEmpty();
        RuleFor(x => x.Titulo).NotEmpty().MaximumLength(180);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.ProblemaOportunidade).MaximumLength(2000);
        RuleFor(x => x.Objetivo).MaximumLength(2000);
        RuleFor(x => x.SolucaoSugerida).MaximumLength(2000);
        RuleFor(x => x.Modulo).MaximumLength(120);
        RuleFor(x => x.Submodulo).MaximumLength(120);
        RuleFor(x => x.Categoria).MaximumLength(120);
        RuleFor(x => x.BeneficioEsperado).MaximumLength(2000);
        RuleFor(x => x.PossiveisImpactos).MaximumLength(2000);
        RuleFor(x => x.IntegracoesNecessarias).MaximumLength(1000);
        RuleFor(x => x.Dependencias).MaximumLength(2000);
        RuleFor(x => x.InformacoesFaltantes).MaximumLength(2000);
        RuleFor(x => x.EsforcoEstimado).MaximumLength(120);
        RuleFor(x => x.ResponsavelAnaliseNome).MaximumLength(160);
        RuleFor(x => x.ResponsavelDesenvolvimentoNome).MaximumLength(160);
        RuleFor(x => x.Observacoes).MaximumLength(4000);
    }
}

public class AtualizarAnaliseIdeiaCommandHandler : IRequestHandler<AtualizarAnaliseIdeiaCommand, IdeiaDetalheDto>
{
    private readonly IAppDbContext _db;

    public AtualizarAnaliseIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<IdeiaDetalheDto> Handle(AtualizarAnaliseIdeiaCommand r, CancellationToken ct)
    {
        var i = await _db.Ideias.FirstOrDefaultAsync(x => x.Id == r.IdeiaId, ct)
            ?? throw new KeyNotFoundException($"Ideia {r.IdeiaId} não encontrada.");

        if (i.Status is StatusIdeia.Implantada or StatusIdeia.Descartada)
            throw new InvalidOperationException("Ideia implantada ou descartada não pode mais ser alterada.");

        i.Titulo = r.Titulo.Trim();
        i.Descricao = r.Descricao.Trim();
        i.ProblemaOportunidade = IdeiaMapeador.Limpar(r.ProblemaOportunidade, 2000);
        i.Objetivo = IdeiaMapeador.Limpar(r.Objetivo, 2000);
        i.SolucaoSugerida = IdeiaMapeador.Limpar(r.SolucaoSugerida, 2000);
        i.Modulo = IdeiaMapeador.Limpar(r.Modulo, 120);
        i.Submodulo = IdeiaMapeador.Limpar(r.Submodulo, 120);
        i.Categoria = IdeiaMapeador.Limpar(r.Categoria, 120);
        i.BeneficioEsperado = IdeiaMapeador.Limpar(r.BeneficioEsperado, 2000);
        i.PossiveisImpactos = IdeiaMapeador.Limpar(r.PossiveisImpactos, 2000);
        i.IntegracoesNecessarias = IdeiaMapeador.Limpar(r.IntegracoesNecessarias, 1000);
        i.NecessidadeIa = r.NecessidadeIa;
        i.Dependencias = IdeiaMapeador.Limpar(r.Dependencias, 2000);
        i.InformacoesFaltantes = IdeiaMapeador.Limpar(r.InformacoesFaltantes, 2000);
        i.Impacto = r.Impacto;
        i.Urgencia = r.Urgencia;
        i.Complexidade = r.Complexidade;
        i.Esforco = r.Esforco;
        i.ValorNegocio = r.ValorNegocio;
        i.EsforcoEstimado = IdeiaMapeador.Limpar(r.EsforcoEstimado, 120);
        i.ViabilidadeTecnica = r.ViabilidadeTecnica;
        i.ViabilidadeOperacional = r.ViabilidadeOperacional;
        i.ResponsavelAnaliseUsuarioId = r.ResponsavelAnaliseUsuarioId;
        i.ResponsavelAnaliseNome = IdeiaMapeador.Limpar(r.ResponsavelAnaliseNome, 160);
        i.ResponsavelDesenvolvimentoUsuarioId = r.ResponsavelDesenvolvimentoUsuarioId;
        i.ResponsavelDesenvolvimentoNome = IdeiaMapeador.Limpar(r.ResponsavelDesenvolvimentoNome, 160);
        i.Observacoes = IdeiaMapeador.Limpar(r.Observacoes, 4000);

        // §9 — a pontuação só apoia a decisão; recalculada a cada edição.
        i.Pontuacao = PontuacaoIdeia.Calcular(i.Impacto, i.Urgencia, i.ValorNegocio, i.Esforco, i.Complexidade);

        IdeiaMapeador.Historico(_db.IdeiaHistoricos, i, TipoHistoricoIdeia.Analise,
            $"{r.Autor.Nome} atualizou a análise da ideia.", r.Autor);
        await _db.SaveChangesAsync(ct);

        return await IdeiaDetalheLoader.CarregarAsync(_db, i.Id, ct);
    }
}

// §6 — transição de status. A permissão por destino é checada no controller (FluxoStatusIdeia.PermissaoExigida).
public record AlterarStatusIdeiaCommand(
    Guid IdeiaId,
    StatusIdeia Destino,
    string? Justificativa,
    PrioridadeIdeia? Prioridade,
    AutorIdeia Autor) : IRequest<IdeiaDetalheDto>;

public class AlterarStatusIdeiaCommandValidator : AbstractValidator<AlterarStatusIdeiaCommand>
{
    public AlterarStatusIdeiaCommandValidator()
    {
        RuleFor(x => x.IdeiaId).NotEmpty();
        RuleFor(x => x.Destino).IsInEnum();
        RuleFor(x => x.Justificativa).MaximumLength(2000);
    }
}

public class AlterarStatusIdeiaCommandHandler : IRequestHandler<AlterarStatusIdeiaCommand, IdeiaDetalheDto>
{
    private readonly IAppDbContext _db;

    public AlterarStatusIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<IdeiaDetalheDto> Handle(AlterarStatusIdeiaCommand r, CancellationToken ct)
    {
        var i = await _db.Ideias.FirstOrDefaultAsync(x => x.Id == r.IdeiaId, ct)
            ?? throw new KeyNotFoundException($"Ideia {r.IdeiaId} não encontrada.");

        if (!FluxoStatusIdeia.Permite(i.Status, r.Destino))
            throw new InvalidOperationException($"Não é possível mover a ideia de {Rotulo(i.Status)} para {Rotulo(r.Destino)}.");

        var justificativa = IdeiaMapeador.Limpar(r.Justificativa, 2000);
        if (FluxoStatusIdeia.ExigeJustificativa(r.Destino) && justificativa is null)
            throw new InvalidOperationException("Informe a justificativa para adiar ou descartar a ideia.");

        if (r.Destino == StatusIdeia.Priorizada)
        {
            if (r.Prioridade is not null) i.Prioridade = r.Prioridade;
            if (i.Prioridade is null)
                throw new InvalidOperationException("Defina a prioridade (P1, P2 ou P3) para priorizar a ideia.");
        }
        else if (r.Prioridade is not null)
        {
            i.Prioridade = r.Prioridade;
        }

        var anterior = i.Status;
        var agora = DateTime.UtcNow;
        i.Status = r.Destino;

        switch (r.Destino)
        {
            case StatusIdeia.Aprovada:
                i.Decisao = DecisaoIdeia.Aprovada;
                i.DecididoPorNome = r.Autor.Nome;
                i.DataAprovacaoUtc = agora;
                if (justificativa is not null) i.Justificativa = justificativa;
                break;
            case StatusIdeia.Adiada:
                i.Decisao = DecisaoIdeia.Adiada;
                i.DecididoPorNome = r.Autor.Nome;
                i.Justificativa = justificativa;
                break;
            case StatusIdeia.Descartada:
                i.Decisao = DecisaoIdeia.Reprovada;
                i.DecididoPorNome = r.Autor.Nome;
                i.Justificativa = justificativa;
                break;
            case StatusIdeia.EmAnalise when anterior is StatusIdeia.Adiada or StatusIdeia.Descartada:
                // Reabertura: a decisão anterior deixa de valer (fica no histórico).
                i.Decisao = null;
                break;
            case StatusIdeia.EmDesenvolvimento:
                i.DataInicioUtc ??= agora;
                break;
            case StatusIdeia.EmTesteValidacao:
                i.DataConclusaoUtc = agora;
                break;
            case StatusIdeia.Implantada:
                i.DataImplantacaoUtc = agora;
                break;
        }

        var tipo = r.Destino is StatusIdeia.Aprovada or StatusIdeia.Adiada or StatusIdeia.Descartada
            ? TipoHistoricoIdeia.Decisao : TipoHistoricoIdeia.MudancaStatus;
        var texto = $"{r.Autor.Nome} alterou o status de {Rotulo(anterior)} para {Rotulo(r.Destino)}.";
        if (justificativa is not null) texto += $" Justificativa: {justificativa}";
        IdeiaMapeador.Historico(_db.IdeiaHistoricos, i, tipo, texto, r.Autor);

        await _db.SaveChangesAsync(ct);
        return await IdeiaDetalheLoader.CarregarAsync(_db, i.Id, ct);
    }

    internal static string Rotulo(StatusIdeia s) => s switch
    {
        StatusIdeia.NovaIdeia => "Nova ideia",
        StatusIdeia.EmAnalise => "Em análise",
        StatusIdeia.AguardandoDecisao => "Aguardando decisão",
        StatusIdeia.Aprovada => "Aprovada",
        StatusIdeia.Priorizada => "Priorizada",
        StatusIdeia.EmDesenvolvimento => "Em desenvolvimento",
        StatusIdeia.EmTesteValidacao => "Em teste/validação",
        StatusIdeia.Implantada => "Implantada",
        StatusIdeia.Adiada => "Adiada",
        StatusIdeia.Descartada => "Descartada",
        _ => s.ToString()
    };
}

// §12 — discussão da ideia.
public record ComentarIdeiaCommand(Guid IdeiaId, string Texto, AutorIdeia Autor) : IRequest<IdeiaComentarioDto>;

public class ComentarIdeiaCommandValidator : AbstractValidator<ComentarIdeiaCommand>
{
    public ComentarIdeiaCommandValidator()
    {
        RuleFor(x => x.IdeiaId).NotEmpty();
        RuleFor(x => x.Texto).NotEmpty().WithMessage("Escreva o comentário.").MaximumLength(4000);
    }
}

public class ComentarIdeiaCommandHandler : IRequestHandler<ComentarIdeiaCommand, IdeiaComentarioDto>
{
    private readonly IAppDbContext _db;

    public ComentarIdeiaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<IdeiaComentarioDto> Handle(ComentarIdeiaCommand r, CancellationToken ct)
    {
        if (!await _db.Ideias.AnyAsync(x => x.Id == r.IdeiaId, ct))
            throw new KeyNotFoundException($"Ideia {r.IdeiaId} não encontrada.");

        var comentario = new IdeiaComentario
        {
            IdeiaId = r.IdeiaId,
            AutorUsuarioId = r.Autor.UsuarioId,
            AutorNome = string.IsNullOrWhiteSpace(r.Autor.Nome) ? "Usuário" : r.Autor.Nome,
            Texto = r.Texto.Trim()
        };
        _db.IdeiaComentarios.Add(comentario);
        await _db.SaveChangesAsync(ct);

        return new IdeiaComentarioDto
        {
            Id = comentario.Id, AutorNome = comentario.AutorNome, Texto = comentario.Texto,
            CreatedAtUtc = comentario.CreatedAtUtc
        };
    }
}
