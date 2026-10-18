using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AAHBRANT.SST.Application.Terceirizados.Portal;

// Fila de conferência do técnico de SST para o que chegou pelo portal. O escopo de obra vem de
// _db.Contratos (filtro global por obra): o técnico só vê e confere documento de contrato da obra dele.

public record DocumentoTerceiroFilaDto(
    Guid Id, TipoDocumentoTerceiro Tipo, StatusDocumentoTerceiro Status,
    Guid TrabalhadorId, string TrabalhadorNome, Guid ContratoId, string NumeroContrato, string EmpresaNome,
    string? CursoNome, DateTime DataDocumento, DateTime DataValidade,
    TipoExameAso? TipoExameAso, ResultadoAso? ResultadoAso, int? CargaHoraria, string? InstituicaoInstrutor, string? NumeroCertificado,
    string NomeArquivo, DateTime EnviadoEmUtc, string? MotivoRecusa, DateTime? ValidadoEmUtc);

public record ListarDocumentosTerceiroQuery(StatusDocumentoTerceiro? Status, Guid? ContratoId) : IRequest<List<DocumentoTerceiroFilaDto>>;

public class ListarDocumentosTerceiroQueryHandler : IRequestHandler<ListarDocumentosTerceiroQuery, List<DocumentoTerceiroFilaDto>>
{
    private readonly IAppDbContext _db;

    public ListarDocumentosTerceiroQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<DocumentoTerceiroFilaDto>> Handle(ListarDocumentosTerceiroQuery request, CancellationToken ct)
    {
        var contratos = _db.Contratos.Where(c => request.ContratoId == null || c.Id == request.ContratoId);
        var query = _db.DocumentosEnviadosTerceiro.AsNoTracking()
            .Where(d => contratos.Any(c => c.Id == d.ContratoId));
        if (request.Status is not null)
            query = query.Where(d => d.Status == request.Status);

        // Sem o Conteudo (pode ter 10 MB por linha): o arquivo sai só pela rota própria.
        var documentos = await query
            .OrderBy(d => d.Status).ThenBy(d => d.CreatedAtUtc)
            .Select(d => new
            {
                d.Id, d.Tipo, d.Status, d.TrabalhadorId, d.ContratoId, d.CursoTreinamentoId, d.DataDocumento, d.DataValidade,
                d.TipoExameAso, d.ResultadoAso, d.CargaHoraria, d.InstituicaoInstrutor, d.NumeroCertificado,
                d.NomeArquivo, d.CreatedAtUtc, d.MotivoRecusa, d.ValidadoEmUtc,
            })
            .ToListAsync(ct);

        // Nomes por consulta própria com IgnoreQueryFilters: a navegação aplicaria o filtro de
        // Trabalhador (Ativo) e um documento de pessoa já desligada sumiria do histórico.
        var trabalhadorIds = documentos.Select(d => d.TrabalhadorId).Distinct().ToList();
        var trabalhadores = await _db.Trabalhadores.IgnoreQueryFilters().AsNoTracking()
            .Where(t => trabalhadorIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.Nome, ct);
        var contratoIds = documentos.Select(d => d.ContratoId).Distinct().ToList();
        var dadosContrato = await _db.Contratos.IgnoreQueryFilters().AsNoTracking()
            .Where(c => contratoIds.Contains(c.Id))
            .Select(c => new { c.Id, c.NumeroContrato, EmpresaNome = c.Empresa != null ? c.Empresa.RazaoSocial : string.Empty })
            .ToDictionaryAsync(c => c.Id, ct);
        var cursoIds = documentos.Where(d => d.CursoTreinamentoId != null).Select(d => d.CursoTreinamentoId!.Value).Distinct().ToList();
        var cursos = await _db.CursosTreinamento.IgnoreQueryFilters().AsNoTracking()
            .Where(c => cursoIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nome, ct);

        return documentos.Select(d => new DocumentoTerceiroFilaDto(
            d.Id, d.Tipo, d.Status, d.TrabalhadorId, trabalhadores.GetValueOrDefault(d.TrabalhadorId, string.Empty),
            d.ContratoId, dadosContrato.GetValueOrDefault(d.ContratoId)?.NumeroContrato ?? string.Empty,
            dadosContrato.GetValueOrDefault(d.ContratoId)?.EmpresaNome ?? string.Empty,
            d.CursoTreinamentoId is null ? null : cursos.GetValueOrDefault(d.CursoTreinamentoId.Value),
            d.DataDocumento, d.DataValidade, d.TipoExameAso, d.ResultadoAso, d.CargaHoraria, d.InstituicaoInstrutor, d.NumeroCertificado,
            d.NomeArquivo, d.CreatedAtUtc, d.MotivoRecusa, d.ValidadoEmUtc)).ToList();
    }
}

public record ArquivoDocumentoTerceiroDto(byte[] Conteudo, string ContentType, string NomeArquivo);

