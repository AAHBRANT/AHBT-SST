using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Commands;

public record EmpresaWebhookDto(
    string RazaoSocial,
    string? NomeFantasia,
    string Cnpj,
    string? TipoServicoPrestado,
    string? ContatoNome,
    string? ContatoTelefone,
    string? ContatoEmail);

public record VagaFuncaoWebhookDto(string FuncaoNome, int Quantidade);

public record ContratoValidadoWebhookCommand(
    string GJuriContratoId,
    string NumeroContrato,
    DateOnly DataInicioVigencia,
    DateOnly DataFimVigencia,
    Guid ObraId,
    EmpresaWebhookDto Empresa,
    List<VagaFuncaoWebhookDto> Vagas) : IRequest<Guid>;

public class ContratoValidadoWebhookCommandValidator : AbstractValidator<ContratoValidadoWebhookCommand>
{
    public ContratoValidadoWebhookCommandValidator()
    {
        RuleFor(x => x.GJuriContratoId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NumeroContrato).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.Empresa.RazaoSocial).NotEmpty();
        RuleFor(x => x.Empresa.Cnpj).NotEmpty().Length(14).Matches("^[0-9]+$");
        RuleFor(x => x.Vagas).NotEmpty().WithMessage("O contrato precisa vir com ao menos uma vaga.");
        RuleForEach(x => x.Vagas).ChildRules(v =>
        {
            v.RuleFor(x => x.FuncaoNome).NotEmpty();
            v.RuleFor(x => x.Quantidade).GreaterThan(0);
        });
    }
}

public class ContratoValidadoWebhookCommandHandler : IRequestHandler<ContratoValidadoWebhookCommand, Guid>
{
    private readonly IAppDbContext _db;
    public ContratoValidadoWebhookCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(ContratoValidadoWebhookCommand request, CancellationToken ct)
    {
        var contratoExistente = await _db.Contratos
            .Include(c => c.Vagas)
            .FirstOrDefaultAsync(c => c.GJuriContratoId == request.GJuriContratoId, ct);

        // Contrato já travado (qualquer vaga com gente alocada) é imutável: reenvio é no-op puro,
        // o conteúdo do payload deixa de importar. Esse check precisa vir ANTES da resolução de
        // FuncaoNome — achado da revisão (2026-09-18): resolver nome de função antes desse check
        // fazia um reenvio inofensivo (ex.: função renomeada/typo do lado do G-Juri) explodir com
        // KeyNotFoundException mesmo quando o contrato já estava travado e a única resposta correta
        // era devolver o Id existente sem tocar em nada.
        if (contratoExistente is not null && contratoExistente.Vagas.Any(v => v.QuantidadePreenchidas > 0))
            return contratoExistente.Id;

        // Resolve nome -> Funcao.Id (não Guid cru no payload — decisão revisada no planejamento:
        // evita acoplar o G-Juri a identificadores internos do SST, que divergem entre ambientes).
        // Só roda a partir daqui: contrato novo, ou contrato existente ainda não travado — os dois
        // únicos casos em que o conteúdo do payload de fato importa.
        var funcoesPorNome = await _db.Funcoes
            .Where(f => request.Vagas.Select(v => v.FuncaoNome).Contains(f.Nome))
            .ToDictionaryAsync(f => f.Nome, f => f.Id, ct);
        var nomeFuncaoFaltante = request.Vagas.Select(v => v.FuncaoNome).FirstOrDefault(nome => !funcoesPorNome.ContainsKey(nome));
        if (nomeFuncaoFaltante is not null)
            throw new KeyNotFoundException($"Função '{nomeFuncaoFaltante}' não encontrada.");

        if (contratoExistente is not null)
            return await AtualizarAsync(contratoExistente, request, funcoesPorNome, ct);

        var obraExiste = await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct);
        if (!obraExiste)
            throw new KeyNotFoundException($"Obra {request.ObraId} não encontrada.");

        var empresa = await _db.Empresas.FirstOrDefaultAsync(e => e.Cnpj == request.Empresa.Cnpj, ct);
        if (empresa is null)
        {
            empresa = new Empresa
            {
                RazaoSocial = request.Empresa.RazaoSocial,
                NomeFantasia = request.Empresa.NomeFantasia,
                Cnpj = request.Empresa.Cnpj,
                TipoServicoPrestado = request.Empresa.TipoServicoPrestado,
                ContatoNome = request.Empresa.ContatoNome,
                ContatoTelefone = request.Empresa.ContatoTelefone,
                ContatoEmail = request.Empresa.ContatoEmail,
            };
            _db.Empresas.Add(empresa);
        }

        var contrato = new Contrato
        {
            EmpresaId = empresa.Id,
            ObraId = request.ObraId,
            NumeroContrato = request.NumeroContrato,
            DataInicioVigencia = request.DataInicioVigencia,
            DataFimVigencia = request.DataFimVigencia,
            GJuriContratoId = request.GJuriContratoId,
        };
        _db.Contratos.Add(contrato);

        foreach (var vaga in request.Vagas)
        {
            _db.ContratoVagasFuncao.Add(new ContratoVagaFuncao
            {
                ContratoId = contrato.Id,
                FuncaoId = funcoesPorNome[vaga.FuncaoNome],
                QuantidadeVagas = vaga.Quantidade,
                QuantidadePreenchidas = 0,
            });
        }

        await _db.SaveChangesAsync(ct);
        return contrato.Id;
    }

    // Idempotência não é "ignorar sempre": enquanto nenhuma vaga do contrato tiver pessoa
    // cadastrada (checado em Handle, antes da resolução de nomes de função), um reenvio do G-Juri
    // com dados diferentes (vigência corrigida, quantidade de vagas ajustada) atualiza os campos.
    // O caso "já travado" nunca chega até aqui — Handle já retornou o Id existente antes de chamar
    // este método. Decisão revisada no planejamento.
    private async Task<Guid> AtualizarAsync(
        Contrato contrato, ContratoValidadoWebhookCommand request, Dictionary<string, Guid> funcoesPorNome, CancellationToken ct)
    {
        contrato.NumeroContrato = request.NumeroContrato;
        contrato.DataInicioVigencia = request.DataInicioVigencia;
        contrato.DataFimVigencia = request.DataFimVigencia;

        var vagasAtuais = contrato.Vagas.ToDictionary(v => v.FuncaoId);
        foreach (var vaga in request.Vagas)
        {
            var funcaoId = funcoesPorNome[vaga.FuncaoNome];
            if (vagasAtuais.TryGetValue(funcaoId, out var vagaAtual))
            {
                vagaAtual.QuantidadeVagas = vaga.Quantidade;
            }
            else
            {
                _db.ContratoVagasFuncao.Add(new ContratoVagaFuncao
                {
                    ContratoId = contrato.Id, FuncaoId = funcaoId, QuantidadeVagas = vaga.Quantidade, QuantidadePreenchidas = 0,
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return contrato.Id;
    }
}
