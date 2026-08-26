# PT — Permissão de Trabalho: fidelidade ao modelo em papel

- **Data:** 2026-08-26
- **Autor:** Assistente (Claude Code), a pedido de Wellington Lourenço
- **Status:** Em revisão pelo usuário
- **Precede:** plano de implementação (writing-plans), com migration EF Core

## 1. Contexto

A AAHBRANT tem um formulário real de PT em papel, atualmente em uso no campo:
`PT-PERMISSÃO DE TRABALHO - Rev00 (2).xlsx`, aba "PT Adut. Local. Terra". O módulo
digital de Permissão de Trabalho já existe no SST-APP (`PermissaoTrabalho.cs` e
telas em `pages/pt/*`), mas hoje é mais genérico que o papel: não tem tipo de
serviço estruturado, não tem os checklists de precaução por categoria, não tem
equipamentos/EPI específicos da PT, e a autorização/encerramento tem só 1 papel
em vez dos 3 do papel.

**Decisão do usuário:** o módulo digital deve ficar **igual ao documento** —
fidelidade estrutural e de conteúdo ao formulário em papel, não uma versão
simplificada.

Este documento é a Etapa 2 do trabalho (Etapa 1 = padronização visual do
documento em papel com a identidade AAHBRANT, já entregue em
`docs/AAHBRANT_PT-PermissaoDeTrabalho_AHBT-FOR-SSO-XXX-00_Rev00_2026-08-26_v1.xlsx`).

## 2. Escopo

**Entra:**
- Novos campos/entidades no `PermissaoTrabalho` para refletir 1:1 as seções do
  papel: Descrição do Trabalho, Documentos Referentes (APR), Tipo de Serviço,
  Cuidados Comuns, Equipamentos/Veículos, Precauções Obrigatórias por
  categoria, EPI obrigatório, Aprovação (3 papéis), Conclusão/Cancelamento (3
  papéis), Recomendações/Observações de SST.
- Catálogos de referência semeados com o texto literal do papel (não
  inventado — ver Seção 5).
- Registro fotográfico da PT (evidências), reaproveitando a entidade
  genérica `Evidencia` já usada por outros módulos — ver Seção 4.5.
- Botão de download do documento (exportação em PDF do registro da PT),
  seguindo o mesmo padrão já usado por DDS e Entrega de EPI — ver Seção 4.6.
- Migration EF Core nova (nunca altera a migration existente
  `20260820185848_AdicionarPermissaoTrabalho`).
- Ajustes em Commands/Queries/Controllers/DTOs da Application/Api.
- Ajustes nas telas React (`pages/pt/*`) para capturar os novos dados.

**Não entra (fora do escopo desta etapa):**
- A "Formalização do Documento" (lista de assinaturas da equipe de campo) —
  **já está coberta** pelo `PermissaoTrabalhoResponsavel` existente + Motor de
  Assinatura Eletrônica (`DocumentoAssinatura`/`DocumentoSignatario`, que já
  suporta múltiplos signatários por entidade). Nenhuma mudança de schema é
  necessária aqui; só validar em teste que múltiplas pessoas conseguem assinar
  a mesma PT em sequência pelo quiosque já existente (`AssinarPtPage`).
- Réplica pixel-a-pixel do layout do xlsx original no PDF exportado — o PDF
  segue o padrão institucional já usado em DDS/EPI, não o grid exato do
  papel (ver nota na Seção 4.6).
- Qualquer mudança no módulo de estoque/entrega de EPI (`CatalogoEpi`/
  `EntregaEpi`) além de uma nova associação de leitura.

## 3. Modelo atual (o que já existe e não muda)

`PermissaoTrabalho : AuditableEntity` — Atividade (FK), Local (texto),
Equipe (FK opcional), Data, HorarioInicio/Fim, Validade, Status
(EmElaboracao/Autorizada/Encerrada), AutorizadoPorUsuarioId + DataAutorizacao,
EncerradaPorUsuarioId + DataEncerramento + ObservacoesEncerramento.
Coleções: `Perigos` (FK a Perigo), `Controles` (texto livre), `Requisitos`
(texto livre + Atendido bool), `Responsaveis` (FK a Trabalhador).

