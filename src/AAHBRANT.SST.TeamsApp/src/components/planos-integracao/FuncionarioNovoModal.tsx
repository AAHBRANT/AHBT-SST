import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  makeStyles,
  mergeClasses,
  tokens,
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogContent,
  DialogActions,
  Button,
} from '@fluentui/react-components';
import {
  PersonAdd24Regular,
  Checkmark16Filled,
  ChevronRight20Regular,
  ArrowLeft20Regular,
  PersonAvailable20Regular,
} from '@fluentui/react-icons';
import { CampoData } from '../CampoData';
import {
  api,
  MotivoPlanoIntegracao,
  type PlanoIntegracao,
  type SituacaoItemKit,
  type SituacaoPassoIntegracao,
} from '../../lib/api';

// Pop-up "Funcionário novo" (especificação 21/10/2026, Módulo 1): aparece depois das Novidades para o
// Técnico de Segurança e o Administrador. Com vários funcionários pendentes, abre numa lista — o
// técnico entra em cada um, em vez de receber uma janela atrás da outra.

const useStyles = makeStyles({
  superficie: { maxWidth: '620px', width: '100%' },
  cabecalho: { display: 'flex', alignItems: 'center', gap: '10px' },
  icone: {
    width: '32px',
    height: '32px',
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorBrandBackground2,
    color: tokens.colorBrandForeground2,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    flexShrink: 0,
  },
  subtitulo: {
    margin: 0,
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground3,
    paddingLeft: '42px',
  },
  lista: { display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '12px' },
  cartaoLista: {
    display: 'grid',
    gridTemplateColumns: '40px 1fr auto',
    alignItems: 'center',
    gap: '12px',
    width: '100%',
    padding: '10px 12px',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
    cursor: 'pointer',
    textAlign: 'left',
    color: tokens.colorNeutralForeground1,
    ':hover': { backgroundColor: tokens.colorNeutralBackground1Hover },
    ':focus-visible': { outline: `2px solid ${tokens.colorBrandStroke1}`, outlineOffset: '2px' },
  },
  nomeLista: { fontWeight: tokens.fontWeightSemibold, display: 'block' },
  textoSecundario: { fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3 },
  perfil: {
    display: 'flex',
    alignItems: 'center',
    gap: '14px',
    padding: '14px 16px',
    borderRadius: tokens.borderRadiusLarge,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    marginTop: '4px',
  },
  avatar: {
    width: '52px',
    height: '52px',
    borderRadius: '50%',
    flexShrink: 0,
    objectFit: 'cover',
    backgroundColor: tokens.colorNeutralBackground1,
    color: tokens.colorBrandForeground1,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontWeight: tokens.fontWeightBold,
    fontSize: tokens.fontSizeBase400,
  },
  avatarPequeno: { width: '40px', height: '40px', fontSize: tokens.fontSizeBase300 },
  nomePerfil: { fontSize: tokens.fontSizeBase500, fontWeight: tokens.fontWeightBold, lineHeight: tokens.lineHeightBase500 },
  detalhePerfil: { fontSize: tokens.fontSizeBase200, opacity: 0.92 },
  responsavel: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    flexWrap: 'wrap',
    gap: '8px',
    marginTop: '10px',
    fontSize: tokens.fontSizeBase200,
    color: tokens.colorNeutralForeground2,
  },
  secao: {
    margin: '16px 0 8px',
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    color: tokens.colorNeutralForeground2,
    textTransform: 'uppercase',
    letterSpacing: '0.04em',
  },
  passos: { listStyleType: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: '6px' },
  passo: {
    display: 'grid',
    gridTemplateColumns: '28px 1fr auto',
    alignItems: 'center',
    gap: '10px',
    width: '100%',
    padding: '8px 10px',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
    color: tokens.colorNeutralForeground1,
    textAlign: 'left',
    cursor: 'pointer',
    ':hover': { backgroundColor: tokens.colorNeutralBackground1Hover },
    ':focus-visible': { outline: `2px solid ${tokens.colorBrandStroke1}`, outlineOffset: '2px' },
    '@media (max-width: 480px)': { gridTemplateColumns: '28px 1fr' },
  },
  numero: {
    width: '26px',
    height: '26px',
    borderRadius: '50%',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: tokens.fontSizeBase200,
    fontWeight: tokens.fontWeightSemibold,
    border: `1px solid ${tokens.colorNeutralStroke1}`,
  },
  numeroFeito: {
    backgroundColor: tokens.colorPaletteGreenBackground2,
    color: tokens.colorPaletteGreenForeground2,
    border: 'none',
  },
  numeroAtual: {
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
    border: 'none',
  },
  tituloPasso: { fontWeight: tokens.fontWeightSemibold, fontSize: tokens.fontSizeBase300, display: 'block' },
  chip: {
    display: 'inline-block',
    padding: '2px 8px',
    borderRadius: tokens.borderRadiusCircular,
    fontSize: tokens.fontSizeBase100,
    fontWeight: tokens.fontWeightSemibold,
    whiteSpace: 'nowrap',
    '@media (max-width: 480px)': { gridColumn: '2', justifySelf: 'start' },
  },
  chipOk: { backgroundColor: tokens.colorPaletteGreenBackground2, color: tokens.colorPaletteGreenForeground2 },
  chipAtencao: { backgroundColor: tokens.colorPaletteYellowBackground2, color: tokens.colorPaletteMarigoldForeground2 },
  chipCritico: { backgroundColor: tokens.colorPaletteRedBackground2, color: tokens.colorPaletteRedForeground2 },
  chipMarca: { backgroundColor: tokens.colorBrandBackground2, color: tokens.colorBrandForeground2 },
  chipNeutro: { backgroundColor: tokens.colorNeutralBackground3, color: tokens.colorNeutralForeground2 },
  kit: { display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(170px, 1fr))', gap: '8px' },
  itemKit: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
    padding: '8px 10px',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    fontSize: tokens.fontSizeBase200,
    alignItems: 'flex-start',
  },
  progresso: { fontSize: tokens.fontSizeBase200, color: tokens.colorNeutralForeground3, whiteSpace: 'nowrap' },
  acoes: { gridColumn: '1 / -1', justifySelf: 'end', flexWrap: 'wrap', '& button': { whiteSpace: 'nowrap' } },
  ajusteData: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: '8px',
    padding: '6px 10px 2px 48px',
    fontSize: tokens.fontSizeBase200,
  },
  campoData: { width: '140px' },
});

