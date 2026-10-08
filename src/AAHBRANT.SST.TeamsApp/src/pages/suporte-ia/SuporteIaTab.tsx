import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
// makeStyles/tokens ainda não têm reexport em @ui (lacuna conhecida, ver passo de lint do ci.yml) —
// mesma exceção pontual da CalendarioPage. Os componentes (Input/Textarea) já vêm de @ui.
// oxlint-disable-next-line no-restricted-imports
import { makeStyles, tokens } from '@fluentui/react-components';
import { Mic24Regular, RecordStop24Filled, Send24Regular } from '@fluentui/react-icons';
import {
  api,
  ResultadoTriagemSuporteIa,
  SeveridadeSolicitacaoSuporteIa,
  StatusSolicitacaoSuporteIa,
  TipoSolicitacaoSuporteIa,
  resultadoTriagemSuporteIaLabel,
  severidadeSolicitacaoSuporteIaLabel,
  statusSolicitacaoSuporteIaLabel,
  tipoSolicitacaoSuporteIaLabel,
  type RelatoVozSuporteIa,
  type SuporteIaSolicitacao,
} from '../../lib/api';
import { Button, FeedbackInline, Input, Legenda, Spinner, StatusChip, Textarea } from '@ui';
import { EsteiraSuporteIa } from './EsteiraSuporteIa';
import { etapaAtivaPorStatus } from './statusEtapaSuporteIa';
import { formatarTempoGravacao, useRelatoVoz } from './useRelatoVoz';

type CampoChamado = 'tipo' | 'severidade' | 'titulo' | 'modulo' | 'descricao';

const STATUS_PENDENTES_RESPONSAVEL: number[] = [
  StatusSolicitacaoSuporteIa.Encaminhada,
  StatusSolicitacaoSuporteIa.EmAnaliseTecnica,
  StatusSolicitacaoSuporteIa.Reaberta,
  StatusSolicitacaoSuporteIa.AguardandoValidacao,
];