`AutorizadoPorUsuarioId`/`EncerradaPorUsuarioId` continuam como estão — são o
registro de auditoria de **qual usuário do sistema** operou a tela (login),
conceito diferente dos "3 papéis" do papel, que são **trabalhadores físicos**
identificados por nome/função/empresa/assinatura. Os dois convivem.

## 4. Modelo proposto

### 4.1 Novos campos simples em `PermissaoTrabalho`

```csharp
public string? DescricaoTrabalho { get; set; }        // "Descrição do Trabalho que será realizado"
public string? RecomendacoesObservacoesSst { get; set; } // "Recomendações/Observações de SST"
public string? OutrosTipoServicoDescricao { get; set; }   // texto livre de "Outros (Especificar)" em Tipo de Serviço
public string? OutrosEquipamentoDescricao { get; set; }   // idem em Equipamentos
```

### 4.2 Novos enums (`Enums.cs`)

```csharp
public enum TipoServicoPt
{
    TrabalhoEmAltura = 1,
    AberturaDeVala = 2,           // "acima de 1,25m"
    MovimentacaoDeCargas = 3,
    TrabalhosComEletricidade = 4,
    SoldagemECorteAQuente = 5,
    Outros = 6,
}

public enum ResultadoChecklistPt
{
    Conforme = 1,      // C
    NaoConforme = 2,   // NC
    NaoAplicavel = 3,  // NA
}

public enum PapelAprovacaoPt
{
    ResponsavelPelaArea = 1,      // Supervisor
    ResponsavelPelaExecucao = 2,  // Encarregado
    ResponsavelSst = 3,           // Téc. Seg. Trab.
}

public enum MotivoEncerramentoPt
{
    Concluida = 1,
    Cancelada = 2,
}
```

### 4.3 Novas entidades

**Catálogos de referência** (semeados uma vez, iguais para todas as obras —
mesmo padrão de `Perigo`/`CatalogoEpi`):

```csharp
public class CatalogoPrecaucaoPt : AuditableEntity
{
    public TipoServicoPt Categoria { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public int Ordem { get; set; }
}

public class CatalogoEquipamentoPt : AuditableEntity
{
    public string Descricao { get; set; } = string.Empty;
    public int Ordem { get; set; }
}

public class CatalogoCuidadoComumPt : AuditableEntity
{
    public string Descricao { get; set; } = string.Empty;
    public int Ordem { get; set; }
}
```

**Ligações PT → catálogo** (uma linha por item marcado nesta PT específica):

```csharp
public class PermissaoTrabalhoTipoServico : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }
    public TipoServicoPt Tipo { get; set; }
}

public class PermissaoTrabalhoEquipamento : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }
    public Guid CatalogoEquipamentoPtId { get; set; }
    public CatalogoEquipamentoPt? CatalogoEquipamentoPt { get; set; }
}

public class PermissaoTrabalhoCuidadoComum : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }
    public Guid CatalogoCuidadoComumPtId { get; set; }
    public CatalogoCuidadoComumPt? CatalogoCuidadoComumPt { get; set; }
    public ResultadoChecklistPt Resultado { get; set; }
}

// Uma linha por item de precaução marcado. NaoAplicavelCategoria replica o
// toggle "NÃO APLICÁVEL" que o papel tem por categoria (ex.: AA12) — quando
// true, os itens individuais dessa categoria ficam informativos, não
// obrigatórios.
public class PermissaoTrabalhoPrecaucao : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }
    public Guid CatalogoPrecaucaoPtId { get; set; }
    public CatalogoPrecaucaoPt? CatalogoPrecaucaoPt { get; set; }
    public ResultadoChecklistPt Resultado { get; set; }
}

// EPI obrigatório para esta PT — reaproveita o catálogo já existente
// (CatalogoEpi), sem duplicar tabela.
public class PermissaoTrabalhoEpiObrigatorio : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }
    public Guid CatalogoEpiId { get; set; }
    public CatalogoEpi? CatalogoEpi { get; set; }
    public ResultadoChecklistPt Resultado { get; set; }
}

// Documentos Referentes (APR) — uma PT pode referenciar mais de uma APR.
public class PermissaoTrabalhoApr : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }
    public Guid AprId { get; set; }
    public Apr? Apr { get; set; }
}

// Os 3 papéis de Aprovação (abertura) e Conclusão/Cancelamento (fechamento).
// Momento distingue os dois blocos do papel; reaproveita a mesma estrutura em
// vez de duplicar a entidade, já que os 3 papéis são os mesmos nos dois casos.
public enum MomentoAprovacaoPt { Abertura = 1, Fechamento = 2 }

public class PermissaoTrabalhoAprovacao : AuditableEntity
{
    public Guid PermissaoTrabalhoId { get; set; }
    public PermissaoTrabalho? PermissaoTrabalho { get; set; }
    public MomentoAprovacaoPt Momento { get; set; }
    public PapelAprovacaoPt Papel { get; set; }
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }
    public string? Empresa { get; set; }     // ex.: "AAHBRANT" — capturado à parte no papel
    public DateTime? DataAprovacao { get; set; }
    // Assinatura em si continua vindo do Motor de Assinatura Eletrônica
    // (DocumentoSignatario), não duplicada aqui.
}
```

