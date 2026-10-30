using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Temas de inspeção padrão, globais (valem para todas as obras), com checklist baseado nas NRs —
// pedido do usuário em 2026-10-30, no máximo 20 itens por tema. Mesmo formato do tema criado pela
// tela (CriarTemaInspecaoCommand): a foto por item segue a regra do Tema, não o ExigeFotografia.
// Idempotente pelo nome do tema (sem diferenciar maiúsculas, incluindo excluídos): se o QSMS já
// criou ou excluiu um tema com o mesmo nome pela tela, ele não é inserido de volta.
// Condições sanitárias ficou de fora: já existe o tema do Canteiro criado pelo técnico.
// Texto genérico das NRs; itens que dependem de IT do Corpo de Bombeiros ou de projeto
// (distâncias, quantidades) ficam como "conforme ..." — QSMS valida antes do uso em auditoria.
public static class TemasInspecaoPadraoSeeder
{
    public const int MaximoItensPorTema = 20;

    private sealed record TemaPadrao(string Nome, int? PeriodicidadeDias, (string Secao, string Descricao)[] Itens);

    private static TemaPadrao[] Temas() => new[]
    {
        new TemaPadrao("Extintores", 30, new[]
        {
            ("Localização e sinalização (NR-23)", "Extintor em local visível, de fácil acesso e desobstruído"),
            ("Localização e sinalização (NR-23)", "Sinalização de parede (placa) instalada acima do extintor"),
            ("Localização e sinalização (NR-23)", "Sinalização de piso, quando exigida"),
            ("Localização e sinalização (NR-23)", "Alça a no máximo 1,60 m do piso, ou extintor apoiado em suporte próprio"),
            ("Localização e sinalização (NR-23)", "Não instalado em escadas, rampas ou atrás de portas"),
            ("Adequação ao risco", "Agente extintor compatível com a classe de fogo da área (A, B, C)"),
            ("Adequação ao risco", "Quantidade e distância conforme a Instrução Técnica do Corpo de Bombeiros"),
            ("Adequação ao risco", "Extintor de CO₂ ou pó químico próximo a quadros elétricos"),
            ("Adequação ao risco", "Extintor junto a áreas de risco (inflamáveis, gerador, cozinha, solda)"),
            ("Condição do equipamento (NBR 12962)", "Manômetro com ponteiro na faixa verde (extintores pressurizados)"),
            ("Condição do equipamento (NBR 12962)", "Lacre e trava de segurança intactos"),
            ("Condição do equipamento (NBR 12962)", "Mangueira e difusor sem rachaduras, ressecamento ou obstrução"),
            ("Condição do equipamento (NBR 12962)", "Cilindro sem amassados, corrosão ou danos na pintura"),
            ("Condição do equipamento (NBR 12962)", "Rótulo, quadro de instruções e selo do Inmetro legíveis"),
            ("Manutenção e registro", "Etiqueta de manutenção com validade em dia (nível 2, anual)"),
            ("Manutenção e registro", "Teste hidrostático dentro do prazo (5 anos)"),
            ("Manutenção e registro", "Anel de identificação da última manutenção presente"),
            ("Manutenção e registro", "Registro de inspeção mensal preenchido"),
            ("Pessoas", "Trabalhadores da frente treinados no uso de extintores"),
            ("Pessoas", "Brigada de incêndio designada e identificada, quando exigida"),
        }),

        new TemaPadrao("Andaimes", 7, new[]
        {
            ("Documentação (NR-18)", "Projeto ou especificação do fabricante disponível na obra"),
            ("Documentação (NR-18)", "Liberação para uso registrada por profissional habilitado"),
            ("Documentação (NR-18)", "Montadores capacitados e com treinamento NR-35"),
            ("Base e estrutura", "Apoiado em base firme, nivelada e com sapatas ou rodízios travados"),
            ("Base e estrutura", "Peças sem trincas, empenos, corrosão ou soldas improvisadas"),
            ("Base e estrutura", "Contraventamentos e travessas completos"),
            ("Base e estrutura", "Ancorado à estrutura conforme o projeto"),
            ("Base e estrutura", "Altura respeita a relação com a base indicada pelo fabricante"),
            ("Plataforma de trabalho", "Piso completo, travado e sem vãos, com largura mínima de 0,60 m"),
            ("Plataforma de trabalho", "Pranchas metálicas ou de madeira certificada, sem danos"),
            ("Plataforma de trabalho", "Guarda-corpo com travessão superior a 1,20 m, intermediário e rodapé"),
            ("Plataforma de trabalho", "Sem sobrecarga de materiais além da capacidade indicada"),
            ("Acesso", "Acesso por escada incorporada ou externa fixada"),
            ("Acesso", "Proibido escalar pelas travessas do andaime"),
            ("Proteção contra quedas (NR-35)", "Ponto de ancoragem para o cinto definido e resistente"),
            ("Proteção contra quedas (NR-35)", "Trabalhadores usando cinto tipo paraquedista conectado"),
            ("Entorno", "Área abaixo isolada e sinalizada"),
            ("Entorno", "Distância segura de redes elétricas energizadas"),
            ("Entorno", "Andaime móvel só deslocado sem pessoas sobre ele"),
            ("Entorno", "Placa de identificação com situação (liberado/interditado)"),
        }),

        new TemaPadrao("Escadas", 30, new[]
        {
            ("Escadas portáteis (NR-18)", "Escada em bom estado, sem degraus quebrados, soltos ou trincados"),
            ("Escadas portáteis (NR-18)", "Montantes sem empenos, rachaduras ou emendas"),
            ("Escadas portáteis (NR-18)", "Sapatas antiderrapantes presentes e íntegras"),
            ("Escadas portáteis (NR-18)", "Madeira sem pintura que esconda defeitos"),
            ("Escadas portáteis (NR-18)", "Escada de mão ultrapassa 1,00 m o ponto de apoio superior"),
            ("Escadas portáteis (NR-18)", "Escada de mão amarrada ou presa no topo"),
            ("Escadas portáteis (NR-18)", "Escada de abrir com limitador de abertura travado"),
            ("Escadas portáteis (NR-18)", "Escada de material isolante perto de instalações elétricas"),
            ("Uso", "Usada apoiada em base firme e nivelada"),
            ("Uso", "Uso por uma pessoa de cada vez, com três pontos de apoio"),
            ("Uso", "Não usada próxima a portas ou áreas de circulação sem isolamento"),
            ("Uso", "Não usada como passarela, prancha ou plataforma"),
            ("Escadas fixas e de uso coletivo", "Degraus uniformes, com piso antiderrapante"),
            ("Escadas fixas e de uso coletivo", "Corrimão e guarda-corpo instalados e firmes"),
            ("Escadas fixas e de uso coletivo", "Escada de marinheiro acima de 6 m com gaiola ou trava-quedas"),
            ("Escadas fixas e de uso coletivo", "Iluminação adequada e acesso desobstruído"),
            ("Guarda e controle", "Escadas guardadas em local coberto quando fora de uso"),
            ("Guarda e controle", "Escada danificada retirada de uso e identificada"),
        }),

        new TemaPadrao("Trabalho em Altura", 7, new[]
        {
            ("Planejamento (NR-35)", "Análise de Risco elaborada para a atividade"),
            ("Planejamento (NR-35)", "Permissão de Trabalho emitida, quando aplicável"),
            ("Planejamento (NR-35)", "Trabalhadores com treinamento NR-35 válido"),
            ("Planejamento (NR-35)", "Trabalhadores com ASO apto para trabalho em altura"),
            ("Planejamento (NR-35)", "Condições climáticas adequadas (sem chuva forte ou ventos)"),
            ("Equipamentos", "Cinto tipo paraquedista com CA válido e sem danos"),
            ("Equipamentos", "Talabarte duplo com absorvedor de energia"),
            ("Equipamentos", "Trava-quedas compatível com a linha de vida"),
            ("Equipamentos", "Capacete com jugular"),
            ("Equipamentos", "Inspeção dos EPIs feita antes do uso"),
            ("Ancoragem", "Ponto de ancoragem resistente e acima da cabeça do trabalhador"),
            ("Ancoragem", "Linha de vida instalada conforme projeto"),
            ("Ancoragem", "Zona livre de queda suficiente abaixo do trabalhador"),
            ("Proteção coletiva", "Guarda-corpo e rodapé nas bordas e aberturas"),
            ("Proteção coletiva", "Aberturas no piso fechadas ou protegidas"),
            ("Proteção coletiva", "Área abaixo isolada e sinalizada"),
            ("Proteção coletiva", "Ferramentas presas para evitar queda de objetos"),
            ("Emergência", "Plano de resgate definido e equipe disponível"),
            ("Emergência", "Trabalhador nunca sozinho em altura"),
        }),

        new TemaPadrao("Instalações Elétricas", 30, new[]
        {
            ("Quadros (NR-10)", "Quadros fechados, com porta e trava"),
            ("Quadros (NR-10)", "Sinalização de risco elétrico no quadro"),
            ("Quadros (NR-10)", "Circuitos identificados"),
            ("Quadros (NR-10)", "Disjuntores e DR (diferencial residual) instalados"),
            ("Quadros (NR-10)", "Sem partes energizadas expostas"),
            ("Quadros (NR-10)", "Aterramento do quadro e das estruturas metálicas"),
            ("Quadros (NR-10)", "Área à frente do quadro desobstruída"),
            ("Cabos e extensões", "Cabos sem emendas improvisadas ou isolamento danificado"),
            ("Cabos e extensões", "Cabos elevados ou protegidos contra passagem de veículos e pessoas"),
            ("Cabos e extensões", "Fiação fora de áreas molhadas"),
            ("Cabos e extensões", "Plugues e tomadas industriais em bom estado"),
            ("Cabos e extensões", "Extensões com fio adequado à carga, sem \"benjamins\""),
            ("Trabalhadores (NR-10)", "Eletricistas com treinamento NR-10 válido"),
            ("Trabalhadores (NR-10)", "Serviço só por profissional autorizado"),
            ("Trabalhadores (NR-10)", "Bloqueio e etiquetagem durante manutenção"),
            ("Trabalhadores (NR-10)", "EPIs para eletricidade em uso (luva isolante, vestimenta)"),
            ("Documentação", "Diagrama unifilar disponível"),
            ("Documentação", "Laudo de aterramento/SPDA em dia, quando exigido"),
        }),

        new TemaPadrao("Máquinas e Equipamentos", 7, new[]
        {
            ("Proteções (NR-12)", "Partes móveis (correias, polias, engrenagens) com proteção fixa"),
            ("Proteções (NR-12)", "Proteções sem remoção ou adaptação"),
            ("Proteções (NR-12)", "Botão de parada de emergência acessível e funcionando"),
            ("Proteções (NR-12)", "Dispositivo que impede partida inesperada"),
            ("Elétrica", "Máquina aterrada e com cabo em bom estado"),
            ("Elétrica", "Comandos identificados e protegidos contra acionamento acidental"),
            ("Condição", "Sem vazamentos de óleo, combustível ou ar"),
            ("Condição", "Manutenção preventiva registrada e em dia"),
            ("Condição", "Manual em português disponível"),
            ("Condição", "Sinalização de riscos na máquina"),
            ("Operador", "Operador capacitado e autorizado"),
            ("Operador", "EPIs adequados à operação em uso"),
            ("Operador", "Sem roupas folgadas, adornos ou cabelo solto perto de partes móveis"),
            ("Instalação", "Máquina fixada e nivelada"),
            ("Instalação", "Área de operação demarcada e desobstruída"),
            ("Instalação", "Serra circular com coifa, cutelo divisor e empurrador"),
            ("Instalação", "Betoneira com proteção na coroa e no motor"),
        }),

        new TemaPadrao("Ferramentas Manuais e Elétricas", 30, new[]
        {
            ("Ferramentas manuais", "Ferramentas adequadas ao serviço e sem improvisos"),
            ("Ferramentas manuais", "Cabos firmes, sem rachaduras ou farpas"),
            ("Ferramentas manuais", "Partes cortantes protegidas quando fora de uso"),
            ("Ferramentas manuais", "Ferramentas guardadas em caixa ou local próprio"),
            ("Ferramentas elétricas (NR-12)", "Carcaça sem trincas e cabo de alimentação íntegro"),
            ("Ferramentas elétricas (NR-12)", "Plugue em bom estado, sem fios expostos"),
            ("Ferramentas elétricas (NR-12)", "Interruptor funcionando corretamente"),
            ("Ferramentas elétricas (NR-12)", "Esmerilhadeira com coifa de proteção e empunhadura lateral"),
            ("Ferramentas elétricas (NR-12)", "Disco compatível com a rotação da máquina e sem danos"),
            ("Ferramentas elétricas (NR-12)", "Furadeira e serra com proteções originais"),
            ("Ferramentas pneumáticas", "Mangueiras e conexões sem vazamento, com trava de segurança"),
            ("Uso", "Usuário treinado na ferramenta"),
            ("Uso", "EPIs em uso (óculos, protetor auditivo, luvas adequadas)"),
            ("Uso", "Ferramenta desligada da tomada ao trocar acessórios"),
            ("Controle", "Ferramenta danificada retirada de uso e identificada"),
            ("Controle", "Controle de entrega e inspeção no almoxarifado"),
        }),

        new TemaPadrao("Proteções Coletivas (EPC)", 7, new[]
        {
            ("Bordas e aberturas (NR-18)", "Guarda-corpo com travessão superior a 1,20 m, intermediário e rodapé"),
            ("Bordas e aberturas (NR-18)", "Aberturas no piso fechadas com tampa fixa ou guarda-corpo"),
            ("Bordas e aberturas (NR-18)", "Poço de elevador protegido em todos os pavimentos"),
            ("Bordas e aberturas (NR-18)", "Escadas de obra com corrimão"),
            ("Bordas e aberturas (NR-18)", "Rede de proteção instalada conforme projeto"),
            ("Queda de materiais", "Plataforma de proteção (bandeja) instalada, quando exigida"),
            ("Queda de materiais", "Tela de fachada íntegra e fixada"),
            ("Queda de materiais", "Tubo coletor de entulho em uso"),
            ("Sinalização e isolamento", "Áreas de risco isoladas e sinalizadas"),
            ("Sinalização e isolamento", "Placas de uso obrigatório de EPI nos acessos"),
            ("Sinalização e isolamento", "Rotas de circulação e saídas desobstruídas"),
            ("Sinalização e isolamento", "Iluminação adequada nas frentes de trabalho"),
            ("Outras proteções", "Linha de vida instalada onde previsto"),
            ("Outras proteções", "Ventilação/exaustão em ambientes com poeira ou gases"),
            ("Outras proteções", "Lava-olhos disponível perto de produtos químicos, quando exigido"),
            ("Manutenção", "EPCs inspecionados e reparados após remoção temporária"),
        }),

        new TemaPadrao("EPI", 30, new[]
        {
            ("Fornecimento (NR-06)", "EPIs fornecidos conforme a função (PGR)"),
            ("Fornecimento (NR-06)", "Ficha de entrega de EPI assinada"),
            ("Fornecimento (NR-06)", "Todos os EPIs com CA válido"),
            ("Fornecimento (NR-06)", "Estoque mínimo de EPIs no almoxarifado"),
            ("Uso em campo", "Capacete em uso, com jugular quando em altura"),
            ("Uso em campo", "Calçado de segurança em uso"),
            ("Uso em campo", "Óculos de proteção em atividades com projeção"),
            ("Uso em campo", "Protetor auditivo em áreas ruidosas"),
            ("Uso em campo", "Luvas adequadas ao risco da atividade"),
            ("Uso em campo", "Proteção respiratória em atividades com poeira ou vapores"),
            ("Uso em campo", "Uniforme completo e em bom estado"),
            ("Condição", "EPIs sem danos, limpos e dentro da validade"),
            ("Condição", "EPI danificado substituído imediatamente"),
            ("Condição", "Guarda adequada dos EPIs quando fora de uso"),
            ("Treinamento", "Trabalhadores orientados sobre uso, guarda e conservação"),
            ("Treinamento", "Visitantes recebem EPI na entrada da obra"),
        }),

        new TemaPadrao("Escavações", 7, new[]
        {
            ("Planejamento (NR-18)", "Escavação acima de 1,25 m com projeto de contenção ou talude"),
            ("Planejamento (NR-18)", "Interferências (redes enterradas) identificadas antes de escavar"),
            ("Planejamento (NR-18)", "Permissão de Trabalho emitida, quando aplicável"),
            ("Contenção", "Escoramento ou talude estável, sem sinais de desmoronamento"),
            ("Contenção", "Material escavado a pelo menos metade da profundidade da borda"),
            ("Contenção", "Equipamentos e veículos afastados da borda"),
            ("Contenção", "Drenagem de água da escavação"),
            ("Acesso", "Escada ou rampa de acesso a cada 15 m de vala"),
            ("Acesso", "Passarelas com guarda-corpo para travessia"),
            ("Proteção das bordas", "Bordas protegidas com guarda-corpo ou barreira"),
            ("Proteção das bordas", "Sinalização diurna e noturna da escavação"),
            ("Atmosfera", "Avaliação de gases em escavações profundas ou fechadas"),
            ("Pessoas", "Supervisão permanente durante o trabalho na vala"),
            ("Pessoas", "Trabalhadores com capacete e EPIs adequados"),
            ("Pessoas", "Inspeção diária após chuva ou vibração"),
        }),

        new TemaPadrao("Içamento de Cargas", 7, new[]
        {
            ("Planejamento (NR-18/NR-12)", "Plano de içamento elaborado para cargas críticas"),
            ("Planejamento (NR-18/NR-12)", "Peso da carga conhecido e dentro da tabela de carga"),
            ("Planejamento (NR-18/NR-12)", "Condições de vento adequadas"),
            ("Equipamento", "Guindaste/munck com inspeção e manutenção em dia"),
            ("Equipamento", "Patolas apoiadas em base firme, com calços"),
            ("Equipamento", "Limitador de carga e alarmes funcionando"),
            ("Equipamento", "Gancho com trava de segurança"),
            ("Acessórios", "Cintas, cabos e manilhas com identificação de capacidade"),
            ("Acessórios", "Acessórios sem cortes, desgaste, nós ou deformações"),
            ("Pessoas", "Operador habilitado e autorizado"),
            ("Pessoas", "Sinaleiro/amarrador capacitado e identificado"),
            ("Pessoas", "Comunicação definida entre operador e sinaleiro"),
            ("Área", "Área de içamento isolada e sinalizada"),
            ("Área", "Ninguém sob a carga suspensa"),
            ("Área", "Cabo-guia usado para controlar a carga"),
            ("Área", "Distância segura de redes elétricas"),
        }),

        new TemaPadrao("Espaço Confinado", 7, new[]
        {
            ("Gestão (NR-33)", "Espaço confinado identificado e sinalizado"),
            ("Gestão (NR-33)", "Permissão de Entrada e Trabalho (PET) emitida e assinada"),
            ("Gestão (NR-33)", "Supervisor de entrada designado"),
            ("Gestão (NR-33)", "Vigia posicionado na entrada durante todo o trabalho"),
            ("Gestão (NR-33)", "Trabalhadores com treinamento NR-33 válido"),
            ("Gestão (NR-33)", "Trabalhadores com ASO apto para espaço confinado"),
            ("Atmosfera", "Detector multigás calibrado"),
            ("Atmosfera", "Medição de O₂, inflamáveis e tóxicos antes da entrada"),
            ("Atmosfera", "Monitoramento contínuo durante o trabalho"),
            ("Atmosfera", "Ventilação/exaustão em funcionamento"),
            ("Isolamento", "Bloqueio e etiquetagem de energias e tubulações"),
            ("Equipamentos", "Equipamentos elétricos adequados à atmosfera"),
            ("Equipamentos", "Tripé e sistema de resgate montados"),
            ("Equipamentos", "Cinto com ponto de içamento em uso"),
            ("Emergência", "Equipe de resgate disponível e treinada"),
            ("Emergência", "Meio de comunicação entre interior e vigia"),
        }),

        new TemaPadrao("Trabalho a Quente", 7, new[]
        {
            ("Planejamento", "Permissão de Trabalho a Quente emitida"),
            ("Planejamento", "Materiais inflamáveis removidos ou protegidos num raio seguro"),
            ("Planejamento", "Extintor adequado próximo ao local"),
            ("Planejamento", "Observador de incêndio durante e após o trabalho, quando exigido"),
            ("Equipamentos de solda", "Cilindros em pé, presos e com capacete de proteção"),
            ("Equipamentos de solda", "Válvulas corta-chama no maçarico e no regulador"),
            ("Equipamentos de solda", "Mangueiras sem vazamento, emendas ou ressecamento"),
            ("Equipamentos de solda", "Máquina de solda aterrada e com cabos íntegros"),
            ("Equipamentos de solda", "Cilindros de oxigênio separados dos de gás combustível"),
            ("Proteção", "Máscara de solda, avental, luvas e perneiras em uso"),
            ("Proteção", "Biombos para proteger terceiros da radiação"),
            ("Proteção", "Ventilação/exaustão de fumos"),
            ("Encerramento", "Área verificada após o término quanto a focos de incêndio"),
        }),

        new TemaPadrao("Produtos Químicos e Inflamáveis", 30, new[]
        {
            ("Armazenamento (NR-20/NR-26)", "Local de armazenamento ventilado, coberto e sinalizado"),
            ("Armazenamento (NR-20/NR-26)", "Produtos incompatíveis separados"),
            ("Armazenamento (NR-20/NR-26)", "Bacia de contenção para líquidos"),
            ("Armazenamento (NR-20/NR-26)", "Quantidade armazenada dentro do limite permitido"),
            ("Armazenamento (NR-20/NR-26)", "Proibição de fumar e de chamas sinalizada"),
            ("Armazenamento (NR-20/NR-26)", "Extintor adequado próximo ao local"),
            ("Identificação", "Embalagens com rótulo original e legível"),
            ("Identificação", "FISPQ/FDS disponível para cada produto"),
            ("Identificação", "Produtos transferidos identificados no novo recipiente"),
            ("Manuseio", "Trabalhadores orientados sobre os riscos dos produtos"),
            ("Manuseio", "EPIs indicados na FISPQ em uso"),
            ("Manuseio", "Kit de contenção de derramamento disponível"),
            ("Manuseio", "Abastecimento de máquinas em local apropriado"),
            ("Resíduos", "Embalagens vazias e resíduos destinados corretamente"),
        }),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        var existentes = await db.TemasInspecao.IgnoreQueryFilters().Select(t => t.Nome).ToListAsync(ct);

        foreach (var padrao in Temas())
        {
            if (existentes.Any(n => string.Equals(n.Trim(), padrao.Nome, StringComparison.OrdinalIgnoreCase)))
                continue;

            var tema = new TemaInspecao { Nome = padrao.Nome, PeriodicidadeDiasPadrao = padrao.PeriodicidadeDias };
            var checklist = new ChecklistModelo
            {
                Nome = $"Checklist de {padrao.Nome}",
                TipoInspecao = TipoInspecao.Tema,
                TemaInspecaoId = tema.Id,
                Versao = 1,
            };

            var ordem = 1;
            foreach (var (secao, descricao) in padrao.Itens.Take(MaximoItensPorTema))
            {
                checklist.Itens.Add(new ChecklistModeloItem
                {
                    Ordem = ordem++,
                    Secao = secao,
                    Descricao = descricao,
                    ExigeFotografia = false,
                    ExigeResponsavel = false,
                    ExigePrazo = false,
                });
            }

            db.TemasInspecao.Add(tema);
            db.ChecklistModelos.Add(checklist);
        }

        await db.SaveChangesAsync(ct);
    }
}
