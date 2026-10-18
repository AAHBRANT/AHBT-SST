using System.Security.Cryptography;
using AAHBRANT.SST.Application.Alertas;
using AAHBRANT.SST.Application.Common;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Application.Common.Seguranca;
using AAHBRANT.SST.Application.Terceirizados.Commands;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Portal;

// O que a empresa terceirizada faz depois de entrar no portal. O AcessoId sempre vem da sessão
// resolvida pela API (ResolverSessaoPortalQuery), nunca do corpo da requisição — e tudo é amarrado
// ao contrato daquele acesso: a empresa não enxerga nem altera pessoa de outro contrato.

public record VagaPortalDto(Guid FuncaoId, string FuncaoNome, int QuantidadeVagas, int QuantidadePreenchidas);

public record DocumentoPortalDto(
    Guid Id, TipoDocumentoTerceiro Tipo, StatusDocumentoTerceiro Status, string? CursoNome,
    DateTime DataDocumento, DateTime DataValidade, DateTime EnviadoEmUtc, string? MotivoRecusa);

public record PessoaPortalDto(
    Guid TrabalhadorId, string Nome, string FuncaoNome, bool Liberado, List<string> Pendencias, List<DocumentoPortalDto> Documentos);

public record CursoPortalDto(Guid Id, string Nome, int CargaHorariaMinima, int ValidadeEmMeses);

public record PainelPortalDto(
    string EmpresaNome, string NumeroContrato, string ObraNome, DateOnly DataInicioVigencia, DateOnly DataFimVigencia,
    bool TermoAceito, string VersaoTermo, List<VagaPortalDto> Vagas, List<PessoaPortalDto> Pessoas, List<CursoPortalDto> Cursos);

public record ObterPainelPortalQuery(Guid AcessoId) : IRequest<PainelPortalDto>;

public class ObterPainelPortalQueryHandler : IRequestHandler<ObterPainelPortalQuery, PainelPortalDto>
{
    private readonly IAppDbContext _db;

    public ObterPainelPortalQueryHandler(IAppDbContext db) => _db = db;

    public async Task<PainelPortalDto> Handle(ObterPainelPortalQuery request, CancellationToken ct)
    {
        var acesso = await _db.AcessosPortalTerceiro.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.AcessoId, ct)
            ?? throw new KeyNotFoundException(AcessoPortalConsulta.MensagemLinkInvalido);
        var contrato = await AcessoPortalConsulta.ObterContratoValidoAsync(_db, acesso.ContratoId, ct)
            ?? throw new KeyNotFoundException(AcessoPortalConsulta.MensagemLinkInvalido);
        var obraNome = await _db.Obras.IgnoreQueryFilters()
            .Where(o => o.Id == contrato.ObraId).Select(o => o.Nome).FirstOrDefaultAsync(ct) ?? string.Empty;

        var vagas = await _db.ContratoVagasFuncao.AsNoTracking()
            .Where(v => v.ContratoId == contrato.Id)
            .Select(v => new { v.FuncaoId, v.QuantidadeVagas, v.QuantidadePreenchidas })
            .ToListAsync(ct);

