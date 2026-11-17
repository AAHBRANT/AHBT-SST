using System.Security.Cryptography;
using AAHBRANT.SST.Application.Assinatura;
using AAHBRANT.SST.Application.Common.Interfaces;
using AAHBRANT.SST.Domain.Entidades;

namespace AAHBRANT.SST.Application.SessoesTreinamento;

// Evidência da presença na turma (05/11, pedido do usuário): guarda na hora o que a entrega de EPI já
// guarda — imagem capturada (rosto ou digital) com SHA-256, IP, aparelho, localização e a validação
// (Azure ou leitor). No encerramento vai para a assinatura do certificado (EncerrarSessaoTreinamentoCommand).
public static class EvidenciaPresencaTreinamento
{
    public static void Aplicar(
        ParticipanteSessaoTreinamento participante,
        ResultadoAutenticacaoAssinatura resultado,
        byte[]? imagem,
        string? contentType,
        LocalizacaoAssinatura? localizacao,
        IClienteIpProvider? clienteIp)
    {
        if (imagem is { Length: > 0 })
        {
            participante.EvidenciaConteudo = imagem;
            participante.EvidenciaContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
            participante.EvidenciaHash = Convert.ToHexString(SHA256.HashData(imagem));
        }

        participante.IpAddress = Truncar(clienteIp?.ObterIp(), 64);
        participante.UserAgent = Truncar(clienteIp?.ObterUserAgent(), 300);
        var local = (localizacao ?? new LocalizacaoAssinatura(Domain.Enums.StatusLocalizacaoAssinatura.NaoInformada)).Normalizada();
        participante.LocalizacaoStatus = local.Status;
        participante.Latitude = local.Latitude;
        participante.Longitude = local.Longitude;
        participante.PrecisaoMetros = local.PrecisaoMetros;
        participante.DispositivoAgenteId = resultado.DispositivoAgenteId;
        participante.ValidacaoModelo = Truncar(resultado.ValidacaoModelo, 60);
        participante.ValidacaoGrupoId = Truncar(resultado.ValidacaoGrupoId, 80);
        participante.ValidacaoRequisicaoId = Truncar(resultado.ValidacaoRequisicaoId, 80);
    }

    // Mesma prova, para a assinatura biométrica do instrutor na turma (16/11): imagem com SHA-256, IP, aparelho,
    // localização e validação (Azure ou leitor).
    public static void AplicarNaAssinaturaInstrutor(
        AssinaturaInstrutorSessaoTreinamento assinatura,
        ResultadoAutenticacaoAssinatura resultado,
        byte[]? imagem,
        string? contentType,
        LocalizacaoAssinatura? localizacao,
        IClienteIpProvider? clienteIp)
    {
        if (imagem is { Length: > 0 })
        {
            assinatura.EvidenciaConteudo = imagem;
            assinatura.EvidenciaContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType;
            assinatura.EvidenciaHash = Convert.ToHexString(SHA256.HashData(imagem));
        }

        assinatura.IpAddress = Truncar(clienteIp?.ObterIp(), 64);
        assinatura.UserAgent = Truncar(clienteIp?.ObterUserAgent(), 300);
        var local = (localizacao ?? new LocalizacaoAssinatura(Domain.Enums.StatusLocalizacaoAssinatura.NaoInformada)).Normalizada();
        assinatura.LocalizacaoStatus = local.Status;
        assinatura.Latitude = local.Latitude;
        assinatura.Longitude = local.Longitude;
        assinatura.PrecisaoMetros = local.PrecisaoMetros;
        assinatura.DispositivoAgenteId = resultado.DispositivoAgenteId;
        assinatura.ValidacaoModelo = Truncar(resultado.ValidacaoModelo, 60);
        assinatura.ValidacaoGrupoId = Truncar(resultado.ValidacaoGrupoId, 80);
        assinatura.ValidacaoRequisicaoId = Truncar(resultado.ValidacaoRequisicaoId, 80);
    }

    private static string? Truncar(string? valor, int max) =>
        string.IsNullOrEmpty(valor) || valor.Length <= max ? valor : valor[..max];
}
