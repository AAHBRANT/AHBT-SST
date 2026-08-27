using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

public static partial class MockObraSeeder
{
    // Distribuição de funções derivada do documento de referência "Matriz EPI x NR x Funções"
    // (fornecido pelo usuário em 2026-08-27, baseado em 7 grupos ocupacionais reais de canteiro de
    // obras). "Encarregado" fica como primeiro item de propósito: a soma das quantidades ANTES dele
    // precisa ser múltiplo de 10 equipes (ver
    // MockObraSeederDadosEstaticosTests.DistribuicaoFuncoes_QuantidadeAntesDeEncarregado_DeveSerMultiploDeDezEquipes)
    // — ficando em primeiro, essa soma é sempre 0, então a posição das demais 73 funções é livre.
    public static readonly (string Funcao, int Quantidade, string[] CodigosCursos)[] DistribuicaoFuncoes =
    {
        // Encarregado genérico — 1 por equipe (10 equipes), usado pelo vínculo Equipe.EncarregadoId.
        (FuncaoEncarregado, 10, new[] { "NR-06", "NR-18", "NR-35" }),

        // Grupo 1 — Produção/Pavimentação (sem trabalho em altura)
        ("Ajudante de Obras", 22, new[] { "NR-06", "NR-18" }),
        ("Pedreiro", 16, new[] { "NR-06", "NR-18" }),
        ("Armador", 9, new[] { "NR-06", "NR-18", "NR-12" }),
        ("Carpinteiro", 8, new[] { "NR-06", "NR-18", "NR-12" }),
        ("Greidista / Nivelador", 3, new[] { "NR-06", "NR-18", "NR-11" }),
        ("Rasteleiro", 4, new[] { "NR-06", "NR-18" }),
        ("Mesista", 3, new[] { "NR-06", "NR-18" }),
        ("Marteleteiro", 2, new[] { "NR-06", "NR-18" }),
        ("Manguerista", 3, new[] { "NR-06", "NR-18" }),
        ("Bandeirinha (Homem Bandeira)", 3, new[] { "NR-06", "NR-11", "NR-18" }),
        ("Sinaleiro", 3, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Operador de Betoneira Elétrica", 2, new[] { "NR-06", "NR-12", "NR-18" }),
        ("Operador de Selagem de Asfálticas I", 2, new[] { "NR-06", "NR-12", "NR-18" }),
        ("Operador de Selagem de Asfálticas II", 2, new[] { "NR-06", "NR-12", "NR-18" }),
        ("Operador de Selagem de Asfálticas III", 1, new[] { "NR-06", "NR-12", "NR-18" }),

        // Grupo 2 — Trabalho em Altura
        ("Ajudante de Obras (Trabalho em Altura)", 4, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Armador (Trabalho em Altura)", 3, new[] { "NR-06", "NR-18", "NR-35", "NR-12" }),
        ("Carpinteiro (Trabalho em Altura)", 3, new[] { "NR-06", "NR-18", "NR-35", "NR-12" }),
        ("Montador e Desmontador de Andaimes", 3, new[] { "NR-06", "NR-18", "NR-35" }),

        // Grupo 3 — Elétrica
        ("Eletricista", 5, new[] { "NR-06", "NR-10", "NR-35" }),
        ("Auxiliar de Eletricista", 3, new[] { "NR-06", "NR-10" }),
        ("Eletricista de Veículos e Máquinas", 2, new[] { "NR-06", "NR-10", "NR-12" }),

        // Grupo 4 — Mecânica/Solda
        ("Mecânico", 4, new[] { "NR-06", "NR-12" }),
        ("Auxiliar de Mecânico", 3, new[] { "NR-06", "NR-12" }),
        ("Soldador", 4, new[] { "NR-06", "NR-12", "NR-18" }),
        ("Borracheiro", 2, new[] { "NR-06", "NR-12" }),
        ("Motorista Lubrificador", 2, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Líder de Lubrificação", 1, new[] { "NR-06", "NR-11", "NR-12" }),

        // Grupo 5 — Operadores/Veículos
        ("Operador de Máquinas", 4, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Operador de Vibroacabadora", 1, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Operador de Fresa", 1, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Operador de Motoniveladora de Acabamento", 1, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Motorista de Caminhão", 4, new[] { "NR-06", "NR-11" }),
        ("Motorista Carreteiro", 2, new[] { "NR-06", "NR-11" }),
        ("Motorista Espargidor", 1, new[] { "NR-06", "NR-11" }),
        ("Motorista Operador de Munck", 2, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Motorista Coletivo", 1, new[] { "NR-06", "NR-11" }),
        ("Motorista (Geral)", 3, new[] { "NR-06", "NR-11" }),

        // Grupo 6 — Gestão/Engenharia/Laboratório/Topografia/SST
        ("Encarregado de Carpintaria", 1, new[] { "NR-06", "NR-18", "NR-35" }),
        ("Encarregado de Produção", 1, new[] { "NR-06", "NR-18" }),
        ("Encarregado de Pavimentação / Pleno", 1, new[] { "NR-06", "NR-18", "NR-12" }),
        ("Encarregado de Drenagem", 1, new[] { "NR-06", "NR-18", "NR-11" }),
        ("Encarregado de Sinalização", 1, new[] { "NR-06", "NR-11" }),
        ("Encarregado de Terraplanagem", 1, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Encarregado Mecânica", 1, new[] { "NR-06", "NR-12", "NR-33" }),
        ("Encarregado de Topografia", 1, new[] { "NR-06", "NR-11" }),
        ("Engenheiro Civil", 2, new[] { "NR-06", "NR-18" }),
        ("Engenheiro Mecânico", 1, new[] { "NR-06", "NR-12" }),
        ("Engenheiro de Qualidade", 1, new[] { "NR-06", "NR-18" }),
        ("Engenheiro Orçamentista", 1, new[] { "NR-06" }),
        ("Engenheiro de Planejamento", 1, new[] { "NR-06" }),
        ("Engenheiro de Segurança do Trabalho", 1, new[] { "NR-06", "NR-18", "NR-33", "NR-35" }),
        ("Técnico de Segurança do Trabalho", 2, new[] { "NR-06", "NR-18", "NR-33" }),
        ("Auxiliar de Segurança do Trabalho", 2, new[] { "NR-06", "NR-18" }),
        ("Técnico em Meio Ambiente", 1, new[] { "NR-06", "NR-18" }),
        ("Técnico de Medição", 1, new[] { "NR-06", "NR-18" }),
        ("Assistente de Medição", 1, new[] { "NR-06", "NR-18" }),
        ("Laboratorista", 2, new[] { "NR-06", "NR-18" }),
        ("Topógrafo", 2, new[] { "NR-06", "NR-11" }),
        ("Apontador de Obra", 2, new[] { "NR-06", "NR-18" }),
        ("Controle de Serviços de Máquinas e Veículos", 1, new[] { "NR-06", "NR-11", "NR-12" }),
        ("Estagiário de Engenharia", 2, new[] { "NR-06", "NR-18" }),

        // Grupo 7 — Apoio/Limpeza/Administrativo
        ("Faxineiro(a)", 4, new[] { "NR-06" }),
        ("Auxiliar de Manutenção Geral", 2, new[] { "NR-06", "NR-12" }),
        ("Almoxarife", 2, new[] { "NR-06", "NR-11" }),
        ("Auxiliar Administrativo de Obras", 2, new[] { "NR-06" }),
        ("Encarregado Administrativo de Obras", 1, new[] { "NR-06" }),
        ("Auxiliar Administrativo de RH", 1, new[] { "NR-06" }),
        ("Assistente Financeiro", 1, new[] { "NR-06" }),
        ("Analista Financeiro", 1, new[] { "NR-06" }),
        ("Analista de Recrutamento e Seleção", 1, new[] { "NR-06" }),
        ("Analista de Planejamento de Manutenção", 1, new[] { "NR-06" }),
        ("Recepcionista", 1, new[] { "NR-06" }),
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

    // Catálogo expandido a partir do documento de referência "Matriz EPI x NR x Funções" — cobre os
    // tipos de EPI específicos citados pelos 7 grupos ocupacionais (produção civil, altura, elétrica,
    // mecânica/solda, operadores/veículos, gestão/engenharia/laboratório/topografia/SST,
    // administrativo/apoio). Os 4 itens com SaldoEstoque <= 3 simulam alerta de estoque crítico.
    // Cuidado: MockObraSeeder.NaoConformidadesEEpi.cs localiza o EPI de entrega padrão via
    // `.Single(e => e.Nome.Contains("Capacete"))` / `.Contains("Bota")` — nenhum outro item deste
    // catálogo pode conter essas substrings no nome (por isso os itens de calçado adicionais usam
    // "Calçado", não "Bota").
    public static readonly (string Nome, string Fabricante, string CertificadoAprovacaoNumero, int VidaUtilEmMeses, int SaldoEstoque)[] CatalogoEpisPadrao =
    {
        ("Capacete de Segurança Classe B", "3M", "CA-31469", 60, 40),
        ("Cinto de Segurança Tipo Paraquedista", "Talabart", "CA-38200", 36, 0),
        ("Luva de Vaqueta", "Danny", "CA-11845", 6, 120),
        ("Bota de Segurança com Bico de Aço", "Vulcabras", "CA-40129", 12, 3),
        ("Protetor Auricular Tipo Plug", "3M", "CA-5745", 4, 200),
        ("Óculos de Proteção Ampla Visão", "Steel Pro", "CA-25763", 12, 0),
        ("Máscara Respiratória PFF2", "3M", "CA-34972", 2, 500),
        ("Talabarte Duplo Tipo Y com Absorvedor de Energia", "Krisbow", "CA-33711", 24, 15),
        ("Trava-Quedas Retrátil", "MSA", "CA-36288", 60, 8),
        ("Luva Isolante de Borracha Classe 0", "Vulcan", "CA-29144", 6, 25),
        ("Luva de Cobertura em Couro para Isolante", "Danny", "CA-19042", 12, 25),
        ("Protetor Facial contra Arco Elétrico", "3M", "CA-37650", 24, 6),
        ("Vestimenta Retardante de Chama", "Sinter", "CA-40877", 12, 10),
        ("Máscara de Solda com Filtro de Escurecimento Automático", "ESAB", "CA-21390", 36, 5),
        ("Avental de Raspa de Couro", "Danny", "CA-14022", 12, 20),
        ("Mangote de Raspa de Couro", "Danny", "CA-14023", 12, 20),
        ("Perneira de Raspa de Couro", "Danny", "CA-14024", 12, 20),
        ("Respirador Semifacial Combinado VO/P2", "3M", "CA-35110", 6, 40),
        ("Luva Nitrílica", "Danny", "CA-22087", 3, 150),
        ("Luva de PVC Impermeável", "Volk", "CA-17654", 6, 90),
        ("Calçado Impermeável de PVC Cano Longo", "Vulcabras", "CA-28933", 12, 30),
        ("Calçado Isolante Elétrico Sem Componentes Metálicos", "Vulcabras", "CA-31980", 12, 2),
        ("Jaleco de Manga Longa", "Brastex", "CA-24501", 12, 15),
        ("Avental Impermeável de Laboratório", "Volk", "CA-26890", 12, 12),
        ("Colete Refletivo Classe 2", "Vicsa", "CA-18877", 12, 25),
    };

    // Dados de demonstração da Matriz de EPI por Função (Fase 1) — cada obra real define sua própria
    // matriz depois do deploy; isto só preenche o seeder de obra mocada para a tela e o filtro terem
    // o que mostrar. Funções puramente administrativas/de escritório (RH, Financeiro, Recrutamento,
    // Planejamento, Recepção) ficam de fora de propósito — não têm EPI aplicável e a ausência de
    // entrada aqui reflete isso na tela da Matriz.
    public static readonly (string Funcao, string[] Epis)[] MatrizEpiPorFuncao =
    {
        (FuncaoEncarregado, new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Cinto de Segurança Tipo Paraquedista", "Colete Refletivo Classe 2" }),

        ("Ajudante de Obras", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão" }),
        ("Pedreiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão" }),
        ("Armador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão" }),
        ("Carpinteiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Protetor Auricular Tipo Plug" }),
        ("Greidista / Nivelador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Colete Refletivo Classe 2" }),
        ("Rasteleiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Máscara Respiratória PFF2" }),
        ("Mesista", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Máscara Respiratória PFF2" }),
        ("Marteleteiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Protetor Auricular Tipo Plug", "Óculos de Proteção Ampla Visão" }),
        ("Manguerista", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de PVC Impermeável", "Óculos de Proteção Ampla Visão", "Protetor Auricular Tipo Plug" }),
        ("Bandeirinha (Homem Bandeira)", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Sinaleiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2", "Protetor Auricular Tipo Plug" }),
        ("Operador de Betoneira Elétrica", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão", "Protetor Auricular Tipo Plug" }),
        ("Operador de Selagem de Asfálticas I", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Máscara Respiratória PFF2", "Óculos de Proteção Ampla Visão" }),
        ("Operador de Selagem de Asfálticas II", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Máscara Respiratória PFF2", "Óculos de Proteção Ampla Visão" }),
        ("Operador de Selagem de Asfálticas III", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Máscara Respiratória PFF2", "Óculos de Proteção Ampla Visão" }),

        ("Ajudante de Obras (Trabalho em Altura)", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Cinto de Segurança Tipo Paraquedista", "Talabarte Duplo Tipo Y com Absorvedor de Energia" }),
        ("Armador (Trabalho em Altura)", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Cinto de Segurança Tipo Paraquedista", "Talabarte Duplo Tipo Y com Absorvedor de Energia" }),
        ("Carpinteiro (Trabalho em Altura)", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Cinto de Segurança Tipo Paraquedista", "Talabarte Duplo Tipo Y com Absorvedor de Energia" }),
        ("Montador e Desmontador de Andaimes", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Cinto de Segurança Tipo Paraquedista", "Trava-Quedas Retrátil" }),

        ("Eletricista", new[] { "Capacete de Segurança Classe B", "Calçado Isolante Elétrico Sem Componentes Metálicos", "Luva Isolante de Borracha Classe 0", "Luva de Cobertura em Couro para Isolante", "Óculos de Proteção Ampla Visão", "Cinto de Segurança Tipo Paraquedista" }),
        ("Auxiliar de Eletricista", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva Isolante de Borracha Classe 0", "Luva de Cobertura em Couro para Isolante", "Óculos de Proteção Ampla Visão" }),
        ("Eletricista de Veículos e Máquinas", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva Isolante de Borracha Classe 0", "Luva de Cobertura em Couro para Isolante", "Óculos de Proteção Ampla Visão", "Protetor Facial contra Arco Elétrico" }),

        ("Mecânico", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva Nitrílica", "Óculos de Proteção Ampla Visão", "Protetor Auricular Tipo Plug" }),
        ("Auxiliar de Mecânico", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva Nitrílica", "Óculos de Proteção Ampla Visão" }),
        ("Soldador", new[] { "Bota de Segurança com Bico de Aço", "Máscara de Solda com Filtro de Escurecimento Automático", "Avental de Raspa de Couro", "Mangote de Raspa de Couro", "Perneira de Raspa de Couro" }),
        ("Borracheiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta", "Óculos de Proteção Ampla Visão" }),
        ("Motorista Lubrificador", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva Nitrílica", "Óculos de Proteção Ampla Visão" }),
        ("Líder de Lubrificação", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva Nitrílica", "Óculos de Proteção Ampla Visão" }),

        ("Operador de Máquinas", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Protetor Auricular Tipo Plug", "Óculos de Proteção Ampla Visão", "Colete Refletivo Classe 2" }),
        ("Operador de Vibroacabadora", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Protetor Auricular Tipo Plug", "Óculos de Proteção Ampla Visão", "Máscara Respiratória PFF2" }),
        ("Operador de Fresa", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Protetor Auricular Tipo Plug", "Óculos de Proteção Ampla Visão", "Máscara Respiratória PFF2" }),
        ("Operador de Motoniveladora de Acabamento", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Protetor Auricular Tipo Plug", "Óculos de Proteção Ampla Visão" }),
        ("Motorista de Caminhão", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Motorista Carreteiro", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Motorista Espargidor", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2", "Luva de PVC Impermeável" }),
        ("Motorista Operador de Munck", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2", "Luva de Vaqueta" }),
        ("Motorista Coletivo", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Motorista (Geral)", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),

        ("Encarregado de Carpintaria", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Cinto de Segurança Tipo Paraquedista" }),
        ("Encarregado de Produção", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Encarregado de Pavimentação / Pleno", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Encarregado de Drenagem", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de PVC Impermeável" }),
        ("Encarregado de Sinalização", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Encarregado de Terraplanagem", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Encarregado Mecânica", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva Nitrílica" }),
        ("Encarregado de Topografia", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Engenheiro Civil", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Engenheiro Mecânico", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Engenheiro de Qualidade", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Engenheiro Orçamentista", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Engenheiro de Planejamento", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Engenheiro de Segurança do Trabalho", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Cinto de Segurança Tipo Paraquedista" }),
        ("Técnico de Segurança do Trabalho", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Auxiliar de Segurança do Trabalho", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Técnico em Meio Ambiente", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Técnico de Medição", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Assistente de Medição", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Laboratorista", new[] { "Bota de Segurança com Bico de Aço", "Jaleco de Manga Longa", "Avental Impermeável de Laboratório", "Luva Nitrílica" }),
        ("Topógrafo", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Apontador de Obra", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Controle de Serviços de Máquinas e Veículos", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Colete Refletivo Classe 2" }),
        ("Estagiário de Engenharia", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),

        ("Faxineiro(a)", new[] { "Bota de Segurança com Bico de Aço", "Luva de PVC Impermeável", "Luva Nitrílica" }),
        ("Auxiliar de Manutenção Geral", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Almoxarife", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço", "Luva de Vaqueta" }),
        ("Auxiliar Administrativo de Obras", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
        ("Encarregado Administrativo de Obras", new[] { "Capacete de Segurança Classe B", "Bota de Segurança com Bico de Aço" }),
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
