using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Terceirizados.Conformidade;

// Checklist da empresa terceirizada (especificação 21/10/2026, etapa 7): perfil com percentual,
// lista de empresas ordenada pela menos adequada, documentos exigidos configuráveis e envio dos
// documentos da empresa com validade.

// ---------- Perfil e lista ----------

public record ObterConformidadeEmpresaQuery(Guid EmpresaId) : IRequest<ConformidadeEmpresaDto?>;

public class ObterConformidadeEmpresaQueryHandler : IRequestHandler<ObterConformidadeEmpresaQuery, ConformidadeEmpresaDto?>
{
    private readonly IAppDbContext _db;
    public ObterConformidadeEmpresaQueryHandler(IAppDbContext db) => _db = db;

    public Task<ConformidadeEmpresaDto?> Handle(ObterConformidadeEmpresaQuery request, CancellationToken ct) =>
        CalculadoraConformidadeEmpresa.CalcularAsync(_db, request.EmpresaId, ct);
}

public record ResumoConformidadeEmpresaDto(
    Guid EmpresaId, string RazaoSocial, string Cnpj, bool EmpresaAtiva,
    int Atendidos, int Exigidos, int? Percentual, string Faixa, List<NivelConformidadeDto> Niveis);

public record ListarConformidadeEmpresasQuery : IRequest<List<ResumoConformidadeEmpresaDto>>;

public class ListarConformidadeEmpresasQueryHandler : IRequestHandler<ListarConformidadeEmpresasQuery, List<ResumoConformidadeEmpresaDto>>
{
    private readonly IAppDbContext _db;
    public ListarConformidadeEmpresasQueryHandler(IAppDbContext db) => _db = db;

    public async Task<List<ResumoConformidadeEmpresaDto>> Handle(ListarConformidadeEmpresasQuery request, CancellationToken ct)
    {
        var ids = await _db.Empresas.Select(e => e.Id).ToListAsync(ct);
        var resumos = new List<ResumoConformidadeEmpresaDto>();
        foreach (var id in ids)
        {
            var c = await CalculadoraConformidadeEmpresa.CalcularAsync(_db, id, ct);
            if (c is null) continue;
            resumos.Add(new ResumoConformidadeEmpresaDto(
                c.EmpresaId, c.RazaoSocial, c.Cnpj, c.EmpresaAtiva, c.Atendidos, c.Exigidos, c.Percentual, c.Faixa, c.Niveis));
        }
        // Da menos adequada para a mais adequada (pedido na especificação); sem itens vai para o fim.
        return resumos
            .OrderBy(r => r.Percentual ?? int.MaxValue)
            .ThenBy(r => r.RazaoSocial)
            .ToList();
    }
}

// ---------- Documentos exigidos (configuração) ----------

public record DocumentoExigidoEmpresaDto(Guid Id, string Nome, string? Descricao, bool ExigeValidade, bool Ativo, int Ordem);

public record ListarDocumentosExigidosEmpresaQuery : IRequest<List<DocumentoExigidoEmpresaDto>>;

public class ListarDocumentosExigidosEmpresaQueryHandler : IRequestHandler<ListarDocumentosExigidosEmpresaQuery, List<DocumentoExigidoEmpresaDto>>
{
    private readonly IAppDbContext _db;
    public ListarDocumentosExigidosEmpresaQueryHandler(IAppDbContext db) => _db = db;

    public Task<List<DocumentoExigidoEmpresaDto>> Handle(ListarDocumentosExigidosEmpresaQuery request, CancellationToken ct) =>
        _db.DocumentosExigidosEmpresa
            .OrderByDescending(d => d.Ativo).ThenBy(d => d.Ordem).ThenBy(d => d.Nome)
            .Select(d => new DocumentoExigidoEmpresaDto(d.Id, d.Nome, d.Descricao, d.ExigeValidade, d.Ativo, d.Ordem))
            .ToListAsync(ct);
}