### 4.4 Campo em `PermissaoTrabalho` (encerramento)

```csharp
public MotivoEncerramentoPt? MotivoEncerramento { get; set; }
```

### 4.5 Evidências fotográficas

Sem entidade nova: reaproveita a entidade genérica `Evidencia` (já usada por
ASO, Treinamento e Entrega de EPI), habilitando `EntidadeTipo =
"PermissaoTrabalho"`. Já vem com hash SHA-256, autor e
latitude/longitude — mesmo padrão probatório dos outros módulos. Trabalho de
implementação: endpoint de upload (se ainda não for genérico o suficiente
para qualquer `EntidadeTipo`) + aba/seção "Fotos" na
`PermissaoTrabalhoDetalhePage`, reaproveitando o componente de upload já
usado em `EntregasEpiTab`/`InspecaoDetalhePage`.

### 4.6 Exportação em PDF (botão de download)

Segue o padrão já existente em DDS (`IDdsPdfService` +
`ExportarDdsPdfQuery`) e Entrega de EPI (`IEntregaEpiPdfService` +
`ExportarEntregaEpiPdfQuery`): novo `IPermissaoTrabalhoPdfService` +
`ExportarPermissaoTrabalhoPdfQuery`, endpoint dedicado no
`PermissaoTrabalhoController`, e botão "Baixar PDF"
(`ArrowDownload24Regular`, mesmo componente usado em
`PainelAssinaturasTab.tsx`) na tela de detalhe da PT.

Importante distinguir de algo que já existe: isso é o PDF do **registro da
PT no sistema** (dados + checklists preenchidos), formatado conforme a
identidade AAHBRANT — não o mesmo artefato que o **comprovante de
assinatura** do Motor de Assinatura Eletrônica (que já existe, é o PDF do
documento assinado digitalmente por cada signatário). Os dois podem
conviver: o botão novo baixa a PT a qualquer momento (mesmo em elaboração);
o comprovante de assinatura só existe depois que a formalização for
concluída. Não é compromisso de réplica pixel-a-pixel do layout do xlsx
original — é um documento institucional formatado, com todos os dados da
Seção 4 legíveis e organizados, seguindo o mesmo padrão visual dos PDFs de
DDS/EPI já existentes.

## 5. Conteúdo dos catálogos (extraído literalmente do papel)

Fonte: `PT-PERMISSÃO DE TRABALHO - Rev00 (2).xlsx`, aba "PT Adut. Local.
Terra", linhas 6–49. Texto reproduzido **exatamente como está no arquivo**,
incluindo pequenas imperfeições de digitação do original (ex.: "equipamdos",
"ddsst", pontuação solta) — não corrigidas aqui para preservar
rastreabilidade; podem ser normalizadas na tela sem alterar o texto de
referência salvo no banco, se a Coordenação da Qualidade preferir.

### 5.1 Cuidados Comuns para Todas Atividades (`CatalogoCuidadoComumPt`)
1. É impedido o acesso de pessoas não autorizadas nas áreas de trabalho?
2. A equipe de trabalho conhece o plano de emergência em vias públicas?

