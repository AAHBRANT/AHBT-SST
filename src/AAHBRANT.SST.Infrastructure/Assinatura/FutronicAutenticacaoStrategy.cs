using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AAHBRANT.SST.Infrastructure.Assinatura;

public class FutronicAutenticacaoStrategy : IAutenticacaoBiometriaLocalService
{
    private readonly IAppDbContext _db;
    private readonly IDispositivoAgenteAutenticador _dispositivoAutenticador;
    private readonly AssinaturaOptions _options;

    public FutronicAutenticacaoStrategy(IAppDbContext db, IDispositivoAgenteAutenticador dispositivoAutenticador, IOptions<AssinaturaOptions> options)
    {
        _db = db;
        _dispositivoAutenticador = dispositivoAutenticador;
        _options = options.Value;
    }

    public async Task<ResultadoAutenticacaoAssinatura> AutenticarPorMatchLocalAsync(
        Guid dispositivoId, string segredoDispositivo, Guid trabalhadorId, double score, LeituraBiometricaAssinada? leitura, CancellationToken ct,
        bool permitirInstrutorDeOutraObra = false)
    {
        var dispositivo = leitura is null
            ? await ValidarLeituraSemAssinaturaAsync(dispositivoId, segredoDispositivo, ct)
            : await ValidarLeituraAssinadaAsync(dispositivoId, leitura, trabalhadorId, score, ct);

        // Instrutor visitante: perfil ativo com autorização para assinar em outras obras. O filtro de escopo
        // do Trabalhador esconderia quem é de outra obra, então a busca o ignora só neste caso.
        var instrutorVisitante = permitirInstrutorDeOutraObra
            && await _db.PerfisInstrutor.AnyAsync(p => p.TrabalhadorId == trabalhadorId && p.PodeAssinarEmOutrasObras, ct);
        var consultaTrabalhador = instrutorVisitante
            ? _db.Trabalhadores.IgnoreQueryFilters().Where(t => t.Ativo)
            : _db.Trabalhadores;
        var trabalhador = await consultaTrabalhador.FirstOrDefaultAsync(t => t.Id == trabalhadorId, ct);
        if (trabalhador is null || (trabalhador.ObraId != dispositivo.ObraId && !instrutorVisitante))
        {
            throw new KeyNotFoundException("Trabalhador não encontrado.");
        }

        // A obra que precisa aceitar biometria é a do leitor onde a digital foi lida.
        var obra = await _db.Obras.FirstOrDefaultAsync(o => o.Id == dispositivo.ObraId, ct);
        if (obra is null || !obra.MetodosAutenticacaoHabilitados.HasFlag(MetodoAutenticacaoObra.Biometria))
        {
            throw new InvalidOperationException("Este método de assinatura não está habilitado para a obra deste trabalhador.");
        }

        if (trabalhador.TermoAceiteAssinaturaEletronicaEm is null || trabalhador.ConsentimentoBiometriaEm is null)
        {
            throw new InvalidOperationException("Trabalhador ainda não confirmou o Termo de Aceite de Assinatura Eletrônica e o consentimento LGPD para uso de biometria.");
        }

        if (score < _options.LimiarConfiancaBiometriaLocal)
        {
            throw new InvalidOperationException("Confiança do match biométrico abaixo do limiar exigido.");
        }

        return new ResultadoAutenticacaoAssinatura(trabalhador.Id, MetodoAutenticacaoAssinatura.Biometria, dispositivoId, Confianca: score);
    }

    // Leitura assinada: o trabalhador e a nota só valem se a assinatura do agente bater com a chave
    // registrada do leitor e o desafio for deste leitor, estiver no prazo e ainda não tiver sido usado.
    private async Task<DispositivoAgenteBiometrico> ValidarLeituraAssinadaAsync(
        Guid dispositivoId, LeituraBiometricaAssinada leitura, Guid trabalhadorId, double score, CancellationToken ct)
    {
        var dispositivo = await _db.DispositivosAgenteBiometrico.FirstOrDefaultAsync(d => d.Id == dispositivoId, ct)
            ?? throw new InvalidOperationException("Leitor de digital não encontrado ou revogado.");
        if (dispositivo.ChavePublicaAssinatura is null)
            throw new InvalidOperationException("Este leitor ainda não registrou a chave de assinatura. Reinicie o Agente Biométrico e tente de novo.");

        var desafio = Guid.TryParseExact(leitura.Desafio, "N", out var desafioId)
            ? await _db.DesafiosBiometria.FirstOrDefaultAsync(d => d.Id == desafioId, ct)
            : null;
        if (desafio is null || desafio.DispositivoId != dispositivoId)
            throw new InvalidOperationException("Leitura de digital inválida. Tente de novo.");
        if (desafio.UsadoEm is not null)
            throw new InvalidOperationException("Esta leitura de digital já foi usada. Faça uma nova leitura.");
        if (desafio.ExpiraEm < DateTime.UtcNow)
            throw new InvalidOperationException("A leitura de digital expirou. Faça uma nova leitura.");

        if (!leitura.Verificar(dispositivo.ChavePublicaAssinatura, trabalhadorId, score))
            throw new InvalidOperationException("Leitura de digital com assinatura inválida: ela não veio do leitor desta obra ou foi alterada.");

        // Gravado já, separado do registro da assinatura: mesmo que a assinatura falhe depois (ex.: já
        // assinou), o desafio não pode ser reaproveitado. Duas requisições simultâneas com o mesmo desafio
        // ainda podem passar juntas, mas as duas carregam a mesma leitura legítima.
        desafio.UsadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return dispositivo;
    }

    // Caminho antigo (agente ainda não atualizado): vale só enquanto o leitor não tiver chave registrada e a
    // exigência estiver desligada. Um leitor que já assina não aceita mais leitura sem assinatura.
    private async Task<DispositivoAgenteBiometrico> ValidarLeituraSemAssinaturaAsync(Guid dispositivoId, string segredoDispositivo, CancellationToken ct)
    {
        if (_options.ExigirLeituraAssinadaBiometria)
            throw new InvalidOperationException("Atualize o Agente Biométrico deste computador: leituras de digital sem assinatura não são mais aceitas.");

        var dispositivo = await _dispositivoAutenticador.ValidarAsync(dispositivoId, segredoDispositivo, ct);
        if (dispositivo.ChavePublicaAssinatura is not null)
            throw new InvalidOperationException("Este leitor usa leitura assinada. Recarregue a página e tente de novo.");
        return dispositivo;
    }
}