// Id nulo cria; preenchido edita. Desativar tira do checklist de todas as empresas sem apagar envios.
public record SalvarDocumentoExigidoEmpresaCommand(Guid? Id, string Nome, string? Descricao, bool ExigeValidade, bool Ativo) : IRequest<Guid>;

public class SalvarDocumentoExigidoEmpresaCommandValidator : AbstractValidator<SalvarDocumentoExigidoEmpresaCommand>
{
    public SalvarDocumentoExigidoEmpresaCommandValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().WithMessage("Informe o nome do documento.").MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(500);
    }
}

public class SalvarDocumentoExigidoEmpresaCommandHandler : IRequestHandler<SalvarDocumentoExigidoEmpresaCommand, Guid>
{
    private readonly IAppDbContext _db;
    public SalvarDocumentoExigidoEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(SalvarDocumentoExigidoEmpresaCommand request, CancellationToken ct)
    {
        var nome = request.Nome.Trim();
        var duplicado = await _db.DocumentosExigidosEmpresa
            .AnyAsync(d => d.Id != request.Id && d.Nome == nome, ct);
        if (duplicado) throw new InvalidOperationException($"Já existe um documento exigido chamado \"{nome}\".");

        DocumentoExigidoEmpresa documento;
        if (request.Id is { } id)
        {
            documento = await _db.DocumentosExigidosEmpresa.FirstOrDefaultAsync(d => d.Id == id, ct)
                ?? throw new KeyNotFoundException("Documento exigido não encontrado.");
        }
        else
        {
            var ultimaOrdem = await _db.DocumentosExigidosEmpresa.MaxAsync(d => (int?)d.Ordem, ct) ?? 0;
            documento = new DocumentoExigidoEmpresa { Ordem = ultimaOrdem + 1 };
            _db.DocumentosExigidosEmpresa.Add(documento);
        }
        documento.Nome = nome;
        documento.Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim();
        documento.ExigeValidade = request.ExigeValidade;
        documento.Ativo = request.Ativo;
        await _db.SaveChangesAsync(ct);
        return documento.Id;
    }
}

// ---------- Documentos enviados pela empresa ----------

// Um por empresa e documento exigido: enviar de novo substitui arquivo e validade.
public record EnviarDocumentoEmpresaCommand(
    Guid EmpresaId, Guid DocumentoExigidoId, DateOnly? DataValidade, string NomeArquivo, string ContentType, byte[] Conteudo) : IRequest<Guid>;

public class EnviarDocumentoEmpresaCommandValidator : AbstractValidator<EnviarDocumentoEmpresaCommand>
{
    public const long TamanhoMaximoBytes = 20 * 1024 * 1024;

    public EnviarDocumentoEmpresaCommandValidator()
    {
        RuleFor(x => x.EmpresaId).NotEmpty();
        RuleFor(x => x.DocumentoExigidoId).NotEmpty();
        RuleFor(x => x.NomeArquivo).NotEmpty().MaximumLength(260);
        RuleFor(x => x.Conteudo).NotEmpty()
            .Must(c => TipoArquivo(c) is not null).WithMessage("Envie o documento em PDF, JPG ou PNG.")
            .Must(c => c.LongLength <= TamanhoMaximoBytes).WithMessage("O arquivo passa de 20 MB.");
    }

    // Pelo conteúdo, não pelo content-type do navegador (mesmo critério do PDF do contrato).
    public static string? TipoArquivo(byte[] c)
    {
        if (c.Length >= 4 && c[0] == '%' && c[1] == 'P' && c[2] == 'D' && c[3] == 'F') return "application/pdf";
        if (c.Length >= 3 && c[0] == 0xFF && c[1] == 0xD8 && c[2] == 0xFF) return "image/jpeg";
        if (c.Length >= 8 && c[0] == 0x89 && c[1] == 'P' && c[2] == 'N' && c[3] == 'G') return "image/png";
        return null;
    }
}