### 5.2 Equipamento(s) / Veículos Utilizado(s) (`CatalogoEquipamentoPt`)
1. Máquina de Solda/Maçaricos
2. Lixadeiras / Furadeiras
3. Ferramentas Pneumáticas
4. Ferramentas Manuais
5. Mini/Escavadeira/martelo hidráulico
6. Caminhão Guindauto
7. Retroescavadeira/martelo hidráulico
8. Motossera
9. Policorte
10. Outros (Especificar) — texto livre via `OutrosEquipamentoDescricao`

### 5.3 Precauções Obrigatórias (`CatalogoPrecaucaoPt`, por `Categoria`)

**TrabalhoEmAltura:**
1. Prever responsável (Encarregado) para acompanhamento da atividade.
2. Prever Proteções Coletivas (Guarda-corpo, acessos seguros, telamento, tapumes, etc).
3. Treinar os profissionais expostos na Instrução de Trabalho e APR 2 da atividade.
4. Divulgar a APR contemplando o perigo de queda de pessoas/materiais.
5. Elaborar check-lists para verificação de conformidade das estruturas de suporte/andaimes.
6. Prever a identificação da capacidade de carga em andaimes e linhas de vida.
7. Definir os EPIs específicos para trabalhos em altura.
8. Definir sistema de sinalização/isolamento das atividades.
9. Prever sinalização de obrigatoriedade do uso de EPI.
10. Amarração de ferramentas, para evitar a queda da mesma.
11. Providenciar linha de vida horizontal/Vertical.
12. Prever somente profissionais com treinamento na NR 35.
13. Efetuar verificação das condições dos andaimes (liberado/em montagem/interditado).
14. Verificar o fechamento de vãos e aberturas no piso.
15. Sistema de comunicação necessários para Emergências.
16. Prever a realização da Instrução Diária de Segurança - IDS.
17. Planejar as atividades a fim de evitar trabalhos sobrepostos.
18. Verificar se os profissionais realizaram a inspeção no cinto de segurança.
19. Outros (Especificar).

**MovimentacaoDeCargas:**
1. Realizar check-list diário de verificação das condições de segurança do equipamento.
2. Efetuar check-list de inspeção dos cabos e outros acessórios de içamento.
3. Prever responsável (encarregado) pelo acompanhamento da atividade de içamento.
4. Treinar os profissionais expostos na Instrução de Trabalho e APR 2 da atividade.
5. Prever a identificação da capacidade de carga em linhas de vida/guindauto.
6. Dispor de profissional devidamente treinado na atividade.
7. Realizar a Instrução Diária de Segurança - DDSST.
8. Definir sistema de sinalização e isolamento de atividades.
9. Conhecimento do equipamento (profissional treinado/autorizado).
10. Avaliar risco de queda de materiais sobre pessoas/Equipamento.
11. Acessórios de içamento com inspeção "Cor do Mês".
12. O equipamento utilizado deve ser compatível com a carga.
13. Providenciar "Plano de Rigging", quando aplicável.
14. Verificar instabilidade/nivelamento do terreno.
15. Outros (Especificar).

**SoldagemECorteAQuente:**
1. Armazenamento de cilindros de gases em locais apropriados.
2. Presença de válvulas corta fogo nos reguladores e nos maçaricos.
3. Monitoramento nas atividades com potencial de formação de atmosfera explosiva.
4. Observar se não há vazamento nas mangueiras, reguladores e no maçarico.
5. Anteparo eficaz para proteção de projeção de fagulhas.
6. Dispor de biombos, a fim de segregar a área.
7. Prever ventilação geral diluidora (natural ou artificial).
8. Dispor de extintor de incêndio, próximo ao local da atividade.
9. Manter o equipamento limpo e livre de óleo ou graxa.
10. Definir os EPIs específicos para essas atividades.
11. Prever a realização da Instrução Diária de Segurança - IDS.
12. Prever aterramento de máquinas de solda.
13. Outros (Especificar).

