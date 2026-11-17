using AAHBRANT.SST.Application.Acidentes;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Acidentes.Commands;

public record AtualizarAcidenteCommand(
    Guid Id,
    TipoOcorrencia Tipo,
    Guid ObraId,
    Guid? TrabalhadorId,
    Guid? AtividadeId,
    string Local,
    DateTime Data,
    TimeSpan? Hora,
    string Descricao,
    string? Lesao,
    string? Consequencia,
    string? Atendimento,
    bool HouveAfastamento,
    int? DiasAfastamento,
    string? NumeroCat,
    MetodologiaInvestigacao? MetodologiaInvestigacao,
    string? Causas,
    GravidadeAcidente Gravidade,
    int? DiasDebitadosInformados,
    List<Guid>? TrabalhadoresIds = null,
    // null = mantém as partes do corpo já gravadas (telas que salvam outros dados não mandam o mapa);
    // lista vazia = limpa.
    List<string>? RegioesCorpo = null,
    // null = mantém as pessoas externas já gravadas; lista (mesmo vazia) substitui o conjunto.
    List<PessoaExternaEntrada>? PessoasExternas = null,
    // null = mantém as testemunhas já gravadas; lista (mesmo vazia) substitui.
    List<Guid>? Testemunhas = null) : IRequest;

public class AtualizarAcidenteCommandValidator : AbstractValidator<AtualizarAcidenteCommand>
{
    public AtualizarAcidenteCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Local).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Lesao).MaximumLength(500);
        RuleFor(x => x.Consequencia).MaximumLength(500);
        RuleFor(x => x.Atendimento).MaximumLength(500);
        RuleFor(x => x.NumeroCat).MaximumLength(50);
        RuleFor(x => x.Causas).MaximumLength(2000);
        RuleFor(x => x.DiasAfastamento).GreaterThanOrEqualTo(0).When(x => x.DiasAfastamento.HasValue);
        RuleFor(x => x.DiasDebitadosInformados)
            .NotNull().WithMessage("Informe os Dias Debitados consultando o Quadro III da NBR 14280.")
            .GreaterThan(0)
            .When(x => x.Gravidade == GravidadeAcidente.IncapacidadePermanenteParcial);
        RuleFor(x => x.RegioesCorpo!.Count).LessThanOrEqualTo(RegioesCorpoAcidente.MaximoRegioes)
            .When(x => x.RegioesCorpo is not null);
        RuleForEach(x => x.RegioesCorpo).Must(RegioesCorpoAcidente.CodigoValido).WithMessage("Parte do corpo inválida: {PropertyValue}.");
        RuleFor(x => x.PessoasExternas!.Count).LessThanOrEqualTo(AcidentePessoasExternasSync.MaximoPessoas)
            .When(x => x.PessoasExternas is not null);
        RuleForEach(x => x.PessoasExternas).SetValidator(new PessoaExternaEntradaValidator());
    }
}

public class AtualizarAcidenteCommandHandler : IRequestHandler<AtualizarAcidenteCommand>
{
    private readonly IAppDbContext _db;
    private readonly IPublicadorAcidenteGrh _publicadorGrh;

    public AtualizarAcidenteCommandHandler(IAppDbContext db, IPublicadorAcidenteGrh publicadorGrh)
    {
        _db = db;
        _publicadorGrh = publicadorGrh;
    }

    public async Task Handle(AtualizarAcidenteCommand request, CancellationToken ct)
    {
        var acidente = await _db.Acidentes.FirstOrDefaultAsync(a => a.Id == request.Id, ct)
            ?? throw new KeyNotFoundException($"Acidente {request.Id} não encontrado.");

        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var envolvidos = AcidenteEnvolvidosSync.Resolver(request.TrabalhadoresIds, request.TrabalhadorId);
        var testemunhas = AcidenteEnvolvidosSync.ResolverTestemunhas(request.Testemunhas);
        AcidenteEnvolvidosSync.ValidarSemSobreposicao(envolvidos, testemunhas);
        await AcidenteEnvolvidosSync.ValidarAsync(_db, envolvidos.Concat(testemunhas ?? new()).ToList(), ct);

        if (request.AtividadeId.HasValue &&
            !await _db.Atividades.AnyAsync(a => a.Id == request.AtividadeId, ct))
            throw new KeyNotFoundException($"Atividade {request.AtividadeId} não encontrada.");

        // Decisão provisória: o tipo "com pessoa externa" só se define na criação. Trocar de/para ele deixaria
        // o funcionário como vítima no G-RH (ou o tipo 7 sem pessoa externa); revisar com o G-RH.
        if (acidente.Tipo != request.Tipo
            && (acidente.Tipo == TipoOcorrencia.OcorrenciaComPessoaExterna || request.Tipo == TipoOcorrencia.OcorrenciaComPessoaExterna))
            throw new InvalidOperationException(
                "O tipo \"Ocorrência com pessoa externa\" só pode ser escolhido ao registrar a ocorrência; não é possível trocar para ele nem a partir dele. Registre uma nova ocorrência.");

        acidente.Tipo = request.Tipo;
        acidente.ObraId = request.ObraId;
        await AcidenteEnvolvidosSync.SincronizarAsync(_db, acidente, envolvidos, ct, testemunhas);
        var totalPessoasExternas = await AcidentePessoasExternasSync.SincronizarAsync(
            _db, acidente, request.PessoasExternas is null ? null : AcidentePessoasExternasSync.Resolver(request.PessoasExternas), ct);
        AcidentePessoasExternasSync.ValidarRegraDoTipo(request.Tipo, totalPessoasExternas, envolvidos.Count);
        acidente.AtividadeId = request.AtividadeId;
        acidente.Local = request.Local;
        acidente.Data = request.Data;
        acidente.Hora = request.Hora;
        acidente.Descricao = request.Descricao;
        acidente.Lesao = request.Lesao;
        if (request.RegioesCorpo is not null)
            acidente.RegioesCorpo = RegioesCorpoAcidente.Gravar(request.RegioesCorpo);
        acidente.Consequencia = request.Consequencia;
        acidente.Atendimento = request.Atendimento;
        acidente.HouveAfastamento = request.HouveAfastamento;
        acidente.DiasAfastamento = request.DiasAfastamento;
        acidente.NumeroCat = request.NumeroCat;
        acidente.MetodologiaInvestigacao = request.MetodologiaInvestigacao;
        acidente.Causas = request.Causas;
        acidente.Gravidade = request.Gravidade;
        acidente.DiasDebitados = TabelaDiasDebitados.Calcular(request.Gravidade, request.DiasDebitadosInformados);
        AcidentePessoasExternasSync.AplicarPadraoDoTipo(acidente);

        await _db.SaveChangesAsync(ct);
        await AcidenteGrhEventoFactory.PublicarAsync(_db, _publicadorGrh, acidente, ct);
    }
}