const ROTULO_PASSO: Record<SituacaoPassoIntegracao, string> = {
  feito: 'Feito',
  pendente: 'Pendente',
  agendado: 'Agendado',
  aguardando: 'Aguardando',
  naoConfigurado: 'Sem curso no catálogo',
};

const ROTULO_KIT: Record<SituacaoItemKit, string> = {
  separado: 'Separado',
  emFalta: 'Em falta',
  entregue: 'Entregue',
};

const ROTULO_MOTIVO: Record<MotivoPlanoIntegracao, string> = {
  [MotivoPlanoIntegracao.Admissao]: 'Admissão',
  [MotivoPlanoIntegracao.TrocaFuncao]: 'Troca de função',
  [MotivoPlanoIntegracao.TransferenciaObra]: 'Transferência de obra',
};

function iniciais(nome: string) {
  const partes = nome.trim().split(/\s+/).filter(Boolean);
  if (partes.length === 0) return '?';
  return ((partes[0]?.[0] ?? '') + (partes.length > 1 ? partes[partes.length - 1]?.[0] ?? '' : '')).toUpperCase();
}

function formatarData(iso: string) {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleDateString('pt-BR', { timeZone: 'UTC' });
}

function Avatar({ plano, pequeno }: { plano: PlanoIntegracao; pequeno?: boolean }) {
  const estilos = useStyles();
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!plano.temFoto) return;
    let cancelado = false;
    let objeto: string | null = null;
    api.trabalhadores
      .baixarFoto(plano.trabalhadorId)
      .then((blob) => {
        if (cancelado) return;
        objeto = URL.createObjectURL(blob);
        setUrl(objeto);
      })
      .catch(() => {
        // Sem foto: ficam as iniciais.
      });
    return () => {
      cancelado = true;
      if (objeto) URL.revokeObjectURL(objeto);
    };
  }, [plano.trabalhadorId, plano.temFoto]);

  const classe = mergeClasses(estilos.avatar, pequeno && estilos.avatarPequeno);
  return url ? <img src={url} alt="" className={classe} /> : <span className={classe} aria-hidden="true">{iniciais(plano.nome)}</span>;
}

interface Props {
  planos: PlanoIntegracao[];
  aoFechar: () => void;
}