        var pessoas = await _db.Trabalhadores.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.ContratoId == contrato.Id && t.Ativo && t.DataDemissao == null)
            .OrderBy(t => t.Nome)
            .Select(t => new { t.Id, t.Nome, t.FuncaoId })
            .ToListAsync(ct);

        var funcaoIds = vagas.Select(v => v.FuncaoId).Concat(pessoas.Select(p => p.FuncaoId)).Distinct().ToList();
        var funcoes = await _db.Funcoes.IgnoreQueryFilters().AsNoTracking()
            .Where(f => funcaoIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Nome, ct);

        var pessoaIds = pessoas.Select(p => p.Id).ToList();
        var documentos = await _db.DocumentosEnviadosTerceiro.AsNoTracking()
            .Where(d => d.ContratoId == contrato.Id && pessoaIds.Contains(d.TrabalhadorId))
            .OrderByDescending(d => d.CreatedAtUtc)
            .Select(d => new
            {
                d.TrabalhadorId,
                Dto = new DocumentoPortalDto(d.Id, d.Tipo, d.Status, d.CursoTreinamento != null ? d.CursoTreinamento.Nome : null,
                    d.DataDocumento, d.DataValidade, d.CreatedAtUtc, d.MotivoRecusa),
            })
            .ToListAsync(ct);

        var pessoasDto = new List<PessoaPortalDto>();
        foreach (var p in pessoas)
        {
            var pendencias = await CalculadoraLiberacaoTerceirizado.ObterPendenciasAsync(_db, p.Id, p.FuncaoId, ct);
            pessoasDto.Add(new PessoaPortalDto(
                p.Id, p.Nome, funcoes.GetValueOrDefault(p.FuncaoId, string.Empty), pendencias.Count == 0, pendencias,
                documentos.Where(d => d.TrabalhadorId == p.Id).Select(d => d.Dto).ToList()));
        }

        // Cursos que a empresa pode precisar enviar: a Integração de Segurança e os obrigatórios das
        // funções do contrato (MatrizTreinamentoFuncao) — não o catálogo inteiro.
        var cursos = await _db.CursosTreinamento.AsNoTracking()
            .Where(c => c.EhIntegracaoSeguranca
                || _db.MatrizTreinamentoFuncoes.Any(m => m.CursoTreinamentoId == c.Id && funcaoIds.Contains(m.FuncaoId)))
            .OrderBy(c => c.Nome)
            .Select(c => new CursoPortalDto(c.Id, c.Nome, c.CargaHorariaMinima, c.ValidadeEmMeses))
            .ToListAsync(ct);

        var termoAceito = acesso.TermoAceitoEmUtc is not null && acesso.TermoVersaoAceita == PortalTerceiroRegras.VersaoTermoAtual;

        return new PainelPortalDto(
            contrato.Empresa?.RazaoSocial ?? string.Empty, contrato.NumeroContrato, obraNome,
            contrato.DataInicioVigencia, contrato.DataFimVigencia, termoAceito, PortalTerceiroRegras.VersaoTermoAtual,
            vagas.Select(v => new VagaPortalDto(v.FuncaoId, funcoes.GetValueOrDefault(v.FuncaoId, string.Empty), v.QuantidadeVagas, v.QuantidadePreenchidas))
                .OrderBy(v => v.FuncaoNome).ToList(),
            pessoasDto, cursos);
    }
}

public record AceitarTermoPortalCommand(Guid AcessoId, string Versao) : IRequest;

public class AceitarTermoPortalCommandHandler : IRequestHandler<AceitarTermoPortalCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public AceitarTermoPortalCommandHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task Handle(AceitarTermoPortalCommand request, CancellationToken ct)
    {
        if (request.Versao != PortalTerceiroRegras.VersaoTermoAtual)
            throw new InvalidOperationException("O termo foi atualizado. Recarregue a página e leia a versão atual.");

        var acesso = await _db.AcessosPortalTerceiro.FirstOrDefaultAsync(a => a.Id == request.AcessoId, ct)
            ?? throw new KeyNotFoundException(AcessoPortalConsulta.MensagemLinkInvalido);
        acesso.TermoAceitoEmUtc = DateTime.UtcNow;
        acesso.TermoVersaoAceita = request.Versao;
        await _auditoria.RegistrarAsync("PortalTerceiro.TermoAceito", nameof(AcessoPortalTerceiro), acesso.Id, null, null,
            new { acesso.ContratoId, Versao = request.Versao }, ct);
        await _db.SaveChangesAsync(ct);
    }
}

internal static class TermoPortal
{
    public static async Task<AcessoPortalTerceiro> ObterAcessoComTermoAceitoAsync(IAppDbContext db, Guid acessoId, CancellationToken ct)
    {
        var acesso = await db.AcessosPortalTerceiro.AsNoTracking().FirstOrDefaultAsync(a => a.Id == acessoId, ct)
            ?? throw new KeyNotFoundException(AcessoPortalConsulta.MensagemLinkInvalido);
        if (acesso.TermoAceitoEmUtc is null || acesso.TermoVersaoAceita != PortalTerceiroRegras.VersaoTermoAtual)
            throw new InvalidOperationException("Leia e aceite o termo de uso do portal antes de continuar.");
        return acesso;
    }
}

// Reaproveita CadastrarPessoaTerceirizadaCommand inteiro (vaga, reserva de EPI, alertas de estoque):
// o portal só troca quem informa os dados. O contrato sai da sessão, não da requisição.
public record CadastrarPessoaPortalCommand(
    Guid AcessoId, Guid FuncaoId, string Nome, string Matricula, string Cpf, DateTime DataAdmissao) : IRequest<Guid>;

