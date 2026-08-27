using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    public static readonly (string Funcao, int Quantidade, string[] CodigosCursos)[] DistribuicaoFuncoes =
    {
        ("Servente", 45, new[] { "NR-06", "NR-18" }),
        ("Pedreiro", 35, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Armador", 20, new[] { "NR-06", "NR-18", "NR-12" }),
        ("Carpinteiro", 18, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Eletricista", 12, new[] { "NR-06", "NR-10", "NR-35" }),
        ("Encanador", 10, new[] { "NR-06", "NR-18" }),
        ("Pintor", 10, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Soldador", 8, new[] { "NR-06", "NR-18", "NR-12" }),
        ("Operador de Grua/Betoneira", 8, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Mestre de Obras", 4, new[] { "NR-06", "NR-18", "NR-35" }),
        (FuncaoEncarregado, 10, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Técnico de Segurança do Trabalho", 4, new[] { "NR-06", "NR-18", "NR-33" }),
        ("Engenheiro Civil", 6, new[] { "NR-06", "NR-18" }),
        ("Almoxarife", 3, new[] { "NR-06", "NR-11" }),
        ("Vigia/Porteiro", 7, new[] { "NR-06" }),
    };

    public static readonly (string Codigo, string Nome, string NormaReferencia, int CargaHorariaMinima, int ValidadeEmMeses)[] CatalogoCursosNr =
    {
        ("NR-06", "NR-06 Equipamento de Proteção Individual", "NR-06", 4, 24),
        ("NR-10", "NR-10 Segurança em Instalações e Serviços em Eletricidade", "NR-10", 40, 24),
        ("NR-11", "NR-11 Transporte, Movimentação, Armazenagem e Manuseio de Materiais", "NR-11", 16, 24),
        ("NR-12", "NR-12 Segurança no Trabalho em Máquinas e Equipamentos", "NR-12", 8, 12),
        ("NR-18", "NR-18 Condições e Meio Ambiente de Trabalho na Construção", "NR-18", 8, 12),
        ("NR-33", "NR-33 Segurança e Saúde nos Trabalhos em Espaços Confinados", "NR-33", 16, 12),
        ("NR-35", "NR-35 Trabalho em Altura", "NR-35", 8, 24),
    };

    public static readonly (string Nome, string Fabricante, string CertificadoAprovacaoNumero, int VidaUtilEmMeses, int SaldoEstoque)[] CatalogoEpisPadrao =
    {
        ("Capacete de Segurança Classe B", "3M", "CA-31469", 60, 40),
        ("Cinto de Segurança Tipo Paraquedista", "Talabart", "CA-38200", 36, 0),
        ("Luva de Vaqueta", "Danny", "CA-11845", 6, 120),
        ("Bota de Segurança com Bico de Aço", "Vulcabras", "CA-40129", 12, 3),
        ("Protetor Auricular Tipo Plug", "3M", "CA-5745", 4, 200),
        ("Óculos de Proteção Ampla Visão", "Steel Pro", "CA-25763", 12, 0),
        ("Máscara Respiratória PFF2", "3M", "CA-34972", 2, 500),
    };

    // Dados de demonstração da Matriz de EPI por Função (Fase 1) — cada obra real define sua
    // própria matriz depois do deploy; isto só preenche o seeder de obra mocada para a tela e o
    // filtro terem o que mostrar. Funções com NR-35 (trabalho em altura) em DistribuicaoFuncoes
    // recebem também o cinto de segurança.
    public static readonly (string Funcao, string[] Epis)[] MatrizEpiPorFuncao =
    {
        ("Servente", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Pedreiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Cinto de Segurança Tipo Paraquedista" }),
        ("Armador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão" }),
        ("Carpinteiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Protetor Auricular Tipo Plug", "Cinto de Segurança Tipo Paraquedista" }),
        ("Eletricista", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Cinto de Segurança Tipo Paraquedista" }),
        ("Encanador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Pintor", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Máscara Respiratória PFF2", "Óculos de Proteção Ampla Visão", "Cinto de Segurança Tipo Paraquedista" }),
        ("Soldador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Protetor Auricular Tipo Plug" }),
        ("Operador de Grua/Betoneira", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Protetor Auricular Tipo Plug" }),
        ("Mestre de Obras", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Cinto de Segurança Tipo Paraquedista" }),
        (FuncaoEncarregado, new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Cinto de Segurança Tipo Paraquedista" }),
        ("Técnico de Segurança do Trabalho", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Engenheiro Civil", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Almoxarife", new[] { "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Vigia/Porteiro", new[] { "Bota de Segurança com Bico de Aço" }),
    };

    public static readonly (StatusNaoConformidade Status, int Quantidade)[] DistribuicaoNaoConformidades =
    {
        (StatusNaoConformidade.Aberta, 8),
        (StatusNaoConformidade.EmTratamento, 7),
        (StatusNaoConformidade.AguardandoValidacao, 4),
        (StatusNaoConformidade.Encerrada, 6),
    };

    private static readonly string[] PrimeirosNomes =
    {
        "João", "Maria", "Carlos", "Ana", "Pedro", "Paulo", "Marcos", "Lucas", "Rafael", "Fernanda",
        "Juliana", "Bruno", "Diego", "Felipe", "Gabriel", "Renata", "Patrícia", "Rodrigo", "Sandra", "Vitor",
    };

    private static readonly string[] Sobrenomes =
    {
        "Silva", "Souza", "Oliveira", "Santos", "Pereira", "Costa", "Rodrigues", "Almeida", "Nascimento", "Lima",
        "Araújo", "Fernandes", "Carvalho", "Gomes", "Martins", "Rocha", "Ribeiro", "Alves", "Monteiro", "Cardoso",
    };

    public static string GerarNome(int indice)
    {
        var primeiro = PrimeirosNomes[indice % PrimeirosNomes.Length];
        var sobrenome1 = Sobrenomes[(indice / PrimeirosNomes.Length) % Sobrenomes.Length];
        var sobrenome2 = Sobrenomes[(indice * 7 + 3) % Sobrenomes.Length];
        return $"{primeiro} {sobrenome1} {sobrenome2}";
    }
}
