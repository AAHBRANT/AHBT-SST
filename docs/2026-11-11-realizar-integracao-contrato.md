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
- Fotos, presença biométrica e instrutor continuam nos endpoints existentes `api/sessoestreinamento/{sessaoTreinamentoId}/...` (a `sessaoTreinamentoId` de cada etapa vem no DTO). Só funcionam com a etapa `emAndamento`; fora disso 400. **Fotos: obrigatório enviar `Metadados` com `origem: "camera"`, `capturadaEm` posterior ao início da etapa (`etapa.iniciadaEmUtc`) e geolocalização; foto repetida (mesmo SHA-256) em qualquer outra etapa/turma ou outro quadro da mesma etapa: 400 "Esta foto já foi usada...".**
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
- `POST api/sessoestreinamento/{id}/encerrar`, `PUT ./{id}/data` e `DELETE .../{id}` (e `excluir-lote`): recusam turma de integração (400; no lote, resultado `bloqueada`).
- `GET api/sessoestreinamento/{id}` (detalhe) não mudou.
- Configuração: nova chave `Integracao:AtendimentoHabilitado` em `appsettings.json` (false).


## Atualização (decisões Q4, Q6, Q7, Q9, Q10 e correções da revisão)

### Rotas novas
| Método | Rota | Policy | Corpo | Resposta |
|---|---|---|---|---|
| GET | `/api/integracoes-atendimento?obraId=&status=` | ver | — | `IntegracaoResumoDto[]` (Q10; `status` = emAndamento\|concluido\|cancelado; sem `obraId`, só as obras em que o usuário é Técnico/Admin) |
| POST | `/{id}/etapas/{etapaId}/participantes/{trabalhadorId}/nao-assinou` | criar | `{ motivo }` (>= 5 caracteres) | 200 `IntegracaoAtendimentoDto` (Q9) |
| DELETE | `/{id}/etapas/{etapaId}/participantes/{trabalhadorId}/nao-assinou` | criar | — | 200 `IntegracaoAtendimentoDto` (desfaz antes de avançar) |
| GET | `/api/sessoestreinamento/{id}/fotos-evidencia/token-captura` | criar | — | `{ token, expiraEmUtc }` (M5) |

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
- **M5**: `POST api/sessoestreinamento/{id}/fotos-evidencia` de turma de integração exige o campo **`tokenCaptura`** (multipart) obtido no GET acima: uso único, 5 min, ligado à turma e ao usuário; sem token válido, 400. A hora da captura gravada (`capturadaEm` e `localizacaoObtidaEm`) é a do **servidor** no consumo do token; o relógio do aparelho não conta. O hash SHA-256 continua recusando repetição. Risco residual (para QSMS/jurídico): reencodar/regravar a imagem muda o hash e escapa da checagem de duplicidade; o token garante apenas que a foto foi enviada durante a etapa, não que o conteúdo é inédito.
- **B1**: presença digital/facial, anexar/remover foto, instrutor e token, quando a turma é de integração, só passam para Técnico/Admin com vínculo na obra (senão 404). Turma comum não muda.
- **B5**: iniciar com outro atendimento em andamento na obra (inclusive corrida no índice único) devolve **409** `{ erro: "Já existe uma integração em andamento nesta obra..." }`.
- Rota do passo no plano: `/treinamentos/integracao/{id}` ou `/treinamentos/integracao/nova?obraId={obraId}`.


## Atualização 16/11/2026 — instrutor com biometria, carga horária automática, pré-checagem e fotos automáticas

Decisões do usuário: (1) o instrutor assina por **digital OU facial, uma vez em cada etapa de curso**, junto dos colaboradores; sem biometria cadastrada **bloqueia** (não há fallback pela sessão logada; a assinatura automática pelo login saiu); (2) o mesmo vale para turmas comuns de Treinamentos (assina antes de encerrar); turmas já encerradas e certificados já emitidos não mudam; (3) a OS não faz parte desta entrega.

### Rotas novas

| Método | Rota | Policy | Corpo | Resposta |
|---|---|---|---|---|
| POST | `/api/integracoes-atendimento/{id}/etapas/{etapaId}/instrutor/assinar/digital` | criar (+ Técnico/Admin da obra) | JSON igual ao de `sessoestreinamento/{id}/instrutor/assinar/biometria-local`: `{ dispositivoId, segredoDispositivo?, trabalhadorId, score, imagemDigital?, localizacao?, desafio?, assinaturaLeitura? }` | 200 `IntegracaoAtendimentoDto` |
| POST | `/api/integracoes-atendimento/{id}/etapas/{etapaId}/instrutor/assinar/facial` | criar (+ Técnico/Admin da obra) | multipart: `foto` (JPEG/PNG), `localizacaoStatus?`, `latitude?`, `longitude?`, `precisaoMetros?` (sem `obraId`: o servidor busca no grupo facial da **obra do instrutor**) | 200 `IntegracaoAtendimentoDto` |
| POST | `/api/integracoes-atendimento/{id}/etapas/{etapaId}/periodos/ajustar` | criar (+ Técnico/Admin da obra) | `{ "periodos": [ { "data": "yyyy-MM-dd", "inicio": "HH:mm", "fim": "HH:mm" } ] }` | 200 `IntegracaoAtendimentoDto` |
| GET | `/api/integracoes-atendimento/configuracao` | ver | — | `ConfiguracaoIntegracaoDto` |
| POST | `/api/sessoestreinamento/{id}/instrutor/assinatura/digital` | criar | mesmo JSON da digital acima | 200 `AssinaturaInstrutorTurmaDto` |
| POST | `/api/sessoestreinamento/{id}/instrutor/assinatura/facial` | criar | mesmo multipart do facial acima | 200 `AssinaturaInstrutorTurmaDto` |