**TrabalhosComEletricidade:**
1. Prever diagramas unifilares atualizados das instalações elétricas.
2. Prever disjuntor residual – DR e sistema de aterramento provisório da estrutura.
3. Dispor de materiais de bloqueio para energia elétrica (cadeado; bloqueio de disjuntor; etc).
4. Treinar os profissionais expostos na Instrução de Trabalho e APR 2 da atividade.
5. Prever sistemática de EBTV – Etiquetamento, Bloqueio, Teste e Verificação.
6. Prever a emissão da Ordem de Serviço (OS).
7. Dispor de ferramentas isolantes conforme a tensão requerida.
8. Definir os EPIs específicos para trabalhos em eletricidade.
9. Prever sistema de sinalização para os painéis elétricos.
10. Prever sinalização de restrição de acesso ao local da atividade.
11. Projeto de padronização das instalações e quadros elétricos.
12. Utilizar ferramentas adequadas para a atividade.
13. Aterrar eletricamente equipamentos, linhas e ferramentas.
14. Retirar adornos metálicos do profissional (NR 10).
15. Outros (Especificar).

**AberturaDeVala:**
1. Todos os profissionais estão devidamente equipados com uniformes com faixas refletivas e EPI específicos e adequados para atividade?
2. Os locais de acessos dos profissionais, equipamentos e as áreas escavadas possuem sinalização de alerta, identificação e advertência, inclusive noturna, e isolamento com barreiras em todo seu perímetro?
3. Foi efetuado levantamento prévio para verificar as interferências (redes elétricas, gases etc.) nas áreas de trabalho e se a escavação não causará danos e riscos para a comunidade ou patrimônios vizinhos?
4. Os materiais retirados da escavação serão depositados a uma distância segura das bordas dos taludes? (No mínimo, metade da profundidade da vala).
5. Há necessidade de passarelas sobre a escavação? Caso positivo, obedecer critérios mínimos de segurança (resistente e guarda-corpo de ambos os lados).
6. As valas ou escavações com profundidade superior a 1,25 m serão escoradas, conforme projeto ou característica do solo?
7. Existem escadas em condições adequadas (com um metro acima da borda da vala) ou rampas que facilite o acesso ao local de trabalho na vala?
8. Equipamento de descida e içamento de trabalhadores e materiais está dotado de sistema de segurança com travamento e outros requisitos para a sua operação?
9. Treinar os profissionais expostos na APR da atividade.
10. Os operadores de máquinas e equipamentos automotivos/motorizados estão habilitados e autorizados?
11. Os equipamentos e ferramentas são adequados para o trabalho e estão em perfeito estado de operação?
12. Operações de soldagens e corte a quente dentro de vala serão realizadas de forma adequada e com segurança?
13. Realizado DDSST, antes do início da atividade?
14. Outros (Especificar).

*(Nota: "não aplicável" por categoria — campo `NaoAplicavelCategoria`,
proposto no item 4.3 acima como parte da UI/Application, não como coluna
própria por item.)*

### 5.4 Equipamentos de Proteção Individual — EPI

Reaproveita `CatalogoEpi` (nome já existe nessa tabela). Itens do papel a
confirmar/seedar nesse catálogo, se ainda não existirem:
Avental (raspa/PVC), Botina de couro com BAPA, Capacete com jugular, Cinto de
segurança paraquedista c/ talabarte, Luvas isolantes dentro do prazo de
validade, Camisa e calça anti-chama, Luvas (Couro/PVC/Alta e média tensão),
Máscara (filtro/autônoma/ar/solda), Protetor facial, Varas de manobras,
Perneira (raspa/couro sintético), Protetor auricular (tipo plug/Concha),
Óculos de segurança de impacto, Trava-quedas, Detector de tensão. Mais 4
posições "Outro" livres no papel — cobertas pela ausência de vínculo com
catálogo (permitir item avulso com apenas descrição, se necessário; a
confirmar no plano).

## 6. Fluxo de Aprovação / Conclusão

- **Abertura** exige as 3 linhas de `PermissaoTrabalhoAprovacao`
  (`Momento=Abertura`) preenchidas antes de `AutorizarPermissaoTrabalhoCommand`
  mudar o Status para `Autorizada` — mesma regra de negócio, só adicionando a
  validação dos 3 papéis.