public record ObterArquivoDocumentoTerceiroQuery(Guid Id) : IRequest<ArquivoDocumentoTerceiroDto?>;

public class ObterArquivoDocumentoTerceiroQueryHandler : IRequestHandler<ObterArquivoDocumentoTerceiroQuery, ArquivoDocumentoTerceiroDto?>
{
    private readonly IAppDbContext _db;

    public ObterArquivoDocumentoTerceiroQueryHandler(IAppDbContext db) => _db = db;

    public Task<ArquivoDocumentoTerceiroDto?> Handle(ObterArquivoDocumentoTerceiroQuery request, CancellationToken ct) =>
        _db.DocumentosEnviadosTerceiro.AsNoTracking()
            .Where(d => d.Id == request.Id && _db.Contratos.Any(c => c.Id == d.ContratoId))
            .Select(d => new ArquivoDocumentoTerceiroDto(d.Conteudo, d.ContentType, d.NomeArquivo))
            .FirstOrDefaultAsync(ct);
}

internal static class FilaDocumentoTerceiro
{
    public static async Task<DocumentoEnviadoTerceiro> ObterPendenteNoEscopoAsync(IAppDbContext db, Guid id, CancellationToken ct)
    {
        var documento = await db.DocumentosEnviadosTerceiro
            .FirstOrDefaultAsync(d => d.Id == id && db.Contratos.Any(c => c.Id == d.ContratoId), ct)
            ?? throw new KeyNotFoundException("Documento não encontrado.");
        if (documento.Status != StatusDocumentoTerceiro.AguardandoValidacao)
            throw new InvalidOperationException("Este documento já foi conferido.");
        return documento;
    }

    // O alerta "aguardando conferência" deixa de fazer sentido assim que alguém confere.
    public static async Task ResolverAlertasAsync(IAppDbContext db, Guid documentoId, CancellationToken ct)
    {
        var alertas = await db.Alertas.IgnoreQueryFilters()
            .Where(a => a.Ativo && a.EntidadeOrigemTipo == nameof(DocumentoEnviadoTerceiro) && a.EntidadeOrigemId == documentoId
                && a.Status != StatusAlerta.Resolvido)
            .ToListAsync(ct);
        foreach (var alerta in alertas)
            alerta.Status = StatusAlerta.Resolvido;
    }
}

// O técnico confere o arquivo e confirma (ou corrige) os dados declarados pela empresa. Só aqui nasce
// o Aso/Treinamento que conta para a liberação (CalculadoraLiberacaoTerceirizado).
public record AprovarDocumentoTerceiroCommand(
    Guid Id,
    Guid? UsuarioId,
    DateTime? DataDocumento = null,
    DateTime? DataValidade = null,
    TipoExameAso? TipoExameAso = null,
    ResultadoAso? ResultadoAso = null,
    int? CargaHoraria = null) : IRequest<Guid>;

public class AprovarDocumentoTerceiroCommandValidator : AbstractValidator<AprovarDocumentoTerceiroCommand>
{
    public AprovarDocumentoTerceiroCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.TipoExameAso).IsInEnum().When(x => x.TipoExameAso is not null);
        RuleFor(x => x.ResultadoAso).IsInEnum().NotEqual(ResultadoAso.Pendente).When(x => x.ResultadoAso is not null);
        RuleFor(x => x.CargaHoraria).GreaterThan(0).When(x => x.CargaHoraria is not null);
    }
}

