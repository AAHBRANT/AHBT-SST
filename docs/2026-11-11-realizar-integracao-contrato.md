# Realizar integração — contrato da API (backend B1–B7, 11/11/2026)

Referência: `docs/2026-11-11-realizar-integracao.md` (plano, seção 5). Este arquivo é o contrato **final** implementado e registra o que mudou em relação ao plano.

Convenções: JSON em camelCase. Enums saem como **texto camelCase** (não número). Datas/horas UTC em ISO 8601 (`2026-11-11T13:37:00Z`). Datas de período são texto `yyyy-MM-dd` e horários `HH:mm` (sem DateOnly/TimeOnly no contrato).
Autenticação: todos os endpoints exigem login. Policies: `treinamento:ver` (GET) e `treinamento:criar` (POST/DELETE). Além disso, o handler confere se o usuário é Técnico de Segurança ou Administrador com vínculo na obra do atendimento (ou escopo global). **Sem acesso: 404** (também para obra/atendimento de outra obra).
Chave de configuração: `Integracao:AtendimentoHabilitado` (padrão `false`). Desligada, `POST /` recusa (400) e a ficha de entrada continua como hoje.

Erros: 400 `{ "erro": "texto" }` (regra de negócio); 404 `{ "erro": "texto" }`; **409** só em `avancar` e `concluir`: `{ "erro": "texto", "faltas": ["..."] }`.

## Rotas

| Método | Rota | Policy | Corpo | Resposta |
|---|---|---|---|---|
| GET | `/api/integracoes-atendimento/entrada?obraId=` | ver | — | `EntradaIntegracaoDto` |
| POST | `/api/integracoes-atendimento` | criar | `{ obraId, trabalhadoresIds[], instrutorTrabalhadorId? }` | 201 `IntegracaoAtendimentoDto` |
| GET | `/api/integracoes-atendimento/{id}` | ver | — | `IntegracaoAtendimentoDto` |
| POST | `/{id}/participantes` | criar | `{ trabalhadoresIds[] }` | 200 `IntegracaoAtendimentoDto` |
| DELETE | `/{id}/participantes/{trabalhadorId}` | criar | — | 200 `IntegracaoAtendimentoDto` (**novo, Q5**) |
| POST | `/{id}/etapas/{etapaId}/periodos` | criar | `{ data, inicio, fim }` | 200 `IntegracaoAtendimentoDto` |
| DELETE | `/{id}/etapas/{etapaId}/periodos/{periodoId}` | criar | — | 200 `IntegracaoAtendimentoDto` |
| POST | `/{id}/etapa-atual/avancar` | criar | `{ etapaOrdem? }` (corpo opcional) | 200 `IntegracaoAtendimentoDto` ou **409** com `faltas` |
| GET | `/{id}/ordens-servico` | ver | — | `OsIntegracaoDto` |
| POST | `/{id}/concluir` | criar | — | 200 `IntegracaoAtendimentoDto` ou **409** com `faltas` |
| GET | `/registros?trabalhadorId=` | `trabalhador:ver` | — | `RegistroIntegracaoDto[]` (**novo**, perfil › Treinamentos; 404 fora da obra) |
| GET | `/relatorio/turmas-integracao-agendadas` | só Admin | — | `TurmaIntegracaoAgendadaDto[]` |

Pontos que o frontend deve respeitar:

- `instrutorTrabalhadorId` é opcional no POST; sem ele vale o instrutor do próprio usuário (se tiver perfil de instrutor). A lista de instrutores elegíveis vem em `entrada.instrutores`.
- `etapaOrdem` no `avancar` torna o clique idempotente: se a etapa informada já está `concluida`, devolve o estado atual (200) sem repetir nada. Mandar sempre a ordem da etapa que a tela mostra.
- Os GET/POST de mutação devolvem o `IntegracaoAtendimentoDto` atualizado, então a tela não precisa de segundo GET.
- Fotos, presença biométrica e instrutor continuam nos endpoints existentes `api/sessoes-treinamento/{sessaoTreinamentoId}/...` (a `sessaoTreinamentoId` de cada etapa vem no DTO). Só funcionam com a etapa `emAndamento`; fora disso 400. **Fotos: obrigatório enviar `Metadados` com `origem: "camera"`, `capturadaEm` posterior ao início da etapa (`etapa.iniciadaEmUtc`) e geolocalização; foto repetida (mesmo SHA-256) em qualquer outra etapa/turma ou outro quadro da mesma etapa: 400 "Esta foto já foi usada...".**
- Encerrar, alterar a data ou excluir uma turma de integração pela tela comum de turmas: 400 (use o atendimento).
- A OS reaproveita `api/trabalhadores/{id}/ordem-servico` (minuta, emitir, assinaturas). A assinatura do funcionário só conta para a integração se for feita **depois** do início do atendimento.

