using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Application.Assinatura;

// Texto curto do método de identificação usado numa assinatura, para a legenda miúda que acompanha
// cada assinatura nos PDFs ("Assinado em 02/10/2026 14:30 · biometria facial"). Pedido do usuário
// (02/10): toda assinatura de documento precisa deixar explícito data/hora e como foi autenticada.
// O dedo usado na digital NÃO é gravado no DocumentoSignatario, por isso o texto não afirma qual dedo.
public static class DescricaoMetodoAssinatura
{
    public static string Texto(MetodoAutenticacaoAssinatura metodo) => metodo switch
    {
        MetodoAutenticacaoAssinatura.Biometria => "biometria digital",
        MetodoAutenticacaoAssinatura.ReconhecimentoFacial => "biometria facial",
        MetodoAutenticacaoAssinatura.SessaoLogada => "usuário logado",
        _ => "assinatura eletrônica",
    };

    // Versão sem o "Assinado em", para células de tabela que já dizem "Assinado" na linha de cima.
    public static string LegendaCurta(DateTime horaBrasilia, MetodoAutenticacaoAssinatura? metodo) =>
        metodo is { } m
            ? $"{horaBrasilia:dd/MM/yyyy HH:mm} · {Texto(m)}"
            : $"{horaBrasilia:dd/MM/yyyy HH:mm}";

    // "Assinado em dd/MM/yyyy HH:mm · biometria facial" — horaBrasilia já convertida pelo chamador.
    public static string Legenda(DateTime horaBrasilia, MetodoAutenticacaoAssinatura? metodo) =>
        metodo is { } m
            ? $"Assinado em {horaBrasilia:dd/MM/yyyy HH:mm} · {Texto(m)}"
            : $"Assinado em {horaBrasilia:dd/MM/yyyy HH:mm}";
}