Erros das rotas de assinatura do instrutor: **404** sem acesso à integração/turma (outra obra, sem vínculo); **400** etapa/turma não está em andamento (ou já encerrada), instrutor não definido, etapa da OS, pessoa lida não é o instrutor ("A digital lida não é a do instrutor desta integração." / "A face lida não é a do instrutor desta integração."; na turma comum, "...desta turma."), instrutor sem perfil, rosto rejeitado (`{ erro, motivo }` com `NenhumRostoDetectado`, `MultiplosRostosDetectados`, `ConfiancaBaixa`, `RostoNaoReconhecido`, `RostoAmbiguo`, `CadastroSemVinculo`); **409** `{ "erro": "O instrutor já assinou esta integração." }` (ou "...esta turma.").

`POST api/sessoestreinamento/{id}/instrutor/assinar/biometria-local` continua só para turmas **já encerradas** (modelo antigo: assina os certificados que ainda não têm o instrutor) e agora também confere o acesso à turma de integração (404 fora da obra).

```ts
type AssinaturaInstrutorTurmaDto = { sessaoTreinamentoId: string; instrutorTrabalhadorId: string;
  metodo: "biometria" | "reconhecimentoFacial"; assinadoEmUtc: string };   // "biometria" = digital
type FotosAutomaticasIntegracaoDto = { primeiraAposSegundos: number; segundaAposSegundos: number; terceiraAposSegundos: number };
type ConfiguracaoIntegracaoDto = { habilitada: boolean; fotosAutomaticas: FotosAutomaticasIntegracaoDto };
```

### DTOs alterados (campos novos, todos aditivos)

- `EtapaIntegracaoDto`: `pendencias: { codigo: string; texto: string }[]` (mesma lista de `faltas`, na mesma ordem, com código estável; `faltas` fica por compatibilidade), `instrutorAssinou: boolean`, `instrutorAssinadoEmUtc: string | null`, `instrutorMetodo: "biometria" | "reconhecimentoFacial" | null`, `instrutorTemDigital: boolean`, `instrutorTemFacial: boolean`. O método usa os mesmos valores do `metodo` dos participantes (digital = `biometria`).
- `periodos[]` da etapa: `automatico: boolean`.
- `EntradaIntegracaoDto`: `fotosAutomaticas` (mesmo objeto de `/configuracao`); `instrutores[]`: `temDigital`, `temFacial` (a tela avisa na hora da escolha).
- `GET api/sessoestreinamento/{id}`: `instrutor` ganhou `assinouAntesDeEncerrar`, `assinadoEmUtc`, `metodoAssinatura`, `temDigital`, `temFacial`.

### Códigos de pendência (`pendencias[].codigo`)

| Código | Texto | Quando |
|---|---|---|
| `sem_participantes` | A etapa não tem participantes. | etapa sem ninguém na turma |
| `instrutor_nao_definido` | Instrutor da turma não definido. | turma sem instrutor |
| `carga` | Carga horária: lance ao menos um período... / ... abaixo do mínimo de N h. | sem período ou abaixo do mínimo |
| `carga_aguardando_fim` | Carga horária fecha às HH:mm — faltam X h YY min. | o período automático ainda não terminou (horário de Brasília; se o fim cai em outro dia: "às HH:mm de dd/MM") |
| `fotos` | Faltam N foto(s) de evidência (são 3, tiradas pela câmera nesta etapa). | menos de 3 fotos |
| `assinaturas_colaboradores` | Assinatura biométrica pendente (N): nomes. | participante sem presença |
| `instrutor_assinatura` | Assinatura biométrica do instrutor pendente. | instrutor não assinou a etapa |
| `instrutor_sem_biometria` | Instrutor sem digital nem facial cadastrada: cadastre no perfil dele. | vem **junto** com `instrutor_assinatura` quando ele não tem digital nem facial |

### Ajustar horário (atômico): `POST /api/integracoes-atendimento/{id}/etapas/{etapaId}/periodos/ajustar`

Substitui **todos** os períodos da etapa pelos informados, em um único `SaveChanges` (tudo ou nada: se qualquer validação falhar, nada muda). Mesma autorização dos demais comandos de período (`treinamento:criar` + acesso à integração; 404 sem acesso) e só vale para etapa de **curso em andamento** (400 caso contrário). Corpo: `{ "periodos": [ { "data": "2026-11-16", "inicio": "10:00", "fim": "12:00" } ] }`; resposta: `IntegracaoAtendimentoDto` atualizado.

