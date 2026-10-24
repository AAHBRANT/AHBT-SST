using System.Text.RegularExpressions;

namespace AAHBRANT.SST.Domain.Entidades;

// Padrão de cada NR para o certificado de treinamento (pedido do usuário, 19/10): o número
// normalizado ("NR-35") é a chave que liga o curso ao seu modelo de certificado, e o conteúdo
// programático mínimo da norma entra por padrão quando o curso não tem um cadastrado.
// Texto montado a partir do conteúdo mínimo descrito em cada NR — precisa de validação do QSMS
// (as normas mudam de redação); CargaHorariaMinima/ValidadeEmMeses nulos onde a própria norma
// não fixa um valor único (dependem de função, grau de risco ou definição do empregador).
public sealed record NormaRegulamentadoraPadrao(
    string Numero,
    string Titulo,
    int? CargaHorariaMinima,
    int? ValidadeEmMeses,
    IReadOnlyList<string> ConteudoProgramatico)
{
    public string ConteudoProgramaticoTexto => string.Join('\n', ConteudoProgramatico);
}

public static partial class CatalogoNormasRegulamentadoras
{
    [GeneratedRegex(@"\bNR\s*[-–.]?\s*0*(\d{1,2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex PadraoNumeroNr();

    // "nr 35", "NR35", "NR-35 Trabalho em Altura", "NR-06" → "NR-35"/"NR-06". Nulo se não houver NR.
    public static string? NormalizarNumero(string? normaReferencia)
    {
        if (string.IsNullOrWhiteSpace(normaReferencia)) return null;
        var m = PadraoNumeroNr().Match(normaReferencia);
        return m.Success ? $"NR-{int.Parse(m.Groups[1].Value):00}" : null;
    }

    public static NormaRegulamentadoraPadrao? Obter(string? normaReferencia)
    {
        var numero = NormalizarNumero(normaReferencia);
        return numero is not null && Padroes.TryGetValue(numero, out var padrao) ? padrao : null;
    }

    public static IReadOnlyCollection<NormaRegulamentadoraPadrao> Todos => Padroes.Values.ToList();

    private static readonly IReadOnlyDictionary<string, NormaRegulamentadoraPadrao> Padroes =
        new NormaRegulamentadoraPadrao[]
        {
            new("NR-05", "Comissão Interna de Prevenção de Acidentes e de Assédio — CIPA", null, null,
            [
                "Estudo do ambiente, das condições de trabalho e dos riscos originados do processo produtivo",
                "Noções sobre acidentes e doenças relacionadas ao trabalho decorrentes das condições de trabalho e da exposição aos riscos existentes",
                "Metodologia de investigação e análise de acidentes e doenças relacionadas ao trabalho",
                "Princípios gerais de higiene do trabalho e de medidas de prevenção dos riscos",
                "Noções sobre a legislação trabalhista e previdenciária relativas à segurança e saúde no trabalho",
                "Noções sobre a inclusão de pessoas com deficiência e reabilitados nos processos de trabalho",
                "Organização da CIPA e outros assuntos necessários ao exercício das atribuições da comissão",
                "Prevenção e combate ao assédio sexual e a outras formas de violência no trabalho",
            ]),
            new("NR-06", "Equipamento de Proteção Individual — EPI", null, null,
            [
                "Finalidade e obrigatoriedade do uso do EPI",
                "EPI adequados aos riscos da função",
                "Uso adequado, guarda e conservação",
                "Higienização e manutenção periódica",
                "Limitações de uso e quando solicitar a substituição",
                "Certificado de Aprovação (CA) e responsabilidades do empregador e do trabalhador",
            ]),
            new("NR-10", "Segurança em Instalações e Serviços em Eletricidade", 40, 24,
            [
                "Introdução à segurança com eletricidade",
                "Riscos em instalações e serviços com eletricidade: choque elétrico, arcos elétricos, queimaduras, quedas e campos eletromagnéticos",
                "Técnicas de análise de risco",
                "Medidas de controle do risco elétrico: desenergização, aterramento, equipotencialização, seccionamento automático, dispositivos a corrente de fuga, extrabaixa tensão, barreiras e invólucros, bloqueios e impedimentos, obstáculos e anteparos, isolamento das partes vivas, isolação dupla ou reforçada, colocação fora de alcance e separação elétrica",
                "Normas técnicas brasileiras: NBR 5410, NBR 14039 e outras",
                "Regulamentações do MTE: NRs, NR-10, qualificação, habilitação, capacitação e autorização",
                "Equipamentos de proteção coletiva",
                "Equipamentos de proteção individual",
                "Rotinas de trabalho e procedimentos: instalações desenergizadas, liberação para serviços, sinalização e inspeções de áreas, serviços, ferramental e equipamentos",
                "Documentação de instalações elétricas",
                "Riscos adicionais: altura, ambientes confinados, áreas classificadas, umidade e condições atmosféricas",
                "Proteção e combate a incêndios: noções básicas, medidas preventivas, métodos de extinção e prática",
                "Acidentes de origem elétrica: causas diretas e indiretas e discussão de casos",
                "Primeiros socorros: noções sobre lesões, priorização do atendimento, respiração artificial, massagem cardíaca, remoção e transporte de acidentados e práticas",
                "Responsabilidades",
            ]),
            new("NR-12", "Segurança no Trabalho em Máquinas e Equipamentos", null, null,
            [
                "Descrição e identificação dos riscos associados a cada máquina e equipamento e as proteções específicas contra cada um deles",
                "Funcionamento das proteções: como e por que devem ser usadas",
                "Como e em que circunstâncias uma proteção pode ser removida, e por quem",
                "O que fazer se uma proteção for danificada ou perder sua função",
                "Princípios de segurança na utilização da máquina ou equipamento",
                "Segurança para riscos mecânicos, elétricos e outros relevantes",
                "Método de trabalho seguro",
                "Permissão de trabalho",
                "Sistema de bloqueio de funcionamento da máquina durante inspeção, limpeza, lubrificação e manutenção",
            ]),
            new("NR-18", "Segurança e Saúde no Trabalho na Indústria da Construção", 4, null,
            [
                "Condições e meio ambiente de trabalho no canteiro de obras",
                "Riscos inerentes às atividades desenvolvidas e à função",
                "Equipamentos e sistemas de proteção coletiva existentes no canteiro",
                "Uso adequado dos equipamentos de proteção individual",
                "Programas de segurança e saúde da obra (PGR)",
                "Procedimentos em situações de emergência",
            ]),
            new("NR-33", "Segurança e Saúde nos Trabalhos em Espaços Confinados — Trabalhador Autorizado e Vigia", 16, 12,
            [
                "Definições",
                "Reconhecimento, avaliação e controle de riscos",
                "Funcionamento de equipamentos utilizados",
                "Procedimentos e utilização da Permissão de Entrada e Trabalho (PET)",
                "Noções de resgate e primeiros socorros",
            ]),
            new("NR-35", "Trabalho em Altura", 8, 24,
            [
                "Normas e regulamentos aplicáveis ao trabalho em altura",
                "Análise de risco e condições impeditivas",
                "Riscos potenciais inerentes ao trabalho em altura e medidas de prevenção e controle",
                "Sistemas, equipamentos e procedimentos de proteção coletiva",
                "Equipamentos de Proteção Individual para trabalho em altura: seleção, inspeção, conservação e limitação de uso",
                "Acidentes típicos em trabalhos em altura",
                "Condutas em situações de emergência, incluindo noções de técnicas de resgate e de primeiros socorros",
            ]),
        }.ToDictionary(p => p.Numero);
}
