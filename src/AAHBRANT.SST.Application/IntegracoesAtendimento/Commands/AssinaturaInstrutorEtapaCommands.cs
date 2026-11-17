using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.SessoesTreinamento;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.IntegracoesAtendimento.Commands;

// O instrutor assina, por digital OU facial, UMA VEZ em cada etapa de curso em andamento (decisão do usuário, 16/11/2026),
// junto dos colaboradores. Sem biometria cadastrada não há assinatura (não existe fallback pelo login). Exige o acesso à
// integração (Técnico/Admin com vínculo na obra; senão 404) e a etapa em andamento. A pessoa lida precisa ser o instrutor
// da integração. Devolve o atendimento atualizado.
public record AssinarInstrutorEtapaPorDigitalCommand(
    Guid IntegracaoId, Guid EtapaId, DadosAssinaturaInstrutorDigital Dados,
    string? AzureAdObjectId = null, bool AutenticacaoHabilitada = false) : IRequest<IntegracaoAtendimentoDto>;

public record AssinarInstrutorEtapaPorFacialCommand(
    Guid IntegracaoId, Guid EtapaId, DadosAssinaturaInstrutorFacial Dados,
    string? AzureAdObjectId = null, bool AutenticacaoHabilitada = false) : IRequest<IntegracaoAtendimentoDto>;

public class AssinarInstrutorEtapaPorDigitalCommandValidator : AbstractValidator<AssinarInstrutorEtapaPorDigitalCommand>
{
    public AssinarInstrutorEtapaPorDigitalCommandValidator()
    {
        RuleFor(x => x.IntegracaoId).NotEmpty();
        RuleFor(x => x.EtapaId).NotEmpty();
        RuleFor(x => x.Dados.DispositivoId).NotEmpty();
        RuleFor(x => x.Dados.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Dados.SegredoDispositivo).NotEmpty().When(x => string.IsNullOrEmpty(x.Dados.Desafio));
        RuleFor(x => x.Dados.AssinaturaLeitura).NotEmpty().When(x => !string.IsNullOrEmpty(x.Dados.Desafio));
        RuleFor(x => x.Dados.Score).InclusiveBetween(0, 100);
    }
}

public class AssinarInstrutorEtapaPorFacialCommandValidator : AbstractValidator<AssinarInstrutorEtapaPorFacialCommand>
{
    public AssinarInstrutorEtapaPorFacialCommandValidator()
    {
        RuleFor(x => x.IntegracaoId).NotEmpty();
        RuleFor(x => x.EtapaId).NotEmpty();
        RuleFor(x => x.Dados.FotoJpeg).NotEmpty();
    }
}

internal static class EtapaParaAssinaturaInstrutor
{
    // Autoriza (404 sem acesso) e devolve a turma da etapa de curso em andamento.
    public static async Task<SessaoTreinamento> CarregarSessaoAsync(
        IAppDbContext db, Guid integracaoId, Guid etapaId, string? azureAdObjectId, bool autenticacaoHabilitada, CancellationToken ct)
    {
        var (at, _) = await AutorizacaoIntegracaoAtendimento.CarregarAsync(db, integracaoId, azureAdObjectId, autenticacaoHabilitada, ct);
        var etapa = await db.IntegracaoEtapas.FirstOrDefaultAsync(e => e.Id == etapaId && e.IntegracaoAtendimentoId == at.Id, ct)
            ?? throw new KeyNotFoundException("Etapa não encontrada nesta integração.");
        if (etapa.Tipo == TipoEtapaIntegracao.OrdemServico)
            throw new InvalidOperationException("A etapa da Ordem de Serviço não tem assinatura de instrutor.");
        if (at.Status != StatusIntegracaoAtendimento.EmAndamento || etapa.Status != StatusEtapaIntegracao.EmAndamento || etapa.SessaoTreinamentoId is null)
            throw new InvalidOperationException("O instrutor só assina a etapa em andamento.");
        return await AssinaturaInstrutorTurma.CarregarParaAssinarAsync(db, etapa.SessaoTreinamentoId.Value, ct);
    }
}

public class AssinarInstrutorEtapaPorDigitalCommandHandler : IRequestHandler<AssinarInstrutorEtapaPorDigitalCommand, IntegracaoAtendimentoDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IClienteIpProvider? _clienteIp;

    public AssinarInstrutorEtapaPorDigitalCommandHandler(IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao, IClienteIpProvider? clienteIp = null)
    {
        _db = db;
        _autenticacao = autenticacao;
        _clienteIp = clienteIp;
    }

    public async Task<IntegracaoAtendimentoDto> Handle(AssinarInstrutorEtapaPorDigitalCommand request, CancellationToken ct)
    {
        var sessao = await EtapaParaAssinaturaInstrutor.CarregarSessaoAsync(
            _db, request.IntegracaoId, request.EtapaId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        await AssinaturaInstrutorTurma.PorDigitalAsync(_db, _autenticacao, _clienteIp, sessao, request.Dados, ct);
        return await MontadorIntegracaoAtendimento.MontarAsync(_db, request.IntegracaoId, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");
    }
}

public class AssinarInstrutorEtapaPorFacialCommandHandler : IRequestHandler<AssinarInstrutorEtapaPorFacialCommand, IntegracaoAtendimentoDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;
    private readonly IClienteIpProvider? _clienteIp;

    public AssinarInstrutorEtapaPorFacialCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial, IClienteIpProvider? clienteIp = null)
    {
        _db = db;
        _facial = facial;
        _clienteIp = clienteIp;
    }

    public async Task<IntegracaoAtendimentoDto> Handle(AssinarInstrutorEtapaPorFacialCommand request, CancellationToken ct)
    {
        var sessao = await EtapaParaAssinaturaInstrutor.CarregarSessaoAsync(
            _db, request.IntegracaoId, request.EtapaId, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);
        await AssinaturaInstrutorTurma.PorFacialAsync(_db, _facial, _clienteIp, sessao, request.Dados, ct);
        return await MontadorIntegracaoAtendimento.MontarAsync(_db, request.IntegracaoId, ct) ?? throw new KeyNotFoundException("Integração não encontrada.");
    }
}