- Exige **pelo menos 1** período (máx. 31). A ordem de envio não importa: o servidor ordena por data e início.
- Cada período segue as **mesmas regras do lançamento manual** (`POST .../periodos`): `hh:mm`, fim depois do início, no máximo 12 h, data não futura, fim de hoje não pode estar no futuro, data não anterior ao dia de abertura da etapa e **início não anterior à abertura da etapa** (hora de Brasília, no minuto para baixo; 400 "O início do período não pode ser anterior à abertura da etapa (HH:mm).").
- Sem sobreposição entre os períodos da lista (no mesmo dia) nem com períodos de **outras etapas** da integração (a mesma hora não conta para dois cursos).
- Os períodos gravados têm `automatico = false` (o período automático da abertura sai, e a espera do fim some).
- Janela que cruza a meia-noite: envie um período até `23:59` e outro a partir de `00:00` do dia seguinte; o minuto da virada é contado (igual ao período automático), então `22:00`→`23:59` + `00:00`→`06:00` soma 480 min.
- Erros de regra: 400 `{ erro }` com a mensagem; lista vazia: 400.

### Regras novas

- **Avançar** (`etapa-atual/avancar`) também exige a assinatura biométrica do instrutor guardada na turma da etapa e, com período automático, que o horário de fim dele já tenha chegado (409 com `faltas`). Depois do fim vale a regra normal do mínimo. A espera só existe enquanto houver período `automatico`; ajustar o horário = remover o período automático (`DELETE .../periodos/{periodoId}`) e lançar manual (`POST .../periodos`, com as validações de sempre: o fim não pode estar no futuro).
- **Carga horária automática**: ao abrir cada etapa de curso o sistema lança sozinho o período: data = hoje (Brasília), início = hora da abertura (no minuto), fim = início + `cargaMinimaHoras` da etapa. Se `cargaMinimaHoras` for 0, nada é lançado (o técnico lança à mão). Se cruza a meia-noite, vira um período por dia: o primeiro termina às `23:59` mas conta os minutos até a meia-noite (`minutos` é a duração real), o segundo começa às `00:00` do dia seguinte; a soma bate com a carga prevista. A sobreposição entre etapas continua proibida (a próxima só abre depois do Avançar).
- **Encerramento da turma** (`EncerrarSessaoTreinamentoCommand`, na integração e na turma comum): recusa (400, antes de gravar qualquer coisa) sem a assinatura biométrica do instrutor guardada ("O instrutor precisa assinar com biometria (digital ou facial) antes de encerrar esta turma / etapa da integração."). Ao gerar cada certificado registra a assinatura do instrutor com **papel Instrutor** e o método real (digital ou facial) a partir da guardada (imagem, IP, aparelho, localização, validação); **não** chama mais `RegistrarAssinaturaSessaoLogadaCommand`. Se esse registro falhar depois de encerrar, o certificado **não é finalizado** (log de erro) e a evidência continua guardada. Trocar o instrutor da turma depois da assinatura guardada é recusado (400). Em turma de **integração** o instrutor é o da integração (único) e a troca por `PUT api/sessoestreinamento/{id}/instrutor` é sempre recusada: 400 "O instrutor desta turma é o da integração e não pode ser trocado aqui.". A exclusão definitiva de turma comum (Admin) apaga junto a assinatura biométrica do instrutor guardada (inclusive a imagem), na mesma transação e registrando `AssinaturasInstrutorRemovidas` na auditoria.
- **Iniciar** (`POST /`): recusa (400) se o instrutor escolhido não tiver digital nem facial cadastrada (mensagem com o nome). Não há pré-checagem de ASO (ASO só barra na OS).
- Turmas já encerradas e certificados já emitidos não são alterados por nenhuma destas regras.

### Fotos automáticas (tela)

A captura é do frontend: 1ª foto `primeiraAposSegundos` (60) depois que o técnico ativa Fotografar automaticamente, a 2ª `segundaAposSegundos` (300) depois da 1ª, a 3ª `terceiraAposSegundos` (300) depois da 2ª. Cada foto pede o **próprio** token (`GET api/sessoestreinamento/{id}/fotos-evidencia/token-captura`: uso único, 5 min, ligado à turma e ao usuário) **no momento da captura** e envia com `tokenCaptura`; só câmera ao vivo e SHA-256 único entre etapas, como antes. Configuração: `Integracao:FotosAutomaticas:{PrimeiraAposSegundos,SegundaAposSegundos,TerceiraAposSegundos}` no appsettings ou variáveis de ambiente (`Integracao__FotosAutomaticas__PrimeiraAposSegundos`); valores fora de 5 s a 1 h voltam ao padrão. Lida por `GET /api/integracoes-atendimento/configuracao` e por `entrada.fotosAutomaticas`.

### Banco

Migration `20261116231336_AdicionarAssinaturaInstrutorEPeriodoAutomatico` (só adiciona): tabela `AssinaturasInstrutorSessaoTreinamento` (índice único por `SessaoTreinamentoId` entre os ativos) e coluna `PeriodosEtapaIntegracao.Automatico` (bit, padrão 0).
