using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.SessoesTreinamento.Commands;

// Criação da turma (pedido do usuário, 04/09) — o responsável define curso/data/carga horária e já
// seleciona os participantes (diferente do DDS, onde o participante só aparece quando a biometria
// confirma presença). A lista de trabalhadores disponíveis para seleção é filtrada por Obra no
// frontend (mesmo princípio de DdsDetalhePage.tsx) — não reforçado aqui no backend.
//
// Número do certificado (04/09, pedido do usuário): passou a ser gerado automaticamente aqui
// (mesmo padrão "PREFIXO-ANO-0001" de APR/DDS/PT/PCMSO — ver GeradorNumeroDocumentoService), não
// mais digitado manualmente. Isso é uma exceção deliberada ao princípio geral de
// IGeradorNumeroDocumentoService de não numerar automaticamente números que vêm de fora do sistema
// (CAT, CA de EPI): o usuário confirmou explicitamente que este número é um controle interno da
// empresa, não um número emitido por terceiro.
public record CriarSessaoTreinamentoCommand(
    Guid ObraId,
    Guid CursoTreinamentoId,
    DateTime DataRealizacao,
    int CargaHorariaRealizada,
    string? InstituicaoInstrutor,
    List<Guid> TrabalhadoresIds,
    // Dados do certificado (19/10). Nulos = padrão do curso/NR. Só o Técnico de Segurança (ou o
    // Administrador) pode mudar: para os demais perfis o servidor ignora o que vier e grava o padrão.
    string? ConteudoProgramatico = null,
    string? Local = null,
    string? InstrutorRegistroProfissional = null,
    int? ValidadeEmMeses = null,
    // Preenchidos pelo controller a partir do token, nunca pelo corpo da requisição.
    string? AzureAdObjectId = null,
    bool AutenticacaoHabilitada = false) : IRequest<Guid>;

public class CriarSessaoTreinamentoCommandValidator : AbstractValidator<CriarSessaoTreinamentoCommand>
{
    public CriarSessaoTreinamentoCommandValidator()
    {
        RuleFor(x => x.ObraId).NotEmpty();
        RuleFor(x => x.CursoTreinamentoId).NotEmpty();
        RuleFor(x => x.DataRealizacao).NotEmpty().NaoFutura("A data de realização");
        RuleFor(x => x.CargaHorariaRealizada).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TrabalhadoresIds).NotEmpty().WithMessage("Selecione ao menos um participante.");
    }
}

public class CriarSessaoTreinamentoCommandHandler : IRequestHandler<CriarSessaoTreinamentoCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IGeradorNumeroDocumentoService _geradorNumero;

    public CriarSessaoTreinamentoCommandHandler(IAppDbContext db, IGeradorNumeroDocumentoService geradorNumero)
    {
        _db = db;
        _geradorNumero = geradorNumero;
    }

    public async Task<Guid> Handle(CriarSessaoTreinamentoCommand request, CancellationToken ct)
    {
        if (!await _db.Obras.AnyAsync(o => o.Id == request.ObraId, ct))
            throw new KeyNotFoundException("Obra não encontrada.");

        var curso = await _db.CursosTreinamento.FirstOrDefaultAsync(c => c.Id == request.CursoTreinamentoId, ct)
            ?? throw new KeyNotFoundException("Curso de treinamento não encontrado.");
        var obraNome = await _db.Obras.Where(o => o.Id == request.ObraId).Select(o => o.Nome).FirstAsync(ct);

        var padrao = PadraoTurmaTreinamento.De(curso, obraNome);
        var podeEditar = await PerfilTecnicoSeguranca.PodeEditarDadosTreinamentoAsync(_db, request.AzureAdObjectId, request.AutenticacaoHabilitada, ct);

        // Carga horária nunca abaixo do mínimo da NR/curso (19/10): abaixo disso o certificado não
        // tem validade legal. Para quem não é técnico, vale sempre o mínimo.
        var cargaHoraria = podeEditar ? request.CargaHorariaRealizada : padrao.CargaHorariaMinima;
        if (cargaHoraria < padrao.CargaHorariaMinima)
            throw new InvalidOperationException(
                $"A carga horária mínima de {padrao.Norma ?? curso.Nome} é {padrao.CargaHorariaMinima}h. Abaixo disso o certificado não tem validade legal.");
        if (cargaHoraria <= 0)
            throw new InvalidOperationException("Informe a carga horária da turma.");

        var sessao = new SessaoTreinamento
        {
            ObraId = request.ObraId,
            CursoTreinamentoId = request.CursoTreinamentoId,
            DataRealizacao = request.DataRealizacao,
            CargaHorariaRealizada = cargaHoraria,
            InstituicaoInstrutor = podeEditar ? Limpar(request.InstituicaoInstrutor) : null,
            InstrutorRegistroProfissional = podeEditar ? Limpar(request.InstrutorRegistroProfissional) : null,
            ConteudoProgramatico = podeEditar ? Limpar(request.ConteudoProgramatico) ?? padrao.ConteudoProgramatico : padrao.ConteudoProgramatico,
            Local = podeEditar ? Limpar(request.Local) ?? padrao.Local : padrao.Local,
            ValidadeEmMeses = podeEditar && request.ValidadeEmMeses is > 0 ? request.ValidadeEmMeses.Value : padrao.ValidadeEmMeses,
            NumeroCertificado = await _geradorNumero.GerarAsync("CERT", ct),
        };

        foreach (var trabalhadorId in request.TrabalhadoresIds.Distinct())
        {
            sessao.Participantes.Add(new ParticipanteSessaoTreinamento
            {
                SessaoTreinamentoId = sessao.Id,
                TrabalhadorId = trabalhadorId,
            });
        }

        _db.SessoesTreinamento.Add(sessao);
        await _db.SaveChangesAsync(ct);
        return sessao.Id;
    }

    private static string? Limpar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