export function FuncionarioNovoModal({ planos, aoFechar }: Props) {
  const estilos = useStyles();
  const navigate = useNavigate();
  const [selecionadoId, setSelecionadoId] = useState<string | null>(planos.length === 1 ? planos[0]!.id : null);
  const [planosAtuais, setPlanosAtuais] = useState(planos);
  const [assumindo, setAssumindo] = useState(false);
  const [erroAssumir, setErroAssumir] = useState<string | null>(null);
  const [editandoData, setEditandoData] = useState<string | null>(null);
  const [novaData, setNovaData] = useState('');
  const [salvandoData, setSalvandoData] = useState(false);
  const [erroData, setErroData] = useState<string | null>(null);
  const selecionado = planosAtuais.find((p) => p.id === selecionadoId) ?? null;

  function irPara(rota?: string | null) {
    if (!rota) return;
    aoFechar();
    navigate(rota);
  }

  async function assumir(plano: PlanoIntegracao) {
    setAssumindo(true);
    setErroAssumir(null);
    try {
      await api.planosIntegracao.assumir(plano.id);
      const atualizado = await api.planosIntegracao.obter(plano.id);
      setPlanosAtuais((lista) => lista.map((p) => (p.id === plano.id ? atualizado : p)));
    } catch (e) {
      setErroAssumir(e instanceof Error ? e.message : 'Não foi possível assumir agora.');
    } finally {
      setAssumindo(false);
    }
  }

  async function salvarData(plano: PlanoIntegracao, sessaoId: string) {
    if (!novaData) {
      setErroData('Informe a data no formato dd/mm/aaaa.');
      return;
    }
    setSalvandoData(true);
    setErroData(null);
    try {
      await api.planosIntegracao.alterarDataTurma(sessaoId, novaData);
      const atualizado = await api.planosIntegracao.obter(plano.id);
      setPlanosAtuais((lista) => lista.map((p) => (p.id === plano.id ? atualizado : p)));
      setEditandoData(null);
    } catch (e) {
      setErroData(e instanceof Error ? e.message : 'Não foi possível mudar a data agora.');
    } finally {
      setSalvandoData(false);
    }
  }

  const chipPasso = (s: SituacaoPassoIntegracao) =>
    s === 'feito' ? estilos.chipOk
      : s === 'pendente' ? estilos.chipNeutro
      : s === 'agendado' ? estilos.chipAtencao
      : estilos.chipNeutro;
  const chipKit = (s: SituacaoItemKit) =>
    s === 'emFalta' ? estilos.chipCritico : s === 'separado' ? estilos.chipOk : estilos.chipNeutro;
  const primeiroPendente = selecionado?.passos.findIndex((p) => p.situacao === 'pendente') ?? -1;

  return (
    <Dialog open onOpenChange={(_, data) => !data.open && aoFechar()}>
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle>
            <div className={estilos.cabecalho}>
              <span className={estilos.icone} aria-hidden="true">
                <PersonAdd24Regular />
              </span>
              <span>{planosAtuais.length > 1 ? `Funcionários novos (${planosAtuais.length})` : 'Funcionário novo'}</span>
            </div>
            <p className={estilos.subtitulo}>
              {selecionado ? 'O que falta para ficar apto, na ordem.' : 'Kit de EPI separado e passos para ficar apto.'}
            </p>
          </DialogTitle>
          <DialogContent>
            {!selecionado && (
              <div className={estilos.lista}>
                {planosAtuais.map((p) => {
                  const emFalta = p.kit.filter((i) => i.situacao === 'emFalta').length;
                  return (
                    <button key={p.id} type="button" className={estilos.cartaoLista} onClick={() => setSelecionadoId(p.id)}>
                      <Avatar plano={p} pequeno />
                      <span>
                        <span className={estilos.nomeLista}>{p.nome}</span>
                        <span className={estilos.textoSecundario}>
                          {p.funcao} · {p.obraNome}
                          {p.equipeNome ? ` · ${p.equipeNome}` : ''}
                          {p.responsavelNome ? ` · com ${p.responsavelNome}` : ''}
                        </span>
                      </span>
                      <span className={estilos.progresso}>
                        {emFalta > 0 && <span className={mergeClasses(estilos.chip, estilos.chipCritico)}>{emFalta} em falta</span>}{' '}
                        {p.passosConcluidos}/{p.passos.length} <ChevronRight20Regular style={{ verticalAlign: 'middle' }} />
                      </span>
                    </button>
                  );
                })}
              </div>
            )}

            {selecionado && (
              <>
                <div className={estilos.perfil}>
                  <Avatar plano={selecionado} />
                  <div>
                    <div className={estilos.nomePerfil}>
                      {selecionado.nome} · {selecionado.funcao}
                    </div>
                    <div className={estilos.detalhePerfil}>
                      {selecionado.obraNome}
                      {selecionado.equipeNome ? ` · ${selecionado.equipeNome}` : ''} · {ROTULO_MOTIVO[selecionado.motivo]} em{' '}
                      {formatarData(selecionado.motivo === MotivoPlanoIntegracao.Admissao ? selecionado.dataAdmissao : selecionado.abertoEm)}
                    </div>
                  </div>
                </div>

                <div className={estilos.responsavel}>
                  <span>
                    {selecionado.responsavelNome
                      ? `Responsável: ${selecionado.responsavelNome}`
                      : 'Ninguém assumiu ainda. Quem assumir fica como responsável para os outros técnicos.'}
                  </span>
                  <Button size="small" icon={<PersonAvailable20Regular />} disabled={assumindo} onClick={() => assumir(selecionado)}>
                    {assumindo ? 'Assumindo…' : 'Assumir'}
                  </Button>
                </div>
                {erroAssumir && <div className={estilos.textoSecundario} role="alert">{erroAssumir}</div>}

                <div className={estilos.secao}>Passo a passo</div>
                <ol className={estilos.passos}>
                  {selecionado.passos.map((passo, i) => (
                    <li key={passo.codigo}>
                      <button type="button" className={estilos.passo} onClick={() => irPara(passo.rota)}>
                        <span
                          className={mergeClasses(
                            estilos.numero,
                            passo.situacao === 'feito' && estilos.numeroFeito,
                            i === primeiroPendente && estilos.numeroAtual,
                          )}
                        >
                          {passo.situacao === 'feito' ? <Checkmark16Filled /> : i + 1}
                        </span>
                        <span>
                          <span className={estilos.tituloPasso}>{passo.titulo}</span>
                          {passo.detalhe && <span className={estilos.textoSecundario}>{passo.detalhe}</span>}
                        </span>
                        <span className={mergeClasses(estilos.chip, i === primeiroPendente ? estilos.chipMarca : chipPasso(passo.situacao))}>
                          {i === primeiroPendente ? 'Fazer agora' : ROTULO_PASSO[passo.situacao]}
                        </span>
                      </button>
                      {passo.situacao === 'agendado' && passo.sessaoTreinamentoId && (
                        <div className={estilos.ajusteData}>
                          {editandoData === passo.codigo ? (
                            <>
                              <CampoData
                                className={estilos.campoData}
                                size="small"
                                aria-label={`Nova data da turma de ${passo.titulo}`}
                                value={novaData}
                                onChange={(_, d) => setNovaData(d.value)}
                              />
                              <Button
                                size="small"
                                appearance="primary"
                                disabled={salvandoData}
                                onClick={() => salvarData(selecionado, passo.sessaoTreinamentoId!)}
                              >
                                {salvandoData ? 'Salvando…' : 'Salvar data'}
                              </Button>
                              <Button size="small" appearance="subtle" onClick={() => setEditandoData(null)}>
                                Cancelar
                              </Button>
                              {erroData && <span role="alert">{erroData}</span>}
                            </>
                          ) : (
                            <Button
                              size="small"
                              appearance="transparent"
                              onClick={() => {
                                setEditandoData(passo.codigo);
                                setNovaData((passo.dataTurma ?? '').slice(0, 10));
                                setErroData(null);
                              }}
                            >
                              Alterar data da turma
                            </Button>
                          )}
                        </div>
                      )}
                    </li>
                  ))}
                </ol>

                {selecionado.kit.length > 0 && (
                  <>
                    <div className={estilos.secao}>Kit de EPI</div>
                    <div className={estilos.kit}>
                      {selecionado.kit.map((item) => (
                        <div key={item.catalogoEpiId} className={estilos.itemKit}>
                          <span>
                            {item.nome}
                            {item.tamanho ? ` · tam. ${item.tamanho}` : ''}
                          </span>
                          <span className={mergeClasses(estilos.chip, chipKit(item.situacao))}>{ROTULO_KIT[item.situacao]}</span>
                        </div>
                      ))}
                    </div>
                  </>
                )}
              </>
            )}
          </DialogContent>
          <DialogActions className={estilos.acoes}>
            {selecionado && planosAtuais.length > 1 && (
              <Button appearance="subtle" icon={<ArrowLeft20Regular />} onClick={() => setSelecionadoId(null)}>
                Voltar à lista
              </Button>
            )}
            <Button appearance="secondary" onClick={aoFechar}>
              Lembrar amanhã
            </Button>
            {selecionado && (
              <Button appearance="primary" onClick={() => irPara(`/pessoas/${selecionado.trabalhadorId}`)}>
                Abrir perfil
              </Button>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