## DTOs

```ts
type EntradaIntegracaoDto = {
  habilitada: boolean; obraId: string; obraNome: string;
  atendimentoEmAndamento: { id: string; numero: string; status: "emAndamento"; etapaAtualOrdem: number | null } | null;
  podeIniciar: boolean;                       // sem bloqueio, sem atendimento aberto e com alguém aguardando
  bloqueios: { codigo: string; mensagem: string; rota: string | null }[]; // FluxoNaoHabilitado | InstrutorNaoCadastrado | CursoNaoCadastrado
  candidatos: { trabalhadorId: string; nome: string; funcaoId: string; funcao: string; planoIntegracaoId: string | null; temPlanoAberto: boolean; jaParticipa: boolean;
                etapas: { tipo: "nr06"|"nr12"|"nr18"|"nr35"|"ordemServico"; ordem: number; situacao: "nestaIntegracao"|"valido"|"naoExigido" }[] }[]  // prévia das 5 etapas, pela calculadora;
  instrutores: { trabalhadorId: string; nome: string; registro: string | null }[];
};

type IntegracaoAtendimentoDto = {
  id: string; numero: string;                 // INT-AAAA-NNNN
  obraId: string; obraNome: string;
  status: "emAndamento" | "concluido" | "cancelado";
  etapaAtualOrdem: number | null;             // 1..5; null quando concluído
  iniciadaEmUtc: string; concluidaEmUtc: string | null;
  responsavelUsuarioId: string | null; responsavelNome: string | null;
  instrutorTrabalhadorId: string; instrutorNome: string | null;
  versao: number;
  podeEditarParticipantes: boolean;           // false depois da primeira assinatura da 1ª etapa de curso
  participantes: { trabalhadorId: string; nome: string; funcaoId: string; funcao: string; planoIntegracaoId: string | null; ordemServicoId: string | null }[];
  etapas: EtapaIntegracaoDto[];               // sempre 5, ordem 1..5
};

type EtapaIntegracaoDto = {
  id: string; ordem: number;                  // 1 NR-06, 2 NR-12, 3 NR-18, 4 NR-35, 5 OS
  tipo: "nr06" | "nr12" | "nr18" | "nr35" | "ordemServico";
  titulo: string;
  status: "pendente" | "emAndamento" | "concluida" | "pulada";
  cursoTreinamentoId: string | null;          // null na OS
  sessaoTreinamentoId: string | null;         // só depois que a etapa de curso abre
  cargaMinimaHoras: number;                   // 0 = sem trava
  cargaLancadaMinutos: number;
  iniciadaEmUtc: string | null; concluidaEmUtc: string | null;
  fotosRegistradas: number; fotosNecessarias: number;   // 3 (0 na OS e em etapa sem turma)
  participantes: { trabalhadorId: string; nome: string; presencaConfirmada: boolean; presencaEmUtc: string | null;
                   metodo: "biometria" | "reconhecimentoFacial" | null; temDigital: boolean; temFacial: boolean }[];
  periodos: { id: string; data: string; inicio: string; fim: string; minutos: number }[];  // yyyy-MM-dd, HH:mm
  faltas: string[];                           // só na etapa emAndamento: o que ainda trava o "Avançar"
  conteudo: string | null;                  // conteúdo programático do curso (null na OS)
  aplicabilidade: string | null;            // ex.: "Vale para Pedreiro, Carpinteiro e Armador"; null quando é de todos (NR-06, NR-18, OS)
};

type OsIntegracaoDto = {
  integracaoId: string; etapaOsEmAndamento: boolean;
  totalPessoas: number; assinadas: number; pendentes: number;
  podeConcluir: boolean;                      // regra Q2
  pessoas: { trabalhadorId: string; nome: string; funcao: string;
             situacao: "naoEmitida" | "aguardandoFuncionario" | "aguardandoTecnico" | "assinada" | "pendente";
             ordemServicoId: string | null; numero: string | null; assinadaEmUtc: string | null;
             podeEmitir: boolean; pendencias: string[] }[];
  pendenciasAgrupadas: { tipo: string; titulo: string; quemResolve: string; trabalhadoresIds: string[]; nomes: string[] }[];
};

type RegistroIntegracaoDto = { id: string; realizadaEmUtc: string; obraId: string; obraNome: string; tecnicoNome: string; funcaoNome: string; numeroAtendimento: string };

type TurmaIntegracaoAgendadaDto = { sessaoTreinamentoId: string; obraId: string; obraNome: string; cursoNome: string; data: string; status: "agendada"; participantes: number };
```

