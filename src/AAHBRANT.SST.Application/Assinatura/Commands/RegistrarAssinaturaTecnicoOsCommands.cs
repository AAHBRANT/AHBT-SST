using AAHBRANT.SST.Application.Assinatura.Queries;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Assinatura.Commands;

// Assinatura do técnico na Ordem de Serviço (16/11/2026): só biometria (digital OU facial), com o papel
// TecnicoSegurancaOs fixo no servidor (o cliente não escolhe papel). Quem pode assinar: ver TecnicoOrdemServicoRegra.
// O documento precisa ser de uma OS dentro do escopo de obra do usuário; fora dele: 404.
internal static class DocumentoOsDoTecnico
{
    public static async Task<TecnicoOrdemServicoRegra.ContextoOs> CarregarNoEscopoAsync(IAppDbContext db, Guid documentoAssinaturaId, CancellationToken ct)
    {
        var documento = await db.DocumentosAssinatura.FirstOrDefaultAsync(d => d.Id == documentoAssinaturaId, ct);
        if (documento is null
            || documento.EntidadeTipo != OrdemServico.EntidadeTipoAssinatura
            || !await EscopoDocumentoAssinatura.PodeVerAsync(db, documento.EntidadeTipo, documento.EntidadeId, ct))
            throw new KeyNotFoundException("Documento de assinatura da Ordem de Serviço não encontrado.");
        return await TecnicoOrdemServicoRegra.CarregarContextoPorOsAsync(db, documento.EntidadeId, ct);
    }
}

public record RegistrarAssinaturaTecnicoOsBiometriaLocalCommand(
    Guid DocumentoAssinaturaId, Guid DispositivoId, string SegredoDispositivo, Guid TrabalhadorId, double Score, string? IpAddress = null, byte[]? ImagemDigital = null,
    LocalizacaoAssinatura? Localizacao = null, string? Desafio = null, string? AssinaturaLeitura = null) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaTecnicoOsBiometriaLocalCommandValidator : AbstractValidator<RegistrarAssinaturaTecnicoOsBiometriaLocalCommand>
{
    public RegistrarAssinaturaTecnicoOsBiometriaLocalCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.DispositivoId).NotEmpty();
        RuleFor(x => x.SegredoDispositivo).NotEmpty().When(x => string.IsNullOrEmpty(x.Desafio));
        RuleFor(x => x.AssinaturaLeitura).NotEmpty().When(x => !string.IsNullOrEmpty(x.Desafio));
        RuleFor(x => x.TrabalhadorId).NotEmpty();
        RuleFor(x => x.Score).InclusiveBetween(0, 100);
    }
}

public class RegistrarAssinaturaTecnicoOsBiometriaLocalCommandHandler : IRequestHandler<RegistrarAssinaturaTecnicoOsBiometriaLocalCommand, DocumentoSignatarioDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoBiometriaLocalService _autenticacao;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaTecnicoOsBiometriaLocalCommandHandler(IAppDbContext db, IAutenticacaoBiometriaLocalService autenticacao, IRegistradorAssinaturaService registrador)
    {
        _db = db;
        _autenticacao = autenticacao;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaTecnicoOsBiometriaLocalCommand request, CancellationToken ct)
    {
        var contexto = await DocumentoOsDoTecnico.CarregarNoEscopoAsync(_db, request.DocumentoAssinaturaId, ct);
        // Recusa antes de ler a digital: o desafio do leitor é de uso único e não deve ser gasto com quem a regra já barra.
        var informado = await _db.Trabalhadores.IgnoreQueryFilters().Include(t => t.Funcao)
            .FirstOrDefaultAsync(t => t.Id == request.TrabalhadorId, ct)
            ?? throw new InvalidOperationException("Trabalhador não encontrado.");
        TecnicoOrdemServicoRegra.GarantirIdentidade(contexto, informado, Domain.Enums.MetodoAutenticacaoAssinatura.Biometria);

        var resultado = await _autenticacao.AutenticarPorMatchLocalAsync(
            request.DispositivoId, request.SegredoDispositivo, request.TrabalhadorId, request.Score,
            string.IsNullOrEmpty(request.Desafio) ? null : new LeituraBiometricaAssinada(request.Desafio, request.AssinaturaLeitura ?? string.Empty),
            ct, permitirInstrutorDeOutraObra: contexto.InstrutorIntegracaoId is not null);
        return await _registrador.RegistrarAsync(
            request.DocumentoAssinaturaId, resultado, request.IpAddress, ct,
            request.ImagemDigital, request.ImagemDigital is null ? null : "image/png",
            papel: Domain.Enums.PapelAssinatura.TecnicoSegurancaOs, localizacao: request.Localizacao);
    }
}