const useStyles = makeStyles({
  cockpit: {
    display: 'grid',
    gap: '16px',
  },
  faixa: {
    display: 'grid',
    gridTemplateColumns: 'minmax(280px, 0.85fr) minmax(360px, 1.15fr) minmax(280px, 0.9fr)',
    gap: '16px',
    alignItems: 'stretch',
    '@media (max-width: 1180px)': {
      gridTemplateColumns: '1fr 1fr',
    },
    '@media (max-width: 820px)': {
      gridTemplateColumns: '1fr',
    },
  },
  painel: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: '8px',
    background: tokens.colorNeutralBackground1,
    boxShadow: tokens.shadow4,
    overflow: 'hidden',
    minWidth: 0,
  },
  painelCabecalho: {
    display: 'flex',
    justifyContent: 'space-between',
    gap: '12px',
    alignItems: 'flex-start',
    padding: '16px 16px 12px',
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    background: tokens.colorNeutralBackground2,
  },
  eyebrow: {
    margin: 0,
    fontSize: '11px',
    fontWeight: 800,
    letterSpacing: '0.04em',
    textTransform: 'uppercase',
    color: tokens.colorNeutralForeground3,
  },
  tituloPainel: {
    margin: '4px 0 0',
    fontSize: '18px',
    lineHeight: '24px',
    fontWeight: 750,
    color: tokens.colorNeutralForeground1,
  },
  corpo: {
    padding: '16px',
  },
  form: {
    display: 'grid',
    gap: '12px',
    // Sem isso o item do grid cresce com mensagem longa (ex.: aviso de microfone bloqueado) em vez
    // de deixar o MessageBar quebrar linha.
    '& > *': { minWidth: 0 },
  },
  linha: {
    display: 'grid',
    gridTemplateColumns: '1fr 1fr',
    gap: '12px',
    '@media (max-width: 680px)': {
      gridTemplateColumns: '1fr',
    },
  },
  campo: {
    display: 'grid',
    gap: '6px',
  },
  label: {
    fontSize: '12px',
    fontWeight: 700,
    color: tokens.colorNeutralForeground2,
  },
  blocoVoz: {
    display: 'grid',
    justifyItems: 'start',
    gap: '8px',
    padding: '12px',
    borderRadius: '8px',
    border: `1px dashed ${tokens.colorBrandStroke1}`,
    background: tokens.colorNeutralBackground3,
  },
  blocoVozTitulo: {
    margin: 0,
    fontSize: '14px',
    fontWeight: 700,
    color: tokens.colorNeutralForeground1,
  },
  dica: {
    fontSize: '12px',
    lineHeight: '16px',
    color: tokens.colorNeutralForeground2,
  },
  ou: {
    fontSize: '11px',
    fontWeight: 700,
    letterSpacing: '0.05em',
    textTransform: 'uppercase',
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
  },
  botaoParar: {
    backgroundColor: tokens.colorPaletteRedBackground3,
    color: tokens.colorNeutralForegroundOnBrand,
    ':hover': {
      backgroundColor: tokens.colorPaletteRedForeground1,
      color: tokens.colorNeutralForegroundOnBrand,
    },
  },
  gravando: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
    width: '100%',
    boxSizing: 'border-box',
    padding: '8px 10px',
    borderRadius: '4px',
    background: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
    fontSize: '13px',
  },
  pontoGravando: {
    width: '10px',
    height: '10px',
    borderRadius: '50%',
    flexShrink: 0,
    background: tokens.colorPaletteRedBackground3,
    animationName: { '50%': { opacity: 0.3 } },
    animationDuration: '1.2s',
    animationIterationCount: 'infinite',
    '@media (prefers-reduced-motion: reduce)': { animationName: 'none' },
  },
  tempoGravando: {
    fontVariantNumeric: 'tabular-nums',
    fontWeight: 700,
  },
  barrasGravando: {
    display: 'flex',
    gap: '2px',
    alignItems: 'center',
    height: '16px',
    flex: 1,
    minWidth: 0,
    overflow: 'hidden',
  },
  barraGravando: {
    width: '3px',
    minHeight: '3px',
    borderRadius: '2px',
    background: tokens.colorPaletteRedBackground3,
    opacity: 0.7,
  },
  spinnerInline: {
    display: 'inline-flex',
    verticalAlign: 'middle',
    marginRight: '6px',
  },
  avisoIa: {
    fontSize: '12px',
    lineHeight: '16px',
    padding: '8px 10px',
    borderRadius: '4px',
    color: tokens.colorNeutralForeground1,
    background: `color-mix(in srgb, ${tokens.colorPaletteGrapeBackground2} 30%, ${tokens.colorNeutralBackground1})`,
  },
  labelComSelo: {
    display: 'flex',
    alignItems: 'center',
    gap: '6px',
  },
  seloIa: {
    fontSize: '10px',
    lineHeight: '14px',
    fontWeight: 700,
    padding: '0 7px',
    borderRadius: '10px',
    color: tokens.colorPaletteGrapeForeground2,
    background: tokens.colorPaletteGrapeBackground2,
  },
  // Tom suave do roxo "IA" sobre o fundo do tema — o Fluent não tem uma tonalidade Grape mais clara.
  campoIa: {
    backgroundColor: `color-mix(in srgb, ${tokens.colorPaletteGrapeBackground2} 30%, ${tokens.colorNeutralBackground1})`,
  },
  select: {
    minHeight: '32px',
    borderRadius: '6px',
    border: `1px solid ${tokens.colorNeutralStroke1}`,
    padding: '0 10px',
    background: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
  },
  ticket: {
    display: 'grid',
    gap: '14px',
  },
  ticketTopo: {
    display: 'flex',
    alignItems: 'center',
    gap: '10px',
  },
  avatarCliente: {
    width: '38px',
    height: '38px',
    borderRadius: '50%',
    display: 'grid',
    placeItems: 'center',
    background: '#10483B',
    color: '#FFFFFF',
    fontSize: '13px',
    fontWeight: 800,
    flexShrink: 0,
  },
  clienteNome: {
    margin: 0,
    fontSize: '14px',
    fontWeight: 750,
    color: tokens.colorNeutralForeground1,
  },
  clienteMeta: {
    margin: 0,
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
  },
  pedidoCliente: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: '8px',
    padding: '14px',
    background: tokens.colorNeutralBackground3,
    display: 'grid',
    gap: '10px',
  },
  pedidoTitulo: {
    margin: 0,
    fontSize: '16px',
    lineHeight: '22px',
    fontWeight: 750,
    color: tokens.colorNeutralForeground1,
  },
  pedidoDescricao: {
    margin: 0,
    whiteSpace: 'pre-wrap',
    fontSize: '13px',
    lineHeight: '19px',
    color: tokens.colorNeutralForeground2,
  },
  meta: {
    display: 'flex',
    gap: '8px',
    flexWrap: 'wrap',
    alignItems: 'center',
  },
  resumoLinha: {
    display: 'grid',
    gridTemplateColumns: '104px 1fr',
    gap: '8px',
    fontSize: '12px',
    lineHeight: '18px',
    color: tokens.colorNeutralForeground2,
  },
  chave: {
    fontWeight: 750,
    color: tokens.colorNeutralForeground3,
  },
  vazio: {
    border: `1px dashed ${tokens.colorNeutralStroke1}`,
    borderRadius: '8px',
    padding: '14px',
    color: tokens.colorNeutralForeground3,
    fontSize: '13px',
    lineHeight: '20px',
    background: tokens.colorNeutralBackground2,
  },
  resultado: {
    display: 'grid',
    gap: '12px',
  },
  blocoTexto: {
    whiteSpace: 'pre-wrap',
    fontSize: '13px',
    color: tokens.colorNeutralForeground1,
    lineHeight: 1.5,
  },
  trilha: {
    display: 'grid',
    gap: '8px',
    marginTop: '14px',
  },
  passo: {
    display: 'grid',
    gridTemplateColumns: '18px 1fr',
    gap: '8px',
    alignItems: 'start',
    fontSize: '12px',
    color: tokens.colorNeutralForeground2,
  },
  ponto: {
    width: '10px',
    height: '10px',
    marginTop: '4px',
    borderRadius: '50%',
    background: '#1B6B55',
    boxShadow: '0 0 0 4px rgba(27, 107, 85, 0.14)',
  },
  historicoPainel: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: '8px',
    background: tokens.colorNeutralBackground1,
    overflow: 'hidden',
  },
  historicoCabecalho: {
    display: 'flex',
    justifyContent: 'space-between',
    gap: '12px',
    alignItems: 'center',
    padding: '14px 16px',
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    background: tokens.colorNeutralBackground2,
  },
  historicoTitulo: {
    margin: 0,
    fontSize: '16px',
    fontWeight: 750,
    color: tokens.colorNeutralForeground1,
  },
  historico: {
    display: 'grid',
  },
  itemHistorico: {
    display: 'grid',
    gridTemplateColumns: 'minmax(220px, 1fr) minmax(160px, 0.65fr) minmax(140px, 0.5fr)',
    gap: '12px',
    padding: '14px 16px',
    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    alignItems: 'center',
    cursor: 'pointer',
    background: 'none',
    border: 0,
    borderTopWidth: '1px',
    width: '100%',
    textAlign: 'left',
    font: 'inherit',
    color: 'inherit',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground2,
    },
    ':first-child': {
      borderTop: 0,
    },
    '@media (max-width: 820px)': {
      gridTemplateColumns: '1fr',
    },
  },
  itemTitulo: {
    display: 'grid',
    gap: '4px',
  },
  forte: {
    fontSize: '14px',
    fontWeight: 750,
    color: tokens.colorNeutralForeground1,
  },
  data: {
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
  },
});

