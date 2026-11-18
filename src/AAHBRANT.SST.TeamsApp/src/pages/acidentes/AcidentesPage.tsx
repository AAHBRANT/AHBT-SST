import { conferirPendencias } from '../../lib/avisoErro';
import { useCallback, useEffect, useState, type ReactElement } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Abas,
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  ListaSelecaoMultipla,
  Select,
  StatusChip,
  Text,
  Textarea,
  type Coluna,
  type Tom,
  tokensUi,
} from '@ui';
import { AddCircle24Regular } from '@fluentui/react-icons';
import {
  api,
  GravidadeAcidente,
  MAXIMO_PESSOAS_EXTERNAS,
  gravidadeAcidenteLabel,
  StatusAcidente,
  statusAcidenteLabel,
  TipoOcorrencia,
  tipoOcorrenciaLabel,
  type Acidente,
  type Atividade,
  type NovoAcidente,
  type Obra,
  type PessoaExternaSugerida,
  type RelatoOcorrenciaSugestao,
  type RespostaPerguntaRelato,
  type Usuario,
  type Trabalhador,
} from '../../lib/api';
import { HhtMensalTab } from './HhtMensalTab';
import { dataBr } from '../../lib/datas';
import { useFiltroObra } from '../../lib/ObraAtivaContext';
import { CartaoPessoaExternaSugerida, RelatoOcorrenciaIa } from './RelatoOcorrenciaIa';
import { BlocoPessoasExternas } from './BlocoPessoasExternas';
import {
  pendenciasPessoaExterna,
  pessoaDaSugestao,
  pessoaParaPayload,
  type PessoaExternaEmEdicao,
} from './pessoaExterna';
import { payloadAcaoOcorrencia, pendenciaResolucao, resolucaoDoVinculo } from './CampoComoSeResolve';
import { MapaCorporal } from '../../components/mapaCorporal/MapaCorporal';
import {
  novaChaveAcao,
  PlanoAcaoOcorrenciaIa,
  type AcaoPlanoEmEdicao,
  type ReuniaoEmEdicao,
  type TemaDdsEmEdicao,
} from './PlanoAcaoOcorrenciaIa';

// Reunião de análise obrigatória só em Acidente (1) e Doença ocupacional (6).
const exigeReuniao = (tipo: number) => tipo === 1 || tipo === 6;

// Campos que a IA pode preencher a partir do relato. Mostram o selo "IA" até o técnico editá-los.
type CampoIa =
  | 'tipo' | 'atividadeId' | 'local' | 'data' | 'hora' | 'trabalhadoresIds' | 'descricao' | 'lesao'
  | 'consequencia' | 'atendimento' | 'houveAfastamento' | 'diasAfastamento' | 'gravidade';

const seloIa = {
  fontSize: 10,
  fontWeight: 700,
  lineHeight: '14px',
  padding: '0 7px',
  borderRadius: 10,
  color: tokensUi.status.info.tinta,
  backgroundColor: tokensUi.status.info.fundo,
} as const;

const destaqueIa = { backgroundColor: tokensUi.status.info.fundo } as const;

function novaInicial(): NovoAcidente {
  return {
    tipo: 1,
    obraId: '',
    trabalhadoresIds: [],
    testemunhas: [],
    atividadeId: '',
    local: '',
    data: '',
    hora: '',
    descricao: '',
    lesao: '',
    consequencia: '',
    atendimento: '',
    houveAfastamento: false,
    diasAfastamento: undefined,
    numeroCat: '',
    causas: '',
    gravidade: GravidadeAcidente.SemAfastamento,
    diasDebitadosInformados: undefined,
  };
}

// Registrado/EmInvestigacao/Concluido — mesmo padrão de progressão neutro→atencao→ok já usado em
// StatusPcmsoDocumento (Task 12) para os 3 estágios de um fluxo sem workflow de aprovação formal.
const tomPorStatusAcidente: Record<number, Tom> = {
  [StatusAcidente.Registrado]: 'neutro',
  [StatusAcidente.EmInvestigacao]: 'atencao',
  [StatusAcidente.Concluido]: 'ok',
  [StatusAcidente.AguardandoLaudo]: 'info',
};

const ehPessoaExterna = (tipo: number) => tipo === TipoOcorrencia.PessoaExterna;

const normalizarNome = (nome: string) =>
  nome.normalize('NFD').replace(/[̀-ͯ]/g, '').trim().toLowerCase();