// A obra do grupo facial sai da própria OS (não do corpo da requisição).
public record RegistrarAssinaturaTecnicoOsFacialCommand(
    Guid DocumentoAssinaturaId,
    byte[] FotoJpeg,
    string? FotoContentType = null,
    string? IpAddress = null,
    LocalizacaoAssinatura? Localizacao = null,
    DateTime? CapturadaEm = null) : IRequest<DocumentoSignatarioDto>;

public class RegistrarAssinaturaTecnicoOsFacialCommandValidator : AbstractValidator<RegistrarAssinaturaTecnicoOsFacialCommand>
{
    public RegistrarAssinaturaTecnicoOsFacialCommandValidator()
    {
        RuleFor(x => x.DocumentoAssinaturaId).NotEmpty();
        RuleFor(x => x.FotoJpeg).NotEmpty();
    }
}

public class RegistrarAssinaturaTecnicoOsFacialCommandHandler : IRequestHandler<RegistrarAssinaturaTecnicoOsFacialCommand, DocumentoSignatarioDto>
{
    private readonly IAppDbContext _db;
    private readonly IAutenticacaoFacialService _autenticacaoFacial;
    private readonly IRegistradorAssinaturaService _registrador;

    public RegistrarAssinaturaTecnicoOsFacialCommandHandler(IAppDbContext db, IAutenticacaoFacialService autenticacaoFacial, IRegistradorAssinaturaService registrador)
    {
        _db = db;
        _autenticacaoFacial = autenticacaoFacial;
        _registrador = registrador;
    }

    public async Task<DocumentoSignatarioDto> Handle(RegistrarAssinaturaTecnicoOsFacialCommand request, CancellationToken ct)
    {
        var contexto = await DocumentoOsDoTecnico.CarregarNoEscopoAsync(_db, request.DocumentoAssinaturaId, ct);
        // Fora da integração, o técnico está no grupo facial da obra da OS. Dentro dela quem assina é o instrutor da
        // integração, que pode ser de outra obra: o rosto dele está no grupo facial da obra DELE (como na assinatura da turma).
        var obraDoGrupoFacial = contexto.ObraId;
        if (contexto.InstrutorIntegracaoId is { } instrutorId)
        {
            obraDoGrupoFacial = await _db.Trabalhadores.IgnoreQueryFilters()
                .Where(t => t.Id == instrutorId).Select(t => (Guid?)t.ObraId).FirstOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("Instrutor não encontrado.");
        }
        var identificacao = await _autenticacaoFacial.IdentificarAsync(obraDoGrupoFacial, request.FotoJpeg, ct);
        if (!identificacao.Aceito)
            throw new RejeicaoFacialException(identificacao.Motivo!.Value, MensagemRejeicaoFacial.Para(identificacao.Motivo));
        if (contexto.InstrutorIntegracaoId is { } esperado && identificacao.Resultado!.TrabalhadorId != esperado)
            throw new InvalidOperationException(TecnicoOrdemServicoRegra.MensagemNaoEhInstrutorFace);

        return await _registrador.RegistrarAsync(
            request.DocumentoAssinaturaId,
            identificacao.Resultado! with { CapturadaEmDeclarada = request.CapturadaEm },
            request.IpAddress, ct, request.FotoJpeg, request.FotoContentType,
            papel: Domain.Enums.PapelAssinatura.TecnicoSegurancaOs, localizacao: request.Localizacao);
    }
}