Regras de negócio visíveis na API:

- Ordem fixa. Etapa de curso sem participante nasce `pulada`; a OS nunca é pulada. Participantes de cada etapa são recalculados na hora de abrir (quem já tem a NR válida não entra).
- Carga mínima = maior entre o padrão do curso/NR e o piso decidido (NR-18 4 h, NR-12 8 h, NR-35 8 h; NR-06 sem piso). Vários períodos (inclusive em dias diferentes) somam. Período: data não futura, `fim > inicio`, sem sobreposição no mesmo dia, até 12 h.
- `avancar` exige: instrutor definido, carga lançada >= mínimo, 3 fotos ativas da etapa e presença biométrica confirmada de todos os participantes da etapa (a presença é a assinatura do certificado daquele curso). Encerra a turma (um certificado por pessoa, cada um com o próprio documento de assinatura), grava a carga e a data (último dia) e abre a próxima etapa que tem gente. Etapa concluída é imutável; não há volta.
- `avancar` na etapa da OS: 400 (use `concluir`). `concluir` fora da etapa da OS: 400.
- Q2: `concluir` libera quando todas as OS **emitíveis** estão assinadas (funcionário e técnico). Quem está com pendência externa (ex.: Sem ASO apto) fica `pendente`, não segura os demais e recebe o registro "Integração realizada" quando assinar a OS depois. Pendência de ASO aparece só como "Sem ASO apto" (`tipo: "SemAsoApto"`).
- Q5: incluir/retirar pessoas só enquanto a 1ª etapa de curso não tiver nenhuma presença; pessoa que exigiria etapa já concluída/pulada para trás: 400 com a explicação. Não dá para retirar a última pessoa do atendimento nem da etapa em andamento.

## Mudanças no contrato de outros endpoints

- `GET api/planos-integracao/pendentes` e `/{id}`: com `Integracao:AtendimentoHabilitado` ligado, o passo `integracao` deixa de ser turma agendada. `situacao` ganha o valor **`"emAndamento"`** (detalhe "Em andamento — etapa X de 5."), `pendente` ("Aguardando a integração em etapas da obra.") e `feito` (registro "Integração realizada" ou certificado antigo válido). O passo `nr06` também pode vir `emAndamento`. Cada passo ganhou o campo opcional **`integracaoAtendimentoId`** (guid ou null); `rota` do passo é `/treinamentos/integracao/{id}` (com atendimento) ou `/treinamentos/integracao/nova?obraId={obraId}` (sem). Os passos NR-12/NR-18/NR-35 deixam de aparecer em "Demais cursos da função". Com a chave desligada nada muda (`agendado` e `sessaoTreinamentoId`/`dataTurma` como antes).
- A ficha de entrada ligada ao atendimento não agenda mais turma do combo (NR-06, NR-12, NR-18, NR-35, Integração de Segurança). Os demais cursos da função continuam agendados como hoje.
- `POST api/sessoes-treinamento/{id}/encerrar`, `PUT ./{id}/data` e `DELETE .../{id}` (e `excluir-lote`): recusam turma de integração (400; no lote, resultado `bloqueada`).
- `GET api/sessoes-treinamento/{id}` (detalhe) não mudou.
- Configuração: nova chave `Integracao:AtendimentoHabilitado` em `appsettings.json` (false).


## Atualização (decisões Q4, Q6, Q7, Q9, Q10 e correções da revisão)

### Rotas novas
| Método | Rota | Policy | Corpo | Resposta |
|---|---|---|---|---|
| GET | `/api/integracoes-atendimento?obraId=&status=` | ver | — | `IntegracaoResumoDto[]` (Q10; `status` = emAndamento\|concluido\|cancelado; sem `obraId`, só as obras em que o usuário é Técnico/Admin) |
| POST | `/{id}/etapas/{etapaId}/participantes/{trabalhadorId}/nao-assinou` | criar | `{ motivo }` (>= 5 caracteres) | 200 `IntegracaoAtendimentoDto` (Q9) |
| DELETE | `/{id}/etapas/{etapaId}/participantes/{trabalhadorId}/nao-assinou` | criar | — | 200 `IntegracaoAtendimentoDto` (desfaz antes de avançar) |
| GET | `/api/sessoes-treinamento/{id}/fotos-evidencia/token-captura` | criar | — | `{ token, expiraEmUtc }` (M5) |