public class AprovarDocumentoTerceiroCommandHandler : IRequestHandler<AprovarDocumentoTerceiroCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public AprovarDocumentoTerceiroCommandHandler(IAppDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<Guid> Handle(AprovarDocumentoTerceiroCommand request, CancellationToken ct)
    {
        var documento = await FilaDocumentoTerceiro.ObterPendenteNoEscopoAsync(_db, request.Id, ct);

        var dataDocumento = (request.DataDocumento ?? documento.DataDocumento).Date;
        var dataValidade = (request.DataValidade ?? documento.DataValidade).Date;
        if (dataValidade <= dataDocumento)
            throw new InvalidOperationException("A validade deve ser posterior à data do documento.");

        Guid geradoId;
        if (documento.Tipo == TipoDocumentoTerceiro.Aso)
        {
            var aso = new Aso
            {
                TrabalhadorId = documento.TrabalhadorId,
                Tipo = request.TipoExameAso ?? documento.TipoExameAso ?? TipoExameAso.Admissional,
                DataExame = dataDocumento,
                DataValidade = dataValidade,
                ResultadoStatus = request.ResultadoAso ?? documento.ResultadoAso ?? ResultadoAso.Pendente,
                CreatedBy = request.UsuarioId,
            };
            _db.Asos.Add(aso);
            documento.AsoGeradoId = geradoId = aso.Id;
        }
        else
        {
            // Curso feito fora da AAHBRANT: Externo bloqueia a emissão do certificado no modelo próprio
            // e o download entrega o arquivo original (ver OrigemCertificadoTreinamento).
            var treinamento = new Treinamento
            {
                TrabalhadorId = documento.TrabalhadorId,
                CursoTreinamentoId = documento.CursoTreinamentoId!.Value,
                DataRealizacao = dataDocumento,
                DataValidade = dataValidade,
                CargaHorariaRealizada = request.CargaHoraria ?? documento.CargaHoraria ?? 0,
                InstituicaoInstrutor = documento.InstituicaoInstrutor,
                NumeroCertificado = documento.NumeroCertificado,
                OrigemCertificado = OrigemCertificadoTreinamento.Externo,
                CreatedBy = request.UsuarioId,
            };
            _db.Treinamentos.Add(treinamento);
            _db.ArquivosCertificadoTreinamento.Add(new ArquivoCertificadoTreinamento
            {
                TreinamentoId = treinamento.Id,
                NomeArquivo = documento.NomeArquivo,
                ContentType = documento.ContentType,
                Conteudo = documento.Conteudo,
                TamanhoBytes = documento.TamanhoBytes,
            });
            documento.TreinamentoGeradoId = geradoId = treinamento.Id;
        }

        documento.Status = StatusDocumentoTerceiro.Aprovado;
        documento.ValidadoPorUsuarioId = request.UsuarioId;
        documento.ValidadoEmUtc = DateTime.UtcNow;
        await FilaDocumentoTerceiro.ResolverAlertasAsync(_db, documento.Id, ct);
        await _auditoria.RegistrarAsync("PortalTerceiro.DocumentoAprovado", nameof(DocumentoEnviadoTerceiro), documento.Id, request.UsuarioId,
            documento.TrabalhadorId, new { documento.Tipo, GeradoId = geradoId, DataDocumento = dataDocumento, DataValidade = dataValidade }, ct);
        await _db.SaveChangesAsync(ct);
        return geradoId;
    }
}

public record RecusarDocumentoTerceiroCommand(Guid Id, Guid? UsuarioId, string Motivo) : IRequest;

public class RecusarDocumentoTerceiroCommandValidator : AbstractValidator<RecusarDocumentoTerceiroCommand>
{
    public RecusarDocumentoTerceiroCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("Informe o motivo da devolução — a empresa recebe esse texto.").MaximumLength(500);
    }
}

public class RecusarDocumentoTerceiroCommandHandler : IRequestHandler<RecusarDocumentoTerceiroCommand>
{
    private readonly IAppDbContext _db;
    private readonly IAuditoriaService _auditoria;
    private readonly IEmailService _email;
    private readonly ILogger<RecusarDocumentoTerceiroCommandHandler> _logger;

    public RecusarDocumentoTerceiroCommandHandler(
        IAppDbContext db, IAuditoriaService auditoria, IEmailService email, ILogger<RecusarDocumentoTerceiroCommandHandler> logger)
    {
        _db = db;
        _auditoria = auditoria;
        _email = email;
        _logger = logger;
    }

    public async Task Handle(RecusarDocumentoTerceiroCommand request, CancellationToken ct)
    {
        var documento = await FilaDocumentoTerceiro.ObterPendenteNoEscopoAsync(_db, request.Id, ct);
        var motivo = request.Motivo.Trim();

        documento.Status = StatusDocumentoTerceiro.Recusado;
        documento.MotivoRecusa = motivo;
        documento.ValidadoPorUsuarioId = request.UsuarioId;
        documento.ValidadoEmUtc = DateTime.UtcNow;
        await FilaDocumentoTerceiro.ResolverAlertasAsync(_db, documento.Id, ct);
        await _auditoria.RegistrarAsync("PortalTerceiro.DocumentoRecusado", nameof(DocumentoEnviadoTerceiro), documento.Id, request.UsuarioId,
            documento.TrabalhadorId, new { documento.Tipo, Motivo = motivo }, ct);
        await _db.SaveChangesAsync(ct);

        // Aviso à empresa é cortesia: o motivo também aparece no portal, então falha de e-mail não
        // desfaz a devolução.
        var destino = await _db.AcessosPortalTerceiro.IgnoreQueryFilters()
            .Where(a => a.Id == documento.AcessoPortalTerceiroId).Select(a => a.Email).FirstOrDefaultAsync(ct);
        var nomePessoa = await _db.Trabalhadores.IgnoreQueryFilters()
            .Where(t => t.Id == documento.TrabalhadorId).Select(t => t.Nome).FirstOrDefaultAsync(ct) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(destino)) return;
        try
        {
            var tipo = documento.Tipo == TipoDocumentoTerceiro.Aso ? "ASO" : "certificado de treinamento";
            await _email.EnviarAsync(destino, EmailPortalTerceiro.AssuntoRecusa(nomePessoa), EmailPortalTerceiro.HtmlRecusa(nomePessoa, tipo, motivo), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao avisar a empresa da devolução do documento {DocumentoId}", documento.Id);
        }
    }
}