public class CadastrarPessoaPortalCommandHandler : IRequestHandler<CadastrarPessoaPortalCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly ISender _sender;
    private readonly IAuditoriaService _auditoria;

    public CadastrarPessoaPortalCommandHandler(IAppDbContext db, ISender sender, IAuditoriaService auditoria)
    {
        _db = db;
        _sender = sender;
        _auditoria = auditoria;
    }

    public async Task<Guid> Handle(CadastrarPessoaPortalCommand request, CancellationToken ct)
    {
        var acesso = await TermoPortal.ObterAcessoComTermoAceitoAsync(_db, request.AcessoId, ct);

        var cpf = new string((request.Cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        var trabalhadorId = await _sender.Send(new CadastrarPessoaTerceirizadaCommand(
            acesso.ContratoId, request.FuncaoId, request.Nome?.Trim() ?? string.Empty, request.Matricula?.Trim() ?? string.Empty,
            cpf, request.DataAdmissao), ct);

        await _auditoria.RegistrarAsync("PortalTerceiro.PessoaCadastrada", nameof(Trabalhador), trabalhadorId, null, trabalhadorId,
            new { AcessoId = acesso.Id, acesso.ContratoId, request.FuncaoId }, ct);
        await _db.SaveChangesAsync(ct);
        return trabalhadorId;
    }
}

public record EnviarDocumentoPortalCommand(
    Guid AcessoId,
    Guid TrabalhadorId,
    TipoDocumentoTerceiro Tipo,
    string NomeArquivo,
    byte[] Conteudo,
    string ContentType,
    DateTime DataDocumento,
    DateTime DataValidade,
    TipoExameAso? TipoExameAso,
    ResultadoAso? ResultadoAso,
    Guid? CursoTreinamentoId,
    int? CargaHoraria,
    string? InstituicaoInstrutor,
    string? NumeroCertificado) : IRequest<Guid>;

public class EnviarDocumentoPortalCommandValidator : AbstractValidator<EnviarDocumentoPortalCommand>
{
    public EnviarDocumentoPortalCommandValidator()
    {
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Tipo).IsInEnum();
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(x => x.ContentType)
            .Must(t => PortalTerceiroRegras.TiposArquivoPermitidos.Contains(t))
            .WithMessage("O documento deve ser um arquivo PDF, JPEG ou PNG.");
        RuleFor(x => x.Conteudo)
            .NotEmpty().WithMessage("O arquivo do documento é obrigatório.")
            .Must(c => c.Length <= PortalTerceiroRegras.TamanhoMaximoArquivoBytes).WithMessage("O arquivo deve ter no máximo 10 MB.")
            .Must((comando, conteudo) => ValidadorAssinaturaArquivo.AssinaturaConfere(conteudo, comando.ContentType))
                .WithMessage("O conteúdo do arquivo não corresponde ao tipo declarado.");
        RuleFor(x => x.DataDocumento).NotEmpty()
            .LessThanOrEqualTo(_ => DateTime.UtcNow.Date.AddDays(1)).WithMessage("A data do documento não pode estar no futuro.");
        RuleFor(x => x.DataValidade).NotEmpty().GreaterThan(x => x.DataDocumento)
            .WithMessage("A validade deve ser posterior à data do documento.");

        When(x => x.Tipo == TipoDocumentoTerceiro.Aso, () =>
        {
            RuleFor(x => x.TipoExameAso).NotNull().WithMessage("Informe o tipo do exame (admissional, periódico...).").IsInEnum();
            RuleFor(x => x.ResultadoAso).NotNull().WithMessage("Informe o resultado do ASO.").IsInEnum()
                .NotEqual(ResultadoAso.Pendente).WithMessage("Informe o resultado do ASO (apto, apto com restrição ou inapto).");
        });
        When(x => x.Tipo == TipoDocumentoTerceiro.CertificadoTreinamento, () =>
        {
            RuleFor(x => x.CursoTreinamentoId).NotEmpty().WithMessage("Informe o treinamento do certificado.");
            RuleFor(x => x.CargaHoraria).NotNull().GreaterThan(0).WithMessage("Informe a carga horária do certificado.");
            RuleFor(x => x.InstituicaoInstrutor).MaximumLength(200);
            RuleFor(x => x.NumeroCertificado).MaximumLength(100);
        });
    }
}

public class EnviarDocumentoPortalCommandHandler : IRequestHandler<EnviarDocumentoPortalCommand, Guid>
{
    public const string MotivoSubstituido = "Substituído por um envio mais recente.";

    private readonly IAppDbContext _db;
    private readonly ITecnicosSegurancaPorObraService _tecnicosSegurancaPorObra;
    private readonly IFilaNotificacaoTeams _filaNotificacaoTeams;
    private readonly IAuditoriaService _auditoria;

    public EnviarDocumentoPortalCommandHandler(
        IAppDbContext db, ITecnicosSegurancaPorObraService tecnicosSegurancaPorObra,
        IFilaNotificacaoTeams filaNotificacaoTeams, IAuditoriaService auditoria)
    {
        _db = db;
        _tecnicosSegurancaPorObra = tecnicosSegurancaPorObra;
        _filaNotificacaoTeams = filaNotificacaoTeams;
        _auditoria = auditoria;
    }