function textoOuPlaceholder(valor: string, placeholder: string) {
  return valor.trim().length > 0 ? valor.trim() : placeholder;
}

function formatarData(valor: string) {
  return new Date(valor).toLocaleString('pt-BR', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function SuporteIaTab() {
  const estilos = useStyles();
  const navigate = useNavigate();
  const [tipo, setTipo] = useState<number>(TipoSolicitacaoSuporteIa.Duvida);
  const [severidade, setSeveridade] = useState<number>(SeveridadeSolicitacaoSuporteIa.Media);
  const [titulo, setTitulo] = useState('');
  const [modulo, setModulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<SuporteIaSolicitacao | null>(null);
  const [historico, setHistorico] = useState<SuporteIaSolicitacao[]>([]);
  const [filaResponsavel, setFilaResponsavel] = useState<SuporteIaSolicitacao[] | null>(null);
  // Campos que a IA preencheu a partir do relato falado — mostram o selo "IA" até o usuário editá-los.
  const [camposIa, setCamposIa] = useState<Set<CampoChamado>>(new Set());
  const [avisoIa, setAvisoIa] = useState<string | null>(null);
  const etapaAtual = resultado ? etapaAtivaPorStatus(resultado.status) : 0;

  // "Relatar por voz" substitui o formulário pelo novo relato. Se a IA não conseguir classificar, a
  // fala transcrita vai para a descrição e o usuário completa o resto.
  const relatoVoz = useRelatoVoz(
    (relato: RelatoVozSuporteIa) => {
      setErro(null);
      setResultado(null);
      if (relato.sugestao) {
        setTipo(relato.sugestao.tipo);
        setSeveridade(relato.sugestao.severidade);
        setTitulo(relato.sugestao.titulo);
        setModulo(relato.sugestao.modulo ?? '');
        setDescricao(relato.sugestao.descricao);
        setCamposIa(new Set<CampoChamado>(['tipo', 'severidade', 'titulo', 'modulo', 'descricao']));
        setAvisoIa('A IA preencheu o chamado. Confira e clique em Abrir chamado. Você pode corrigir qualquer campo.');
      } else {
        setDescricao(relato.transcricao);
        setCamposIa(new Set<CampoChamado>(['descricao']));
        setAvisoIa('A fala foi transcrita na descrição, mas a IA não conseguiu classificar. Complete os outros campos.');
      }
    },
    (mensagem) => setErro(mensagem),
  );
  const ocupadoComVoz = relatoVoz.estado !== 'parado';

  function editadoPeloUsuario(campo: CampoChamado) {
    setCamposIa((atual) => {
      if (!atual.has(campo)) return atual;
      const proximo = new Set(atual);
      proximo.delete(campo);
      return proximo;
    });
  }

  function rotuloCampo(texto: string, campo: CampoChamado) {
    return (
      <span className={`${estilos.label} ${estilos.labelComSelo}`}>
        {texto}
        {camposIa.has(campo) && <span className={estilos.seloIa}>IA</span>}
      </span>
    );
  }

  const pedidoPreview = useMemo(
    () => ({
      titulo: textoOuPlaceholder(titulo, 'Pedido do cliente ainda sem título'),
      descricao: textoOuPlaceholder(
        descricao,
        'Assim que o usuário descrever o problema, o texto aparece aqui como um pedido de atendimento pronto para triagem.',
      ),
      modulo: textoOuPlaceholder(modulo, 'Não informado'),
    }),
    [descricao, modulo, titulo],
  );

  async function carregarHistorico() {
    try {
      const dados = await api.suporteIa.listar();
      setHistorico(dados);
    } catch {
      setHistorico([]);
    }
  }

  async function carregarFilaResponsavel() {
    try {
      const dados = await api.suporteIa.listarTodos();
      setFilaResponsavel(dados.filter((item) => STATUS_PENDENTES_RESPONSAVEL.includes(item.status)));
    } catch {
      // suporte-ia:administrar negado (usuário sem essa permissão) — a seção do responsável some.
      setFilaResponsavel(null);
    }
  }

  useEffect(() => {
    carregarHistorico();
    carregarFilaResponsavel();
  }, []);

  async function enviar() {
    try {
      setEnviando(true);
      setErro(null);
      const resposta = await api.suporteIa.criar({
        tipo,
        severidadeInformada: severidade,
        titulo,
        descricao,
        modulo: modulo || null,
        urlContexto: window.location.href,
      });
      setResultado(resposta);
      setTitulo('');
      setDescricao('');
      setModulo('');
      setCamposIa(new Set());
      setAvisoIa(null);
      await Promise.all([carregarHistorico(), carregarFilaResponsavel()]);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a solicitação ao suporte IA.');
    } finally {
      setEnviando(false);
    }
  }

  return (
    <div className={estilos.cockpit}>
      <div className={estilos.faixa}>
        <section className={estilos.painel}>
          <div className={estilos.painelCabecalho}>
            <div>
              <p className={estilos.eyebrow}>Pedido do cliente</p>
              <h2 className={estilos.tituloPainel}>Registro original</h2>
            </div>
            <StatusChip tom="info">rascunho</StatusChip>
          </div>
          <div className={estilos.corpo}>
            <div className={estilos.ticket}>
              <div className={estilos.ticketTopo}>
                <div className={estilos.avatarCliente}>CL</div>
                <div>
                  <p className={estilos.clienteNome}>Usuário do SST</p>
                  <p className={estilos.clienteMeta}>Chamado aberto no sistema</p>
                </div>
              </div>

              <div className={estilos.pedidoCliente}>
                <div className={estilos.meta}>
                  <StatusChip tom="neutro">{tipoSolicitacaoSuporteIaLabel[tipo]}</StatusChip>
                  <StatusChip tom={severidade >= SeveridadeSolicitacaoSuporteIa.Alta ? 'alerta' : 'atencao'}>
                    {severidadeSolicitacaoSuporteIaLabel[severidade]}
                  </StatusChip>
                </div>
                <h3 className={estilos.pedidoTitulo}>{pedidoPreview.titulo}</h3>
                <p className={estilos.pedidoDescricao}>{pedidoPreview.descricao}</p>
              </div>

              <div className={estilos.resumoLinha}>
                <span className={estilos.chave}>Módulo</span>
                <span>{pedidoPreview.modulo}</span>
              </div>
              <div className={estilos.resumoLinha}>
                <span className={estilos.chave}>Contexto</span>
                <span>Tela atual anexada automaticamente</span>
              </div>
            </div>
          </div>
        </section>

        <section className={estilos.painel}>
          <div className={estilos.painelCabecalho}>
            <div>
              <p className={estilos.eyebrow}>Entrada</p>
              <h2 className={estilos.tituloPainel}>Registrar solicitação</h2>
            </div>
          </div>
          <div className={estilos.corpo}>
            <div className={estilos.form}>
              {erro && (
                <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
                  {erro}
                </FeedbackInline>
              )}

              <div className={estilos.blocoVoz}>
                {relatoVoz.estado === 'gravando' ? (
                  <>
                    <div className={estilos.gravando} role="status">
                      <span className={estilos.pontoGravando} />
                      <span>Gravando</span>
                      <span className={estilos.tempoGravando}>{formatarTempoGravacao(relatoVoz.segundos)}</span>
                      <span className={estilos.barrasGravando} aria-hidden="true">
                        {relatoVoz.niveis.map((nivel, i) => (
                          <i key={i} className={estilos.barraGravando} style={{ height: `${Math.round(3 + nivel * 13)}px` }} />
                        ))}
                      </span>
                    </div>
                    <Button className={estilos.botaoParar} icon={<RecordStop24Filled />} onClick={relatoVoz.parar}>
                      Parar e preencher
                    </Button>
                    <span className={estilos.dica}>O áudio não é salvo. Só o texto volta para o formulário.</span>
                  </>
                ) : relatoVoz.estado === 'transcrevendo' ? (
                  <span className={estilos.dica} role="status">
                    <Spinner size="extra-tiny" className={estilos.spinnerInline} />
                    Áudio enviado. A IA está preenchendo o chamado…
                  </span>
                ) : (
                  <>
                    <p className={estilos.blocoVozTitulo}>Fale o seu problema</p>
                    <span className={estilos.dica}>A IA preenche o chamado para você. Até 10 minutos.</span>
                    <Button appearance="primary" icon={<Mic24Regular />} onClick={relatoVoz.iniciar} disabled={enviando}>
                      {camposIa.size > 0 ? 'Gravar de novo' : 'Relatar por voz'}
                    </Button>
                  </>
                )}
              </div>

              {avisoIa && !ocupadoComVoz ? (
                <span className={estilos.avisoIa} role="status">
                  {avisoIa}
                </span>
              ) : (
                <span className={estilos.ou}>ou preencha</span>
              )}

              <div className={estilos.linha}>
                <label className={estilos.campo}>
                  {rotuloCampo('Tipo', 'tipo')}
                  <select
                    className={`${estilos.select} ${camposIa.has('tipo') ? estilos.campoIa : ''}`}
                    value={tipo}
                    disabled={ocupadoComVoz}
                    onChange={(e) => {
                      setTipo(Number(e.target.value));
                      editadoPeloUsuario('tipo');
                    }}
                  >
                    {Object.entries(tipoSolicitacaoSuporteIaLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </select>
                </label>

                <label className={estilos.campo}>
                  {rotuloCampo('Severidade percebida', 'severidade')}
                  <select
                    className={`${estilos.select} ${camposIa.has('severidade') ? estilos.campoIa : ''}`}
                    value={severidade}
                    disabled={ocupadoComVoz}
                    onChange={(e) => {
                      setSeveridade(Number(e.target.value));
                      editadoPeloUsuario('severidade');
                    }}
                  >
                    {Object.entries(severidadeSolicitacaoSuporteIaLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </select>
                </label>
              </div>

              <label className={estilos.campo}>
                {rotuloCampo('Título', 'titulo')}
                <Input
                  className={camposIa.has('titulo') ? estilos.campoIa : undefined}
                  value={titulo}
                  disabled={ocupadoComVoz}
                  onChange={(_, data) => {
                    setTitulo(data.value);
                    editadoPeloUsuario('titulo');
                  }}
                  placeholder="Ex.: Não consigo encerrar inspeção"
                />
              </label>

              <label className={estilos.campo}>
                {rotuloCampo('Módulo ou tela', 'modulo')}
                <Input
                  className={camposIa.has('modulo') ? estilos.campoIa : undefined}
                  value={modulo}
                  disabled={ocupadoComVoz}
                  onChange={(_, data) => {
                    setModulo(data.value);
                    editadoPeloUsuario('modulo');
                  }}
                  placeholder="Ex.: Inspeções, EPI, DDS"
                />
              </label>

              <label className={estilos.campo}>
                {rotuloCampo('Descrição do cliente', 'descricao')}
                <Textarea
                  className={camposIa.has('descricao') ? estilos.campoIa : undefined}
                  value={descricao}
                  disabled={ocupadoComVoz}
                  onChange={(_, data) => {
                    setDescricao(data.value);
                    editadoPeloUsuario('descricao');
                  }}
                  resize="vertical"
                  rows={8}
                  placeholder="Descreva o que aconteceu, o que esperava ver e quais passos levaram até aqui."
                />
              </label>

              <Button
                appearance="primary"
                icon={<Send24Regular />}
                onClick={enviar}
                disabled={enviando || ocupadoComVoz || titulo.trim().length === 0 || descricao.trim().length === 0}
              >
                {enviando ? 'Triando...' : camposIa.size > 0 ? 'Abrir chamado' : 'Enviar para triagem'}
              </Button>
            </div>
          </div>
        </section>

        <section className={estilos.painel}>
          <div className={estilos.painelCabecalho}>
            <div>
              <p className={estilos.eyebrow}>IA + responsável</p>
              <h2 className={estilos.tituloPainel}>Triagem governada</h2>
            </div>
            {resultado && <StatusChip tom={resultado.requerAlteracaoCodigo ? 'alerta' : 'ok'}>triado</StatusChip>}
          </div>
          <div className={estilos.corpo}>
            {resultado ? (
              <div className={estilos.resultado}>
                <div className={estilos.meta}>
                  <StatusChip tom={resultado.requerAlteracaoCodigo ? 'alerta' : 'ok'}>
                    {resultadoTriagemSuporteIaLabel[resultado.resultadoTriagem]}
                  </StatusChip>
                  <StatusChip tom="neutro">{statusSolicitacaoSuporteIaLabel[resultado.status]}</StatusChip>
                </div>
                <div className={estilos.blocoTexto}>{resultado.respostaAoUsuario}</div>
                {resultado.requerAlteracaoCodigo && (
                  <>
                    <Legenda>Demanda reduzida</Legenda>
                    <div className={estilos.blocoTexto}>{resultado.demandaReduzida}</div>
                    <Legenda>Solução proposta</Legenda>
                    <div className={estilos.blocoTexto}>{resultado.solucaoProposta}</div>
                  </>
                )}
              </div>
            ) : (
              <div className={estilos.vazio}>
                A IA classifica o chamado, responde dúvidas simples e separa demandas que exigem aprovação,
                análise técnica ou alteração de código.
              </div>
            )}

            <div className={estilos.trilha}>
              <div className={estilos.passo}>
                <span className={estilos.ponto} />
                <span>Chamado registrado com tipo, prioridade, módulo e contexto.</span>
              </div>
              <div className={estilos.passo}>
                <span className={estilos.ponto} />
                <span>Triagem idempotente: orientação, demanda técnica ou melhoria.</span>
              </div>
              <div className={estilos.passo}>
                <span className={estilos.ponto} />
                <span>Quando exige mudança, segue para aprovação e análise responsável.</span>
              </div>
            </div>
          </div>
        </section>
      </div>

      <EsteiraSuporteIa etapaAtiva={etapaAtual} />

      <section className={estilos.historicoPainel}>
        <div className={estilos.historicoCabecalho}>
          <div>
            <p className={estilos.eyebrow}>Fila recente</p>
            <h2 className={estilos.historicoTitulo}>Pedidos registrados</h2>
          </div>
          <StatusChip tom="neutro">{historico.length} solicitações</StatusChip>
        </div>
        <div className={estilos.historico}>
          {historico.length === 0 && (
            <div className={estilos.corpo}>
              <Legenda>Nenhuma solicitação registrada ainda.</Legenda>
            </div>
          )}
          {historico.slice(0, 8).map((item) => (
            <button
              type="button"
              className={estilos.itemHistorico}
              key={item.id}
              onClick={() => navigate(`/suporte-ia/${item.id}`)}
            >
              <div className={estilos.itemTitulo}>
                <span className={estilos.forte}>{item.titulo}</span>
                <span className={estilos.data}>
                  {item.modulo || 'Módulo não informado'} · {formatarData(item.createdAtUtc)}
                </span>
              </div>
              <div className={estilos.meta}>
                <StatusChip tom={item.requerAlteracaoCodigo ? 'alerta' : 'ok'}>
                  {item.resultadoTriagem === ResultadoTriagemSuporteIa.DemandaTecnica ? 'Técnica' : 'Orientação'}
                </StatusChip>
                <StatusChip tom="neutro">{tipoSolicitacaoSuporteIaLabel[item.tipo]}</StatusChip>
              </div>
              <StatusChip tom="neutro">{statusSolicitacaoSuporteIaLabel[item.status]}</StatusChip>
            </button>
          ))}
        </div>
      </section>

      {filaResponsavel && (
        <section className={estilos.historicoPainel}>
          <div className={estilos.historicoCabecalho}>
            <div>
              <p className={estilos.eyebrow}>Fila do responsável</p>
              <h2 className={estilos.historicoTitulo}>Chamados aguardando aprovação, execução ou validação</h2>
            </div>
            <StatusChip tom="neutro">{filaResponsavel.length} pendentes</StatusChip>
          </div>
          <div className={estilos.historico}>
            {filaResponsavel.length === 0 && (
              <div className={estilos.corpo}>
                <Legenda>Nenhum chamado pendente de tratativa no momento.</Legenda>
              </div>
            )}
            {filaResponsavel.slice(0, 20).map((item) => (
              <button
                type="button"
                className={estilos.itemHistorico}
                key={item.id}
                onClick={() => navigate(`/suporte-ia/${item.id}`)}
              >
                <div className={estilos.itemTitulo}>
                  <span className={estilos.forte}>{item.titulo}</span>
                  <span className={estilos.data}>
                    {item.solicitanteNome || item.solicitanteEmail || 'Solicitante não identificado'} ·{' '}
                    {formatarData(item.createdAtUtc)}
                  </span>
                </div>
                <div className={estilos.meta}>
                  <StatusChip tom="neutro">{tipoSolicitacaoSuporteIaLabel[item.tipo]}</StatusChip>
                  <StatusChip tom={item.severidadeInformada >= SeveridadeSolicitacaoSuporteIa.Alta ? 'alerta' : 'atencao'}>
                    {severidadeSolicitacaoSuporteIaLabel[item.severidadeInformada]}
                  </StatusChip>
                </div>
                <StatusChip tom="neutro">{statusSolicitacaoSuporteIaLabel[item.status]}</StatusChip>
              </button>
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
