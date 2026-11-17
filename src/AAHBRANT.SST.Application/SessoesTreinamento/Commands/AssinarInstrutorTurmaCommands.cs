using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using FluentValidation;
using MediatR;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Turma comum (fora da integração): o instrutor assina por digital ou facial ANTES de encerrar; o encerramento fica bloqueado
// até lá (decisão do usuário, 16/11/2026). Também aceita a turma de uma etapa de integração em andamento (o controller de
// turmas já confere o acesso por obra com GarantirAcessoTurmaIntegracaoQuery). Etapa/turma encerrada: recusa.
public record AssinarInstrutorTurmaPorDigitalCommand(Guid SessaoTreinamentoId, DadosAssinaturaInstrutorDigital Dados) : IRequest<AssinaturaInstrutorTurmaDto>;

public record AssinarInstrutorTurmaPorFacialCommand(Guid SessaoTreinamentoId, DadosAssinaturaInstrutorFacial Dados) : IRequest<AssinaturaInstrutorTurmaDto>;

public class AssinarInstrutorTurmaPorDigitalCommandValidator : AbstractValidator<AssinarInstrutorTurmaPorDigitalCommand>
{
    public AssinarInstrutorTurmaPorDigitalCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.Dados.DispositivoId).NotEmpty();
        RuleFor(x => x.Dados.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Dados.SegredoDispositivo).NotEmpty().When(x => string.IsNullOrEmpty(x.Dados.Desafio));
        RuleFor(x => x.Dados.AssinaturaLeitura).NotEmpty().When(x => !string.IsNullOrEmpty(x.Dados.Desafio));
        RuleFor(x => x.Dados.Score).InclusiveBetween(0, 100);
    }
}

public class AssinarInstrutorTurmaPorFacialCommandValidator : AbstractValidator<AssinarInstrutorTurmaPorFacialCommand>
{
    public AssinarInstrutorTurmaPorFacialCommandValidator()
    {
        RuleFor(x => x.SessaoTreinamentoId).NotEmpty();
        RuleFor(x => x.Dados.FotoJpeg).NotEmpty();
    }
}

public class AssinarInstrutorTurmaPorDigitalCommandHandler : IRequestHandler<AssinarInstrutorTurmaPorDigitalCommand, AssinaturaInstrutorTurmaDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IClienteIpProvider? _clienteIp;

    public AssinarInstrutorTurmaPorDigitalCommandHandler(IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao, IClienteIpProvider? clienteIp = null)
    {
        _db = db;
        _autenticacao = autenticacao;
        _clienteIp = clienteIp;
    }

    public async Task<AssinaturaInstrutorTurmaDto> Handle(AssinarInstrutorTurmaPorDigitalCommand request, CancellationToken ct)
    {
        var sessao = await AssinaturaInstrutorTurma.CarregarParaAssinarAsync(_db, request.SessaoTreinamentoId, ct);
        await AAHBRANT.SST.Application.IntegracoesAtendimento.ProtecaoTurmaIntegracao.GarantirEtapaEmAndamentoAsync(_db, sessao.Id, ct);
        return await AssinaturaInstrutorTurma.PorDigitalAsync(_db, _autenticacao, _clienteIp, sessao, request.Dados, ct);
    }
}

public class AssinarInstrutorTurmaPorFacialCommandHandler : IRequestHandler<AssinarInstrutorTurmaPorFacialCommand, AssinaturaInstrutorTurmaDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _facial;
    private readonly IClienteIpProvider? _clienteIp;

    public AssinarInstrutorTurmaPorFacialCommandHandler(IAppDbContext db, IAutenticacaoFacialService facial, IClienteIpProvider? clienteIp = null)
    {
        _db = db;
        _facial = facial;
        _clienteIp = clienteIp;
    }

    public async Task<AssinaturaInstrutorTurmaDto> Handle(AssinarInstrutorTurmaPorFacialCommand request, CancellationToken ct)
    {
        var sessao = await AssinaturaInstrutorTurma.CarregarParaAssinarAsync(_db, request.SessaoTreinamentoId, ct);
        await AAHBRANT.SST.Application.IntegracoesAtendimento.ProtecaoTurmaIntegracao.GarantirEtapaEmAndamentoAsync(_db, sessao.Id, ct);
        return await AssinaturaInstrutorTurma.PorFacialAsync(_db, _facial, _clienteIp, sessao, request.Dados, ct);
    }
}