- **Conclusão/Cancelamento** exige as 3 linhas com `Momento=Fechamento`,
  junto com o `MotivoEncerramento` (Concluida/Cancelada), antes de
  `EncerrarPermissaoTrabalhoCommand` mudar o Status para `Encerrada`.
- A assinatura de cada papel continua vindo do Motor de Assinatura Eletrônica
  (mesmo `EntidadeTipo="PermissaoTrabalho"`); o `TrabalhadorId` em
  `PermissaoTrabalhoAprovacao` identifica quem preenche o papel, e o
  signatário correspondente no Motor confirma a assinatura.

## 7. Impacto em API e UI (alto nível — detalhado no plano de implementação)

- **Application:** novos Commands (`AdicionarTipoServicoPtCommand`,
  `MarcarPrecaucaoPtCommand`, `MarcarEquipamentoPtCommand`,
  `MarcarEpiObrigatorioPtCommand`, `RegistrarAprovacaoPtCommand`, etc.),
  Queries (`ListarCatalogoPrecaucaoPtQuery` etc.), seguindo o padrão já usado
  por `PermissaoTrabalhoRequisito`/`PermissaoTrabalhoControle`, mais
  `ExportarPermissaoTrabalhoPdfQuery` + `IPermissaoTrabalhoPdfService`
  (Seção 4.6).
- **Api:** novos endpoints em `PermissaoTrabalhoController` (ou controller de
  catálogos dedicado para os `Catalogo*Pt`, análogo a um controller de
  catálogo de Perigos, se existir), incluindo `GET .../pdf` para o download e
  reaproveitamento do endpoint genérico de upload de `Evidencia` (Seção 4.5).
- **TeamsApp:** `PermissaoTrabalhoDetalhePage` ganha novas abas (Tipo de
  Serviço, Equipamentos, Precauções, EPI, Aprovação/Conclusão, Fotos),
  seguindo o padrão de aba já usado por
  `PermissaoTrabalhoControlesTab`/`PermissaoTrabalhoRequisitosTab`, mais o
  botão "Baixar PDF" no cabeçalho (mesmo padrão de
  `PainelAssinaturasTab.tsx`).

## 8. Migration

Nova migration (nome sugerido: `AdicionarEstruturaCompletaPt`), nunca
alterando `20260820185848_AdicionarPermissaoTrabalho`. Inclui: novas tabelas,
novos enums (armazenados como int, padrão já usado no projeto), seed dos 3
catálogos (`CatalogoPrecaucaoPt`, `CatalogoEquipamentoPt`,
`CatalogoCuidadoComumPt`) com o conteúdo literal da Seção 5, via
`HasData` ou script de seed — a confirmar qual padrão o projeto já usa
(verificar migrations anteriores no plano de implementação).

## 9. Pendências (não decidíveis por mim — registrar e seguir)

1. **Inconsistência no papel** (linhas 63–64: "Responsável pela
   Área/Execução" fundido, depois "Responsável pela Execução" repetido) —
   assumido como os mesmos 3 papéis de sempre (Área, Execução, SST); a
   Coordenação da Qualidade deve confirmar se isso foi um erro de digitação
   do modelo original.
2. **EPI "Outro" (4 posições livres no papel)** — a confirmar no plano se
   viram itens de texto livre sem vínculo a `CatalogoEpi`, ou se todo EPI
   deve necessariamente vir do catálogo.
3. **Pequenas imperfeições de texto do original** (Seção 5) — mantidas como
   estão; normalizar ou não na exibição é decisão de UI, não de dado.
4. SEQ do código do documento (`AHBT-FOR-SSO-XXX-00`) e demais pendências já
   registradas na Etapa 1 continuam abertas.

## 10. Testes (alto nível)

- Testes de unidade nos novos Commands (validação dos 3 papéis antes de
  autorizar/encerrar).
- Teste de integração: criar PT completa (todos os blocos) e verificar
  fidelidade dos dados salvos.
- Teste manual no navegador: fluxo completo de uma PT — abrir, marcar tipo de
  serviço/precauções/EPI, aprovar com os 3 papéis, várias pessoas assinando
  via quiosque, encerrar com motivo.