    public async Task<Guid> Handle(EnviarDocumentoPortalCommand request, CancellationToken ct)
    {
        var acesso = await TermoPortal.ObterAcessoComTermoAceitoAsync(_db, request.AcessoId, ct);
        var contrato = await AcessoPortalConsulta.ObterContratoValidoAsync(_db, acesso.ContratoId, ct)
            ?? throw new KeyNotFoundException(AcessoPortalConsulta.MensagemLinkInvalido);

        var trabalhador = await _db.Trabalhadores.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Id == request.TrabalhadorId && t.ContratoId == contrato.Id && t.Ativo && t.DataDemissao == null)
            .Select(t => new { t.Id, t.Nome })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Pessoa não encontrada neste contrato.");

        string? cursoNome = null;
        if (request.Tipo == TipoDocumentoTerceiro.CertificadoTreinamento)
        {
            cursoNome = await _db.CursosTreinamento.Where(c => c.Id == request.CursoTreinamentoId)
                .Select(c => c.Nome).FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Treinamento não encontrado.");
        }

        // Reenvio (foto ilegível, arquivo errado) substitui o envio ainda não conferido do mesmo
        // documento, para o técnico não conferir duas versões.
        var pendentesAnteriores = await _db.DocumentosEnviadosTerceiro
            .Where(d => d.TrabalhadorId == trabalhador.Id && d.Tipo == request.Tipo
                && d.CursoTreinamentoId == (request.Tipo == TipoDocumentoTerceiro.CertificadoTreinamento ? request.CursoTreinamentoId : null)
                && d.Status == StatusDocumentoTerceiro.AguardandoValidacao)
            .ToListAsync(ct);
        foreach (var anterior in pendentesAnteriores)
        {
            anterior.Status = StatusDocumentoTerceiro.Recusado;
            anterior.MotivoRecusa = MotivoSubstituido;
        }

        var ehAso = request.Tipo == TipoDocumentoTerceiro.Aso;
        var documento = new DocumentoEnviadoTerceiro
        {
            AcessoPortalTerceiroId = acesso.Id,
            ContratoId = contrato.Id,
            TrabalhadorId = trabalhador.Id,
            Tipo = request.Tipo,
            NomeArquivo = Path.GetFileName(request.NomeArquivo),
            ContentType = request.ContentType,
            Conteudo = request.Conteudo,
            TamanhoBytes = request.Conteudo.LongLength,
            HashSha256 = Convert.ToHexString(SHA256.HashData(request.Conteudo)),
            DataDocumento = request.DataDocumento.Date,
            DataValidade = request.DataValidade.Date,
            TipoExameAso = ehAso ? request.TipoExameAso : null,
            ResultadoAso = ehAso ? request.ResultadoAso : null,
            CursoTreinamentoId = ehAso ? null : request.CursoTreinamentoId,
            CargaHoraria = ehAso ? null : request.CargaHoraria,
            InstituicaoInstrutor = ehAso ? null : request.InstituicaoInstrutor?.Trim(),
            NumeroCertificado = ehAso ? null : request.NumeroCertificado?.Trim(),
        };
        _db.DocumentosEnviadosTerceiro.Add(documento);

        var descricaoDocumento = ehAso ? "ASO" : $"Certificado {cursoNome}";
        var alertas = new List<Alerta>();
        foreach (var usuarioId in await _tecnicosSegurancaPorObra.ObterUsuarioIdsAsync(contrato.ObraId, ct))
        {
            var alerta = new Alerta
            {
                Tipo = TipoAlerta.DocumentoTerceiroAguardandoValidacao,
                Severidade = SeveridadeAlerta.Atencao,
                Titulo = $"{descricaoDocumento} de {trabalhador.Nome} aguardando conferência",
                Descricao = $"Enviado pela {contrato.Empresa?.RazaoSocial} (contrato {contrato.NumeroContrato}) pelo portal de terceiros. Confira o documento para liberar a pessoa.",
                EntidadeOrigemTipo = nameof(DocumentoEnviadoTerceiro),
                EntidadeOrigemId = documento.Id,
                TrabalhadorId = trabalhador.Id,
                ObraId = contrato.ObraId,
                DestinatarioUsuarioId = usuarioId,
            };
            _db.Alertas.Add(alerta);
            alertas.Add(alerta);
        }

        await _auditoria.RegistrarAsync("PortalTerceiro.DocumentoEnviado", nameof(DocumentoEnviadoTerceiro), documento.Id, null, trabalhador.Id,
            new { AcessoId = acesso.Id, documento.Tipo, documento.CursoTreinamentoId, documento.HashSha256, Substituidos = pendentesAnteriores.Count }, ct);
        await _db.SaveChangesAsync(ct);

        foreach (var alerta in alertas)
        {
            await _filaNotificacaoTeams.EnfileirarAsync(
                new NotificacaoTeamsMensagem(alerta.Id, alerta.DestinatarioUsuarioId!.Value, alerta.Titulo, alerta.Descricao),
                ct);
        }

        return documento.Id;
    }
}
