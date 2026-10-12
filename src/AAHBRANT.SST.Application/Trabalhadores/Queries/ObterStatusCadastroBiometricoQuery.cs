using AAHBRANT.SST.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AAHBRANT.SST.Application.Trabalhadores.Queries;

public class StatusCadastroBiometricoDto
{
    public DateTime? DigitalCadastradaEm { get; set; }
    public DateTime? FacialCadastradoEm { get; set; }
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
        var digital = await _db.TemplatesBiometricoFutronic
            .Where(tb => tb.TrabalhadorId == request.TrabalhadorId)
            .OrderBy(tb => tb.CapturadoEm)
            .Select(tb => (DateTime?)tb.CapturadoEm)
            .FirstOrDefaultAsync(ct);
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
            DigitalCadastradaEm = digital,
            FacialCadastradoEm = facial,
            ConsentimentoBiometriaEm = consentimento?.ConsentimentoBiometriaEm,
            ConsentimentoBiometriaVersao = consentimento?.ConsentimentoBiometriaVersao,
            ConsentimentoBiometriaRevogadoEm = consentimento?.ConsentimentoBiometriaRevogadoEm,
        };
    }
}