public class EnviarDocumentoEmpresaCommandHandler : IRequestHandler<EnviarDocumentoEmpresaCommand, Guid>
{
    private readonly IAppDbContext _db;
    public EnviarDocumentoEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task<Guid> Handle(EnviarDocumentoEmpresaCommand request, CancellationToken ct)
    {
        if (!await _db.Empresas.AnyAsync(e => e.Id == request.EmpresaId, ct))
            throw new KeyNotFoundException("Empresa não encontrada.");
        var exigido = await _db.DocumentosExigidosEmpresa.FirstOrDefaultAsync(d => d.Id == request.DocumentoExigidoId, ct)
            ?? throw new KeyNotFoundException("Documento exigido não encontrado.");
        if (exigido.ExigeValidade && request.DataValidade is null)
            throw new InvalidOperationException($"Informe a data de validade do {exigido.Nome}.");

        var documento = await _db.DocumentosEmpresaTerceirizada
            .Include(d => d.Arquivo)
            .FirstOrDefaultAsync(d => d.EmpresaId == request.EmpresaId && d.DocumentoExigidoEmpresaId == request.DocumentoExigidoId, ct);
        if (documento is null)
        {
            documento = new DocumentoEmpresaTerceirizada
            {
                EmpresaId = request.EmpresaId,
                DocumentoExigidoEmpresaId = request.DocumentoExigidoId,
            };
            _db.DocumentosEmpresaTerceirizada.Add(documento);
        }
        documento.DataValidade = request.DataValidade;
        documento.NomeArquivo = request.NomeArquivo;
        documento.ContentType = EnviarDocumentoEmpresaCommandValidator.TipoArquivo(request.Conteudo) ?? request.ContentType;
        documento.TamanhoBytes = request.Conteudo.LongLength;
        if (documento.Arquivo is null)
            documento.Arquivo = new ArquivoDocumentoEmpresaTerceirizada { Conteudo = request.Conteudo };
        else
            documento.Arquivo.Conteudo = request.Conteudo;
        await _db.SaveChangesAsync(ct);
        return documento.Id;
    }
}

public record ArquivoDocumentoEmpresaDto(string NomeArquivo, string ContentType, byte[] Conteudo);

public record ObterArquivoDocumentoEmpresaQuery(Guid EmpresaId, Guid DocumentoId) : IRequest<ArquivoDocumentoEmpresaDto?>;

public class ObterArquivoDocumentoEmpresaQueryHandler : IRequestHandler<ObterArquivoDocumentoEmpresaQuery, ArquivoDocumentoEmpresaDto?>
{
    private readonly IAppDbContext _db;
    public ObterArquivoDocumentoEmpresaQueryHandler(IAppDbContext db) => _db = db;

    public Task<ArquivoDocumentoEmpresaDto?> Handle(ObterArquivoDocumentoEmpresaQuery request, CancellationToken ct) =>
        _db.DocumentosEmpresaTerceirizada
            .Where(d => d.Id == request.DocumentoId && d.EmpresaId == request.EmpresaId && d.Arquivo != null)
            .Select(d => new ArquivoDocumentoEmpresaDto(d.NomeArquivo, d.ContentType, d.Arquivo!.Conteudo))
            .FirstOrDefaultAsync(ct);
}

public record RemoverDocumentoEmpresaCommand(Guid EmpresaId, Guid DocumentoId) : IRequest;

public class RemoverDocumentoEmpresaCommandHandler : IRequestHandler<RemoverDocumentoEmpresaCommand>
{
    private readonly IAppDbContext _db;
    public RemoverDocumentoEmpresaCommandHandler(IAppDbContext db) => _db = db;

    public async Task Handle(RemoverDocumentoEmpresaCommand request, CancellationToken ct)
    {
        var documento = await _db.DocumentosEmpresaTerceirizada
            .Include(d => d.Arquivo)
            .FirstOrDefaultAsync(d => d.Id == request.DocumentoId && d.EmpresaId == request.EmpresaId, ct)
            ?? throw new KeyNotFoundException("Documento não encontrado.");
        _db.DocumentosEmpresaTerceirizada.Remove(documento);
        await _db.SaveChangesAsync(ct);
    }
}