```ts
type IntegracaoResumoDto = { id: string; numero: string; obraId: string; obraNome: string; status: "emAndamento"|"concluido"|"cancelado";
  etapaAtualOrdem: number | null; etapaAtualTitulo: string | null; participantes: number; iniciadaEmUtc: string; concluidaEmUtc: string | null; responsavelNome: string | null };
```

### DTO
- `participantes[]` da etapa ganhou `naoAssinou: boolean` e `motivoNaoAssinou: string | null`. Quem está marcado continua listado (com `presencaConfirmada: false`) mas sai da turma: não recebe certificado, não entra na ata e não trava o Avançar.
- `OsIntegracaoDto.pessoas[].situacao` ganhou **`"bloqueada"`**: pendência de PGR/engenheiro/obra (não é do RH). `pendencias[]` traz o texto com "(resolve: SST)".

### Regras que mudaram
- **Q4**: com `Integracao:AtendimentoHabilitado` ligada, nenhum curso da função é agendado automaticamente (também na dispensa de ficha). Desligada, como antes.
- **Q6**: a data de cada certificado (`DataRealizacao`, e a validade a partir dela) é o dia (Brasília) em que a pessoa assinou aquele curso. A OS já usa a data da assinatura/emissão do próprio documento.
- **Q7 / A3**: só com a chave ligada, a entrega de EPI exige NR-06 válida **e assinada** pelo trabalhador (ou certificado externo com arquivo anexado). `RegistroIntegracao` e certificado antigo de Integração **não** liberam EPI. Número e data da NR-06 vêm do certificado da NR-06. Texto de NR-06 fora da validade: "precisa ser renovada". Desligada, a regra de antes (NR-06 válida), só com o texto novo.
- **Q9**: `Marcar/Desfazer` só na etapa de curso `emAndamento`; pessoa que já assinou não pode ser marcada. Se **todos** da etapa estiverem marcados, o Avançar fecha a etapa sem certificados e sem exigir fotos/instrutor/carga; havendo ao menos 1 presente, as fotos continuam obrigatórias. Quem foi desligado/inativo na abertura de uma etapa entra marcado automaticamente com motivo `"Desligado"`. A pessoa marcada só recebe `RegistroIntegracao` quando tiver todos os cursos exigidos válidos e a OS assinada (checado ao assinar a OS e ao encerrar qualquer turma).
- **M1**: Concluir só desconsidera pendência individual resolvida pelo RH (ex.: Sem ASO apto). Pendência de PGR/engenheiro/obra segura (409 com `faltas`, ex.: "Fulano: pendência que segura a conclusão — ... (resolve: SST)"). Exige ao menos 1 OS assinada.
- **M2**: toda etapa de curso exige ao menos 1 período lançado (falta "Carga horária: lance ao menos um período..."), inclusive com mínimo 0. O certificado grava a carga lançada em **horas inteiras arredondadas para baixo (mínimo 1 h)**, porque `Treinamento.CargaHorariaRealizada` é inteiro (não migrei para decimal; o PDF mostra horas inteiras). Nunca usa a carga padrão do curso.
- **A2**: período: data >= dia (Brasília) do início da etapa; fim <= agora; sem sobreposição com períodos de outras etapas do mesmo atendimento (mesma hora não vale para dois cursos).
- **M3/M4**: o passo do plano busca `RegistroIntegracao`/atendimento sem depender do escopo do usuário, mas só da **mesma obra** do plano; o passo `nr06` só fecha pela validade da NR-06. `PlanosIds` não traz plano que já não esteja `Aberto`.
- **M5**: `POST api/sessoes-treinamento/{id}/fotos-evidencia` de turma de integração exige o campo **`tokenCaptura`** (multipart) obtido no GET acima: uso único, 5 min, ligado à turma e ao usuário; sem token válido, 400. A hora da captura gravada (`capturadaEm` e `localizacaoObtidaEm`) é a do **servidor** no consumo do token; o relógio do aparelho não conta. O hash SHA-256 continua recusando repetição. Risco residual (para QSMS/jurídico): reencodar/regravar a imagem muda o hash e escapa da checagem de duplicidade; o token garante apenas que a foto foi enviada durante a etapa, não que o conteúdo é inédito.
- **B1**: presença digital/facial, anexar/remover foto, instrutor e token, quando a turma é de integração, só passam para Técnico/Admin com vínculo na obra (senão 404). Turma comum não muda.
- **B5**: iniciar com outro atendimento em andamento na obra (inclusive corrida no índice único) devolve **409** `{ erro: "Já existe uma integração em andamento nesta obra..." }`.
- Rota do passo no plano: `/treinamentos/integracao/{id}` ou `/treinamentos/integracao/nova?obraId={obraId}`.
