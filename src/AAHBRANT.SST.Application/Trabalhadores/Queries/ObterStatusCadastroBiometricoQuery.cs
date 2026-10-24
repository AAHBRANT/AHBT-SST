using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public record DigitalCadastradaDto(DedoBiometria Dedo, DateTime CadastradaEm);

public class StatusCadastroBiometricoDto
{
    public DateTime? DigitalCadastradaEm { get; set; }
    public DateTime? FacialCadastradoEm { get; set; }
    // Uma linha por digital cadastrada, com o dedo (21/10: polegar direito e esquerdo separados).
    public List<DigitalCadastradaDto> Digitais { get; set; } = new();
    public bool TemDigital => DigitalCadastradaEm is not null;
    public bool TemFacial => FacialCadastradoEm is not null;
    // Consentimento LGPD da biometria: quando foi dado, em qual versão do termo e se foi revogado.
    public DateTime? ConsentimentoBiometriaEm { get; set; }
    public string? ConsentimentoBiometriaVersao { get; set; }
    public DateTime? ConsentimentoBiometriaRevogadoEm { get; set; }
}

// Diz se a digital e o facial do trabalhador já foram cadastrados com sucesso — a aba de assinatura
// usa isto para esconder os botões de cadastro (regra do usuário, 30/09: cadastro uma única vez).
public record ObterStatusCadastroBiometricoQuery(Guid TrabalhadorId) : IRequest<StatusCadastroBiometricoDto>;

public class ObterStatusCadastroBiometricoQueryHandler : IRequestHandler<ObterStatusCadastroBiometricoQuery, StatusCadastroBiometricoDto>
{
    private readonly IAppDbContext _db;

    public ObterStatusCadastroBiometricoQueryHandler(IAppDbContext db) => _db = db;

    public async Task<StatusCadastroBiometricoDto> Handle(ObterStatusCadastroBiometricoQuery request, CancellationToken ct)
    {
        var digitais = await _db.TemplatesBiometricoFutronic
            .Where(tb => tb.TrabalhadorId == request.TrabalhadorId)
            .OrderBy(tb => tb.CapturadoEm)
            .Select(tb => new DigitalCadastradaDto(tb.Dedo, tb.CapturadoEm))
            .ToListAsync(ct);
        var facial = await _db.FotosCadastroFacial
            .Where(f => f.TrabalhadorId == request.TrabalhadorId)
            .OrderBy(f => f.CapturadaEm)
            .Select(f => (DateTime?)f.CapturadaEm)
            .FirstOrDefaultAsync(ct);

        var consentimento = await _db.Trabalhadores
            .Where(t => t.Id == request.TrabalhadorId)
            .Select(t => new { t.ConsentimentoBiometriaEm, t.ConsentimentoBiometriaVersao, t.ConsentimentoBiometriaRevogadoEm })
            .FirstOrDefaultAsync(ct);

        return new StatusCadastroBiometricoDto
        {
            DigitalCadastradaEm = digitais.Count > 0 ? digitais[0].CadastradaEm : null,
            Digitais = digitais,
            FacialCadastradoEm = facial,
            ConsentimentoBiometriaEm = consentimento?.ConsentimentoBiometriaEm,
            ConsentimentoBiometriaVersao = consentimento?.ConsentimentoBiometriaVersao,
            ConsentimentoBiometriaRevogadoEm = consentimento?.ConsentimentoBiometriaRevogadoEm,
        };
    }
}