// Onda 2 Task 20 (camada ui/): lista + formulário de registro de acidentes/incidentes/quase-acidentes,
// sempre aninhada como aba de OcorrenciasPage (nunca teve PageHeader próprio). Abas internas
// "Acidentes & Incidentes"/"HHT Mensal" viram Abas nivel="interno" — navegação de conteúdo, não de
// página-pilar, então sem useAbaNaUrl (mesmo critério já usado nas sub-abas de TrabalhadorDetalhePage).
export function AcidentesPage({ tipoFixo }: { tipoFixo?: number } = {}) {
  const navigate = useNavigate();
  const [obraFiltro] = useFiltroObra();
  const [acidentes, setAcidentes] = useState<Acidente[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [nova, setNova] = useState<NovoAcidente>(novaInicial());
  // Suporta abrir a tela já filtrada por tipo via URL (?tipo=3) — legado, de quando "Acidentes",
  // "Incidentes" e "Quase-acidentes" eram itens de menu apontando pra essa mesma tela. Hoje viraram
  // abas de OcorrenciasPage, que passa tipoFixo diretamente (sem depender de querystring).
  const [searchParams] = useSearchParams();
  const [filtroStatus, setFiltroStatus] = useState<string>('');
  const [filtroTipo, setFiltroTipo] = useState<string>(
    tipoFixo != null ? String(tipoFixo) : searchParams.get('tipo') ?? '',
  );
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [aba, setAba] = useState<'ocorrencias' | 'hht'>('ocorrencias');
  const [camposIa, setCamposIa] = useState<Set<CampoIa>>(new Set());
  const [editados, setEditados] = useState<Set<CampoIa>>(new Set());
  const [avisosIa, setAvisosIa] = useState<string[]>([]);
  // Pessoas que não são funcionárias: as do formulário e as que a IA sugeriu (nada entra sem o técnico aceitar).
  const [pessoasExternas, setPessoasExternas] = useState<PessoaExternaEmEdicao[]>([]);
  const [pessoasSugeridas, setPessoasSugeridas] = useState<PessoaExternaSugerida[]>([]);
  const [sugestoesIgnoradas, setSugestoesIgnoradas] = useState<Set<string>>(new Set());
  const [avisoTipo, setAvisoTipo] = useState<string | null>(null);
  // Funcionários que foram para Testemunhas só porque o tipo é "com pessoa externa": voltam a Envolvidos se o tipo mudar.
  const [testemunhasAutomaticas, setTestemunhasAutomaticas] = useState<string[]>([]);
  const AVISO_TESTEMUNHAS = 'Funcionários citados foram marcados como testemunhas; ajuste se algum deles se feriu.';
  // Etapa 2 do registro por relato: análise de causas + plano de ação sugeridos pela IA.
  const [versaoRelato, setVersaoRelato] = useState(0);
  const [metodologia, setMetodologia] = useState<number | null>(null);
  const [causas, setCausas] = useState('');
  const [acoesPlano, setAcoesPlano] = useState<AcaoPlanoEmEdicao[]>([]);
  const [temaDds, setTemaDds] = useState<TemaDdsEmEdicao | null>(null);
  const [reuniao, setReuniao] = useState<ReuniaoEmEdicao | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [gerandoPlano, setGerandoPlano] = useState(false);

  // Alteração feita pelo técnico: tira o selo "IA" e protege o campo nas próximas rodadas de perguntas.
  function alterar(campo: CampoIa, patch: Partial<NovoAcidente>) {
    setNova((atual) => ({ ...atual, ...patch }));
    setCamposIa((atual) => {
      if (!atual.has(campo)) return atual;
      const proximo = new Set(atual);
      proximo.delete(campo);
      return proximo;
    });
    setEditados((atual) => new Set(atual).add(campo));
  }

  // "Ocorrência com pessoa externa" não tem funcionário envolvido (quem estava presente é testemunha) nem
  // afastamento/CAT/dias: ao escolher esse tipo, os funcionários marcados passam a testemunhas e os campos
  // de afastamento voltam ao padrão. Só existe na criação.
  function mudarTipo(tipo: number) {
    if (!ehPessoaExterna(tipo)) {
      if (ehPessoaExterna(nova.tipo) && testemunhasAutomaticas.length > 0) {
        const voltam = testemunhasAutomaticas;
        alterar('tipo', {
          tipo,
          trabalhadoresIds: [...new Set([...(nova.trabalhadoresIds ?? []), ...voltam])],
          testemunhas: (nova.testemunhas ?? []).filter((id) => !voltam.includes(id)),
        });
        setTestemunhasAutomaticas([]);
        setAvisoTipo(null);
      } else {
        alterar('tipo', { tipo });
      }
      return;
    }
    const envolvidos = nova.trabalhadoresIds ?? [];
    alterar('tipo', {
      tipo,
      trabalhadoresIds: [],
      testemunhas: [...new Set([...(nova.testemunhas ?? []), ...envolvidos])],
      houveAfastamento: false,
      diasAfastamento: undefined,
      numeroCat: '',
      gravidade: GravidadeAcidente.SemAfastamento,
      diasDebitadosInformados: undefined,
    });
    if (envolvidos.length > 0) {
      setTestemunhasAutomaticas((atual) => [...new Set([...atual, ...envolvidos])]);
      setAvisoTipo(AVISO_TESTEMUNHAS);
    }
  }

  function adicionarPessoaSugerida(s: PessoaExternaSugerida) {
    if (pessoasExternas.length >= MAXIMO_PESSOAS_EXTERNAS) {
      setErro(`Limite de ${MAXIMO_PESSOAS_EXTERNAS} pessoas externas por ocorrência.`);
      return;
    }
    setPessoasExternas((atuais) => [...atuais, pessoaDaSugestao(s)]);
  }

  function ignorarPessoaSugerida(s: PessoaExternaSugerida) {
    setSugestoesIgnoradas((atual) => new Set(atual).add(normalizarNome(s.nome)));
  }

  // A pessoa citada é funcionário: se o tipo é "com pessoa externa" ele não serve mais (funcionário seria
  // envolvido), então volta para Acidente; depois rola até a lista de funcionários.
  function sugestaoEhFuncionario(s: PessoaExternaSugerida) {
    ignorarPessoaSugerida(s);
    if (ehPessoaExterna(nova.tipo)) {
      mudarTipo(TipoOcorrencia.Acidente);
      setAvisoTipo('O tipo voltou para Acidente porque há funcionário envolvido. Confira o tipo e marque o funcionário na lista.');
    }
    window.setTimeout(() => document.getElementById('oco-campo-funcionarios')?.scrollIntoView({ behavior: 'smooth', block: 'center' }), 50);
  }

  const sugestoesVisiveis = pessoasSugeridas.filter(
    (s) =>
      !sugestoesIgnoradas.has(normalizarNome(s.nome)) &&
      !pessoasExternas.some((p) => normalizarNome(p.nome) === normalizarNome(s.nome)),
  );

  // O que o técnico preencheu à mão vai para a IA junto das respostas, para ela não perguntar de novo.
  // Tipo, gravidade e descrição ficam de fora: não viram perguntas.
  function camposPreenchidosAMao(): RespostaPerguntaRelato[] {
    const rotulos: Partial<Record<CampoIa, string>> = {
      local: 'Local', data: 'Data', hora: 'Hora', lesao: 'Lesão', consequencia: 'Consequência',
      atendimento: 'Atendimento', atividadeId: 'Atividade', trabalhadoresIds: 'Funcionários envolvidos',
      houveAfastamento: 'Houve afastamento', diasAfastamento: 'Dias de afastamento',
    };
    const valor = (campo: CampoIa): string => {
      switch (campo) {
        case 'atividadeId': return atividades.find((a) => a.id === nova.atividadeId)?.nome ?? '';
        case 'trabalhadoresIds':
          return (nova.trabalhadoresIds ?? []).map((id) => trabalhadores.find((t) => t.id === id)?.nome).filter(Boolean).join(', ');
        case 'houveAfastamento': return nova.houveAfastamento ? 'sim' : 'não';
        case 'diasAfastamento': return nova.diasAfastamento != null ? String(nova.diasAfastamento) : '';
        default: return String(nova[campo] ?? '');
      }
    };
    return [...editados]
      .filter((campo) => rotulos[campo])
      .map((campo) => ({ pergunta: `${rotulos[campo]} (preenchido pelo técnico no formulário)`, resposta: valor(campo).trim() }))
      .filter((c) => c.resposta);
  }

  const aplicarSugestao = useCallback(
    (s: RelatoOcorrenciaSugestao, primeiraRodada: boolean) => {
      const protegidos = primeiraRodada ? new Set<CampoIa>() : editados;
      const preenchidos = new Set<CampoIa>();
      setNova((atual) => {
        const proxima = { ...atual };
        const definir = <K extends CampoIa>(campo: K, valor: NovoAcidente[K] | null | undefined, vazio: NovoAcidente[K]) => {
          if (protegidos.has(campo)) return;
          if (valor === null || valor === undefined || valor === '') {
            // Relato novo substitui o formulário; nas rodadas de perguntas, "não sei" não apaga nada.
            if (primeiraRodada) proxima[campo] = vazio;
            return;
          }
          proxima[campo] = valor;
          preenchidos.add(campo);
        };
        definir('tipo', s.tipo, 1);
        definir('atividadeId', s.atividadeId, '');
        definir('local', s.local, '');
        definir('data', s.data, '');
        definir('hora', s.hora, '');
        definir('descricao', s.descricao, '');
        definir('lesao', s.lesao, '');
        definir('consequencia', s.consequencia, '');
        definir('atendimento', s.atendimento, '');
        definir('houveAfastamento', s.houveAfastamento, false);
        definir('diasAfastamento', s.diasAfastamento ?? undefined, undefined);
        definir('gravidade', s.gravidade, GravidadeAcidente.SemAfastamento);
        if (!protegidos.has('trabalhadoresIds') && (primeiraRodada || s.trabalhadoresIds.length > 0)) {
          proxima.trabalhadoresIds = s.trabalhadoresIds;
          if (s.trabalhadoresIds.length > 0) preenchidos.add('trabalhadoresIds');
        }
        // Tipo "com pessoa externa" não aceita funcionário envolvido: quem a IA achou entre os funcionários
        // vira testemunha (o técnico revisa) e os campos de afastamento voltam ao padrão.
        if (ehPessoaExterna(proxima.tipo)) {
          proxima.testemunhas = [...new Set([...(proxima.testemunhas ?? []), ...(proxima.trabalhadoresIds ?? [])])];
          proxima.trabalhadoresIds = [];
          proxima.houveAfastamento = false;
          proxima.diasAfastamento = undefined;
          proxima.gravidade = GravidadeAcidente.SemAfastamento;
        }
        return proxima;
      });
      setCamposIa((atual) => {
        const base = primeiraRodada ? new Set<CampoIa>() : new Set([...atual].filter((c) => !protegidos.has(c)));
        preenchidos.forEach((c) => base.add(c));
        return base;
      });
      if (primeiraRodada) {
        setEditados(new Set());
        setSugestoesIgnoradas(new Set());
        setAvisoTipo(null);
        setTestemunhasAutomaticas([]);
      }
      // Servidor sem o campo: lista vazia. As sugestões só viram cartão se o técnico aceitar.
      setPessoasSugeridas(s.pessoasExternasSugeridas ?? []);
      if (!protegidos.has('tipo') && ehPessoaExterna(s.tipo) && s.trabalhadoresIds.length > 0) {
        setTestemunhasAutomaticas((atual) => [...new Set([...atual, ...s.trabalhadoresIds])]);
        setAvisoTipo(AVISO_TESTEMUNHAS);
      }
      setAvisosIa(s.avisos);
      setErro(null);
      setVersaoRelato((v) => v + 1);
    },
    [editados],
  );

  const aoSoTranscrever = useCallback((transcricao: string) => {
    setNova((atual) => ({ ...atual, descricao: transcricao }));
    setCamposIa(new Set<CampoIa>(['descricao']));
    setAvisosIa(['A fala foi transcrita na descrição, mas a IA não conseguiu classificar. Complete os outros campos.']);
    setVersaoRelato((v) => v + 1);
  }, []);

  async function gerarPlano() {
    if (!nova.obraId || !nova.descricao.trim()) return;
    try {
      setGerandoPlano(true);
      const plano = await api.acidentes.planoSugerido({
        obraId: nova.obraId,
        atividadeId: nova.atividadeId || null,
        tipo: nova.tipo,
        gravidade: nova.gravidade,
        descricao: nova.descricao,
        lesao: nova.lesao || null,
        consequencia: nova.consequencia || null,
        atendimento: nova.atendimento || null,
      });
      setMetodologia(plano.metodologia);
      setCausas(plano.causas ?? '');
      setTemaDds({ nome: plano.temaDds.nome, roteiro: plano.temaDds.roteiro, data: plano.temaDds.data.slice(0, 10) });
      setReuniao(
        plano.reuniao
          ? {
              data: plano.reuniao.inicio.slice(0, 10),
              hora: plano.reuniao.inicio.slice(11, 16),
              duracaoMinutos: plano.reuniao.duracaoMinutos,
              participantes: plano.reuniao.participantes,
              aviso: plano.reuniao.aviso,
            }
          : null,
      );
      setAcoesPlano(
        plano.acoes.map((a) => ({
          chave: novaChaveAcao(),
          tipo: a.tipo,
          descricao: a.descricao,
          prioridade: a.prioridade,
          prazo: a.prazo.slice(0, 10),
          responsavelUsuarioId: a.responsavelUsuarioId ?? '',
          papelResponsavel: a.papelResponsavel,
          avisoResponsavel: a.avisoResponsavel,
          fundamentacao: a.fundamentacao,
          baseConfirmada: a.baseConfirmada,
          sugeridaPelaIa: true,
          resolucao: resolucaoDoVinculo(a.vinculo),
        })),
      );
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'A IA não conseguiu montar o plano de ação agora.');
    } finally {
      setGerandoPlano(false);
    }
  }

  // Cada nova rodada do relato (preenchimento ou respostas às perguntas) refaz a análise com o
  // formulário já atualizado — o efeito roda depois do render, com o `nova` novo.
  useEffect(() => {
    if (versaoRelato > 0) void gerarPlano();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [versaoRelato]);

  useEffect(() => {
    api.usuarios
      .listar(1)
      .then(setUsuarios)
      .catch(() => setUsuarios([]));
  }, []);

  function rotulo(texto: string, campo: CampoIa): ReactElement | string {
    return camposIa.has(campo) ? (
      <span style={{ display: 'inline-flex', gap: 6, alignItems: 'center' }}>
        {texto} <span style={seloIa}>IA</span>
      </span>
    ) : (
      texto
    );
  }

  const estiloCampo = (campo: CampoIa) => (camposIa.has(campo) ? destaqueIa : undefined);
  const nomesTestemunhas = trabalhadores
    .filter((t) => nova.testemunhas?.includes(t.id))
    .map((t) => (t.matricula ? `${t.nome} (${t.matricula})` : t.nome));
  const nomesSelecionados = trabalhadores
    .filter((t) => nova.trabalhadoresIds?.includes(t.id))
    .map((t) => (t.matricula ? `${t.nome} (${t.matricula})` : t.nome));

  async function carregar() {
    try {
      setErro(null);
      const [listaAcidentes, listaObras, listaAtividades] = await Promise.all([
        api.acidentes.listar({
          status: filtroStatus ? Number(filtroStatus) : undefined,
          tipo: filtroTipo ? Number(filtroTipo) : undefined,
        }),
        api.obras.listar(),
        api.atividades.listar(),
      ]);
      setAcidentes(listaAcidentes);
      setObras(listaObras);
      setAtividades(listaAtividades);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar acidentes.');
    }
  }

  // Funcionários envolvidos são sempre da obra escolhida (200+ no total não cabem num select único).
  useEffect(() => {
    if (!nova.obraId) {
      setTrabalhadores([]);
      return;
    }
    api.trabalhadores
      .listar(nova.obraId)
      .then(setTrabalhadores)
      .catch(() => setTrabalhadores([]));
  }, [nova.obraId]);

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroStatus, filtroTipo]);

  async function criar() {
    if (
      !conferirPendencias([
        { nome: 'Obra', dica: 'Selecione a obra da ocorrência', ok: !!nova.obraId, alvo: 'oco-campo-obra' },
        { nome: 'Local', dica: 'Onde aconteceu', ok: !!nova.local.trim(), alvo: 'oco-campo-local' },
        { nome: 'Data', dica: 'Quando aconteceu', ok: !!nova.data, alvo: 'oco-campo-data' },
        { nome: 'Descrição', dica: 'Conte o que aconteceu', ok: !!nova.descricao.trim(), alvo: 'oco-campo-descricao' },
        {
          nome: 'Pessoa externa',
          dica: 'Este tipo precisa de ao menos uma pessoa que não é funcionária',
          ok: !ehPessoaExterna(nova.tipo) || pessoasExternas.length > 0,
          alvo: 'oco-campo-pessoas-externas',
        },
        ...pessoasExternas.flatMap((p, i) =>
          pendenciasPessoaExterna(p, i).map((item) => ({ nome: item.nome, dica: item.dica, ok: false, alvo: item.alvo })),
        ),
      ])
    ) {
      return;
    }
    // Registro por relato: o tema do DDS do dia seguinte é obrigatório (regra do usuário, 08/10/2026).
    if (versaoRelato > 0 && (!temaDds?.nome.trim() || !temaDds.roteiro.trim())) {
      setErro(gerandoPlano ? 'Aguarde a IA terminar o plano de ação.' : 'Informe o tema e o roteiro do DDS do dia seguinte.');
      return;
    }
    // Sem conclusão manual: cada ação do plano precisa de responsável e de "como se resolve".
    for (const a of acoesPlano.filter((x) => x.descricao.trim())) {
      const pendencia = !a.responsavelUsuarioId ? 'Designe quem vai resolver a ação' : pendenciaResolucao(a.resolucao);
      if (pendencia) {
        setErro(`Ação "${a.descricao.trim().slice(0, 60)}": ${pendencia.charAt(0).toLowerCase()}${pendencia.slice(1)}`);
        return;
      }
    }
    const enviarReuniao = versaoRelato > 0 && exigeReuniao(nova.tipo);
    if (enviarReuniao && (!reuniao?.data || !reuniao.hora)) {
      setErro(
        reuniao
          ? 'Informe a data e o horário da reunião de análise do acidente.'
          : 'Para acidente, gere o plano novamente para sugerir a reunião de análise.',
      );
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const tipoExterno = ehPessoaExterna(nova.tipo);
      await api.acidentes.criar({
        ...nova,
        // Tipo "com pessoa externa": sem funcionário envolvido, sem afastamento/CAT/dias (o servidor também fixa isso).
        trabalhadoresIds: tipoExterno ? [] : nova.trabalhadoresIds ?? [],
        testemunhas: (nova.testemunhas ?? []).filter((id) => !(nova.trabalhadoresIds ?? []).includes(id)),
        pessoasExternas: pessoasExternas.map(pessoaParaPayload),
        gravidade: tipoExterno ? GravidadeAcidente.SemAfastamento : nova.gravidade,
        diasDebitadosInformados: tipoExterno ? undefined : nova.diasDebitadosInformados,
        houveAfastamento: tipoExterno ? false : nova.houveAfastamento,
        atividadeId: nova.atividadeId || null,
        // input type="time" retorna "HH:mm"; TimeSpan? no backend exige segundos ("HH:mm:ss").
        hora: nova.hora ? `${nova.hora}:00` : null,
        lesao: nova.lesao || null,
        consequencia: nova.consequencia || null,
        atendimento: nova.atendimento || null,
        diasAfastamento: !tipoExterno && nova.houveAfastamento ? nova.diasAfastamento ?? null : null,
        numeroCat: tipoExterno ? null : nova.numeroCat || null,
        causas: versaoRelato > 0 ? causas.trim() || null : nova.causas || null,
        metodologiaInvestigacao: versaoRelato > 0 ? metodologia : nova.metodologiaInvestigacao ?? null,
        acoesPlano: acoesPlano
          .filter((a) => a.descricao.trim())
          .map((a) => ({
            tipo: a.tipo,
            descricao: a.descricao.trim(),
            prioridade: a.prioridade,
            prazo: a.prazo || null,
            responsavelUsuarioId: a.responsavelUsuarioId || null,
            fundamentacao: a.fundamentacao,
            ...payloadAcaoOcorrencia(a.resolucao),
          })),
        temaDds: versaoRelato > 0 && temaDds ? { nome: temaDds.nome.trim(), roteiro: temaDds.roteiro.trim() } : undefined,
        reuniao:
          enviarReuniao && reuniao
            ? {
                inicio: `${reuniao.data}T${reuniao.hora}:00`,
                duracaoMinutos: reuniao.duracaoMinutos,
                participantesUsuarioIds: reuniao.participantes.map((p) => p.usuarioId),
              }
            : undefined,
      });
      setNova(novaInicial());
      setCamposIa(new Set());
      setEditados(new Set());
      setAvisosIa([]);
      setPessoasExternas([]);
      setPessoasSugeridas([]);
      setSugestoesIgnoradas(new Set());
      setAvisoTipo(null);
      setTestemunhasAutomaticas([]);
      setVersaoRelato(0);
      setMetodologia(null);
      setCausas('');
      setAcoesPlano([]);
      setTemaDds(null);
      setReuniao(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar ocorrência.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<Acidente>[] = [
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoOcorrenciaLabel[a.tipo] },
    { chave: 'obra', rotulo: 'Obra', render: (a) => a.obraNome ?? '—' },
    { chave: 'funcionario', rotulo: 'Funcionário / pessoa', render: (a) => {
        const nomes = ehPessoaExterna(a.tipo)
          ? (a.pessoasExternas ?? []).map((p) => p.nome)
          : a.envolvidos.map((e) => e.nome);
        if (nomes.length === 0) return '—';
        return nomes.length === 1 ? nomes[0] : `${nomes[0]} +${nomes.length - 1}`;
      },
    },
    { chave: 'data', rotulo: 'Data', render: (a) => dataBr(a.data) },
    { chave: 'local', rotulo: 'Local' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => <StatusChip tom={tomPorStatusAcidente[a.status] ?? 'neutro'}>{statusAcidenteLabel[a.status]}</StatusChip>,
    },
    { chave: 'gravidade', rotulo: 'Gravidade', render: (a) => (ehPessoaExterna(a.tipo) ? '—' : gravidadeAcidenteLabel[a.gravidade]) },
  ];

  return (
    <div>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Abas
        nivel="interno"
        abas={[
          { valor: 'ocorrencias', rotulo: 'Acidentes & Incidentes' },
          { valor: 'hht', rotulo: 'HHT Mensal' },
        ]}
        valor={aba}
        aoMudar={setAba}
      />

      {aba === 'hht' && <HhtMensalTab obras={obras} />}

      {aba === 'ocorrencias' && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card titulo="Registrar acidente / incidente">
            {/* minmax(0, 1fr): sem isso o grid cresce com o texto do aviso em vez de quebrar a linha. */}
            <div style={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr)', gap: 12, marginBottom: 16 }}>
              <div id="oco-campo-obra" style={{ maxWidth: 420 }}>
                <Field label="Obra" required>
                  <Select value={nova.obraId} onChange={(_, d) => { setNova({ ...nova, obraId: d.value, trabalhadoresIds: [], testemunhas: [] }); setTestemunhasAutomaticas([]); }}>
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <RelatoOcorrenciaIa
                obraId={nova.obraId}
                desabilitado={carregando}
                aoSugerir={aplicarSugestao}
                aoSoTranscrever={aoSoTranscrever}
                aoErro={setErro}
                camposPreenchidos={camposPreenchidosAMao}
              />
              {camposIa.size > 0 && (
                <FeedbackInline tom="info">
                  A IA preencheu o registro a partir do relato. Revise cada campo antes de registrar: tipo, gravidade
                  e afastamento alimentam os indicadores da NBR 14280.
                </FeedbackInline>
              )}
              {sugestoesVisiveis.map((s, i) => (
                <CartaoPessoaExternaSugerida
                  key={`${normalizarNome(s.nome)}-${i}`}
                  sugestao={s}
                  aoAdicionar={() => adicionarPessoaSugerida(s)}
                  aoEscolherFuncionario={() => sugestaoEhFuncionario(s)}
                  aoIgnorar={() => ignorarPessoaSugerida(s)}
                />
              ))}
              {avisoTipo && (
                <FeedbackInline tom="info" aoFechar={() => setAvisoTipo(null)}>
                  {avisoTipo}
                </FeedbackInline>
              )}
              {avisosIa.length > 0 && (
                <FeedbackInline tom="aviso" aoFechar={() => setAvisosIa([])}>
                  {avisosIa.map((a) => (
                    <div key={a}>{a}</div>
                  ))}
                </FeedbackInline>
              )}
            </div>
            <FormSection titulo="Dados Gerais da Ocorrência" numero={1} primeira>
              <FormGrid>
                <Campo span={2}>
                  <Field label={rotulo('Tipo', 'tipo')} required>
                    <Select select={{ style: estiloCampo('tipo') }} value={String(nova.tipo)} onChange={(_, d) => mudarTipo(Number(d.value))}>
                      {Object.entries(tipoOcorrenciaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Atividade', 'atividadeId')}>
                    <Select
                      select={{ style: estiloCampo('atividadeId') }}
                      value={nova.atividadeId ?? ''}
                      onChange={(_, d) => alterar('atividadeId', { atividadeId: d.value })}
                    >
                      <option value="">Nenhuma</option>
                      {atividades.map((atividade) => (
                        <option key={atividade.id} value={atividade.id}>
                          {atividade.nome}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={4} id="oco-campo-local">
                  <Field label={rotulo('Local', 'local')} required>
                    <Input style={estiloCampo('local')} value={nova.local} onChange={(_, d) => alterar('local', { local: d.value })} />
                  </Field>
                </Campo>
                <Campo span={2} id="oco-campo-data">
                  <Field label={rotulo('Data', 'data')} required>
                    <CampoData style={estiloCampo('data')} value={nova.data} onChange={(_, d) => alterar('data', { data: d.value })} />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label={rotulo('Hora', 'hora')}>
                    <Input style={estiloCampo('hora')} type="time" value={nova.hora ?? ''} onChange={(_, d) => alterar('hora', { hora: d.value })} />
                  </Field>
                </Campo>
                {!ehPessoaExterna(nova.tipo) && (
                <Campo span={12} id="oco-campo-funcionarios">
                  <Field
                    label={rotulo(`Funcionários envolvidos${(nova.trabalhadoresIds?.length ?? 0) > 0 ? ` (${nova.trabalhadoresIds?.length})` : ''}`, 'trabalhadoresIds')}
                    hint={
                      !nova.obraId
                        ? undefined
                        : nomesSelecionados.length > 0
                          // A lista tem centenas de nomes: sem isso, quem a IA marcou fica escondido no meio dela.
                          ? `Selecionados: ${nomesSelecionados.join(', ')}`
                          : 'Selecione todos os funcionários envolvidos na ocorrência. Deixe vazio se não houve funcionário envolvido.'
                    }
                  >
                    {nova.obraId ? (
                      <ListaSelecaoMultipla
                        aria-label="Funcionários envolvidos"
                        opcoes={trabalhadores
                          .filter((t) => !(nova.testemunhas ?? []).includes(t.id))
                          .map((t) => ({ id: t.id, rotulo: t.matricula ? `${t.nome} (${t.matricula})` : t.nome }))}
                        selecionados={nova.trabalhadoresIds ?? []}
                        aoMudar={(atualizar) => alterar('trabalhadoresIds', { trabalhadoresIds: atualizar(nova.trabalhadoresIds ?? []) })}
                      />
                    ) : (
                      <Text>Selecione a obra primeiro.</Text>
                    )}
                  </Field>
                </Campo>
                )}
                <Campo span={12} id="oco-campo-pessoas-externas">
                  <BlocoPessoasExternas
                    pessoas={pessoasExternas}
                    aoMudarLista={setPessoasExternas}
                    tipoPessoaExterna={ehPessoaExterna(nova.tipo)}
                  />
                </Campo>
                <Campo span={12}>
                  <Field
                    label={`Testemunhas (funcionários que presenciaram)${(nova.testemunhas?.length ?? 0) > 0 ? ` (${nova.testemunhas?.length})` : ''}`}
                    hint={
                      !nova.obraId
                        ? undefined
                        : nomesTestemunhas.length > 0
                          ? `Selecionadas: ${nomesTestemunhas.join(', ')}`
                          : 'Quem viu a ocorrência mas não se envolveu. Testemunha não vai para o G-RH nem para a taxa de gravidade.'
                    }
                  >
                    {nova.obraId ? (
                      <ListaSelecaoMultipla
                        aria-label="Testemunhas"
                        opcoes={trabalhadores
                          .filter((t) => !(nova.trabalhadoresIds ?? []).includes(t.id))
                          .map((t) => ({ id: t.id, rotulo: t.matricula ? `${t.nome} (${t.matricula})` : t.nome }))}
                        selecionados={nova.testemunhas ?? []}
                        aoMudar={(atualizar) => {
                          setNova((atual) => ({ ...atual, testemunhas: atualizar(atual.testemunhas ?? []) }));
                          setTestemunhasAutomaticas([]);
                        }}
                      />
                    ) : (
                      <Text>Selecione a obra primeiro.</Text>
                    )}
                  </Field>
                </Campo>
              </FormGrid>
            </FormSection>

            <FormSection titulo="Lesão, Gravidade e Consequências" numero={2}>
              <FormGrid>
                <Campo span={12} id="oco-campo-descricao">
                  <Field label={rotulo('Descrição', 'descricao')} required>
                    <Textarea style={estiloCampo('descricao')} resize="vertical" rows={5} value={nova.descricao} onChange={(_, d) => alterar('descricao', { descricao: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Lesão', 'lesao')}>
                    <Input style={estiloCampo('lesao')} value={nova.lesao ?? ''} onChange={(_, d) => alterar('lesao', { lesao: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Consequência', 'consequencia')}>
                    <Input style={estiloCampo('consequencia')} value={nova.consequencia ?? ''} onChange={(_, d) => alterar('consequencia', { consequencia: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label={rotulo('Atendimento prestado', 'atendimento')}>
                    <Input style={estiloCampo('atendimento')} value={nova.atendimento ?? ''} onChange={(_, d) => alterar('atendimento', { atendimento: d.value })} />
                  </Field>
                </Campo>
                {/* Mapa corporal: só quando há lesão (acidente, doença ocupacional ou lesão informada). */}
                {(nova.tipo === TipoOcorrencia.Acidente || nova.tipo === TipoOcorrencia.DoencaOcupacional || Boolean(nova.lesao?.trim()) ||
                  (ehPessoaExterna(nova.tipo) && pessoasExternas.some((p) => p.resultado !== 1))) && (
                  <Campo span={12}>
                    <Field label="Partes do corpo atingidas">
                      <MapaCorporal
                        selecionadas={nova.regioesCorpo ?? []}
                        aoAlterar={(regioesCorpo) => setNova((atual) => ({ ...atual, regioesCorpo }))}
                      />
                    </Field>
                  </Campo>
                )}
                {!ehPessoaExterna(nova.tipo) && (<>
                <Campo span={3}>
                  <Field label={rotulo('Houve afastamento?', 'houveAfastamento')}>
                    <Select
                      select={{ style: estiloCampo('houveAfastamento') }}
                      value={nova.houveAfastamento ? '1' : '0'}
                      onChange={(_, d) => alterar('houveAfastamento', { houveAfastamento: d.value === '1' })}
                    >
                      <option value="0">Não</option>
                      <option value="1">Sim</option>
                    </Select>
                  </Field>
                </Campo>
                {nova.houveAfastamento && (
                  <Campo span={3}>
                    <Field label={rotulo('Dias de afastamento', 'diasAfastamento')}>
                      <Input
                        style={estiloCampo('diasAfastamento')}
                        type="number"
                        min={0}
                        value={nova.diasAfastamento?.toString() ?? ''}
                        onChange={(_, d) => alterar('diasAfastamento', { diasAfastamento: d.value ? Number(d.value) : undefined })}
                      />
                    </Field>
                  </Campo>
                )}
                <Campo span={3}>
                  <Field label={rotulo('Gravidade', 'gravidade')} required>
                    <Select
                      select={{ style: estiloCampo('gravidade') }}
                      value={String(nova.gravidade)}
                      onChange={(_, d) =>
                        alterar('gravidade', { gravidade: Number(d.value), diasDebitadosInformados: undefined })
                      }
                    >
                      {Object.entries(gravidadeAcidenteLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                {nova.gravidade === GravidadeAcidente.IncapacidadePermanenteParcial && (
                  <Campo span={6}>
                    <Field
                      label="Dias Debitados (consultar Quadro III da NBR 14280)"
                      required
                      hint="Valor não calculado automaticamente pelo sistema — consulte a tabela oficial de Dias Debitados por lesão/parte do corpo."
                    >
                      <Input
                        type="number"
                        min={1}
                        value={nova.diasDebitadosInformados?.toString() ?? ''}
                        onChange={(_, d) =>
                          setNova({ ...nova, diasDebitadosInformados: d.value ? Number(d.value) : undefined })
                        }
                      />
                    </Field>
                  </Campo>
                )}
                {(nova.gravidade === GravidadeAcidente.Obito ||
                  nova.gravidade === GravidadeAcidente.IncapacidadePermanenteTotal) && (
                  <Campo span={3}>
                    <Field label="Dias Debitados">
                      <Text>6.000 dias (fixo, calculado automaticamente)</Text>
                    </Field>
                  </Campo>
                )}
                <Campo span={3}>
                  <Field label="Número da CAT">
                    <Input value={nova.numeroCat ?? ''} onChange={(_, d) => setNova({ ...nova, numeroCat: d.value })} />
                  </Field>
                </Campo>
                </>)}
              </FormGrid>
            </FormSection>

            {versaoRelato > 0 && (
              <PlanoAcaoOcorrenciaIa
                reuniao={exigeReuniao(nova.tipo) ? reuniao : null}
                aoMudarReuniao={setReuniao}
                temaDds={temaDds}
                aoMudarTemaDds={setTemaDds}
                metodologia={metodologia}
                causas={causas}
                acoes={acoesPlano}
                usuarios={usuarios}
                gerando={gerandoPlano}
                aoMudarMetodologia={setMetodologia}
                aoMudarCausas={setCausas}
                aoMudarAcoes={setAcoesPlano}
                aoGerarNovamente={() => void gerarPlano()}
              />
            )}

            <FormRodape>
              <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando || gerandoPlano}>
                Registrar
              </Button>
            </FormRodape>
          </Card>

          <Card
            titulo="Acidentes e incidentes"
            acoes={
              <div style={{ display: 'flex', gap: 12, alignItems: 'flex-end' }}>
                {tipoFixo == null && (
                  <Field label="Tipo">
                    <Select value={filtroTipo} onChange={(_, d) => setFiltroTipo(d.value)}>
                      <option value="">Todos</option>
                      {Object.entries(tipoOcorrenciaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                )}
                <Field label="Status">
                  <Select value={filtroStatus} onChange={(_, d) => setFiltroStatus(d.value)}>
                    <option value="">Todos</option>
                    {Object.entries(statusAcidenteLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
            }
          >
            <DataTable
              aria-label="Acidentes e incidentes"
              colunas={colunas}
              linhas={obraFiltro ? acidentes.filter((a) => a.obraId === obraFiltro) : acidentes}
              chaveLinha={(a) => a.id}
              vazio={{ titulo: 'Nenhuma ocorrência cadastrada ainda.' }}
              aoClicarLinha={(a) => navigate(`/acidentes/${a.id}`)}
            />
          </Card>
        </div>
      )}
    </div>
  );
}
