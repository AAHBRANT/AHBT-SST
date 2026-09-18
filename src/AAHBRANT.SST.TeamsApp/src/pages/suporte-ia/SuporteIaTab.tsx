import { useEffect, useMemo, useState } from 'react';
import { Input, Textarea, makeStyles, tokens } from '@fluentui/react-components';
import { Send24Regular } from '@fluentui/react-icons';
import {
  api,
  ResultadoTriagemSuporteIa,
  SeveridadeSolicitacaoSuporteIa,
  TipoSolicitacaoSuporteIa,
  resultadoTriagemSuporteIaLabel,
  severidadeSolicitacaoSuporteIaLabel,
  statusSolicitacaoSuporteIaLabel,
  tipoSolicitacaoSuporteIaLabel,
  type SuporteIaSolicitacao,
} from '../../lib/api';
import { Button, FeedbackInline, Legenda, StatusChip } from '@ui';

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
  governanca: {
    display: 'grid',
    gridTemplateColumns: 'repeat(4, minmax(180px, 1fr))',
    gap: '12px',
    '@media (max-width: 980px)': {
      gridTemplateColumns: 'repeat(2, minmax(180px, 1fr))',
    },
    '@media (max-width: 620px)': {
      gridTemplateColumns: '1fr',
    },
  },
  etapa: {
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: '8px',
    padding: '14px',
    background: tokens.colorNeutralBackground1,
    display: 'grid',
    gap: '8px',
    minHeight: '132px',
  },
  etapaAtiva: {
    border: '1px solid #1B6B55',
    boxShadow: 'inset 3px 0 0 #1B6B55',
  },
  etapaNumero: {
    width: '24px',
    height: '24px',
    borderRadius: '50%',
    display: 'grid',
    placeItems: 'center',
    background: '#E5F1EC',
    color: '#10483B',
    fontSize: '12px',
    fontWeight: 800,
  },
  etapaTitulo: {
    margin: 0,
    fontSize: '13px',
    fontWeight: 800,
    color: tokens.colorNeutralForeground1,
  },
  etapaTexto: {
    margin: 0,
    fontSize: '12px',
    lineHeight: '18px',
    color: tokens.colorNeutralForeground2,
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

function indiceEtapaAtual(resultado: SuporteIaSolicitacao | null) {
  if (!resultado) return 0;
  if (resultado.requerAlteracaoCodigo) return 2;
  return 1;
}

export function SuporteIaTab() {
  const estilos = useStyles();
  const [tipo, setTipo] = useState<number>(TipoSolicitacaoSuporteIa.Duvida);
  const [severidade, setSeveridade] = useState<number>(SeveridadeSolicitacaoSuporteIa.Media);
  const [titulo, setTitulo] = useState('');
  const [modulo, setModulo] = useState('');
  const [descricao, setDescricao] = useState('');
  const [enviando, setEnviando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [resultado, setResultado] = useState<SuporteIaSolicitacao | null>(null);
  const [historico, setHistorico] = useState<SuporteIaSolicitacao[]>([]);
  const etapaAtual = indiceEtapaAtual(resultado);

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

  useEffect(() => {
    carregarHistorico();
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
      await carregarHistorico();
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

              <div className={estilos.linha}>
                <label className={estilos.campo}>
                  <span className={estilos.label}>Tipo</span>
                  <select className={estilos.select} value={tipo} onChange={(e) => setTipo(Number(e.target.value))}>
                    {Object.entries(tipoSolicitacaoSuporteIaLabel).map(([valor, rotulo]) => (
                      <option key={valor} value={valor}>
                        {rotulo}
                      </option>
                    ))}
                  </select>
                </label>

                <label className={estilos.campo}>
                  <span className={estilos.label}>Severidade percebida</span>
                  <select
                    className={estilos.select}
                    value={severidade}
                    onChange={(e) => setSeveridade(Number(e.target.value))}
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
                <span className={estilos.label}>Título</span>
                <Input
                  value={titulo}
                  onChange={(_, data) => setTitulo(data.value)}
                  placeholder="Ex.: Não consigo encerrar inspeção"
                />
              </label>

              <label className={estilos.campo}>
                <span className={estilos.label}>Módulo ou tela</span>
                <Input
                  value={modulo}
                  onChange={(_, data) => setModulo(data.value)}
                  placeholder="Ex.: Inspeções, EPI, DDS"
                />
              </label>

              <label className={estilos.campo}>
                <span className={estilos.label}>Descrição do cliente</span>
                <Textarea
                  value={descricao}
                  onChange={(_, data) => setDescricao(data.value)}
                  resize="vertical"
                  rows={8}
                  placeholder="Descreva o que aconteceu, o que esperava ver e quais passos levaram até aqui."
                />
              </label>

              <Button
                appearance="primary"
                icon={<Send24Regular />}
                onClick={enviar}
                disabled={enviando || titulo.trim().length === 0 || descricao.trim().length === 0}
              >
                {enviando ? 'Triando...' : 'Enviar para triagem'}
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

      <section className={estilos.governanca} aria-label="Esteira governada do suporte com IA">
        <div className={`${estilos.etapa} ${etapaAtual === 0 ? estilos.etapaAtiva : ''}`}>
          <span className={estilos.etapaNumero}>1</span>
          <h3 className={estilos.etapaTitulo}>Abertura do chamado</h3>
          <p className={estilos.etapaTexto}>
            O pedido do usuário vira um registro estruturado, com módulo, severidade, descrição e contexto da tela.
          </p>
        </div>
        <div className={`${estilos.etapa} ${etapaAtual === 1 ? estilos.etapaAtiva : ''}`}>
          <span className={estilos.etapaNumero}>2</span>
          <h3 className={estilos.etapaTitulo}>Triagem assistida</h3>
          <p className={estilos.etapaTexto}>
            A IA responde dúvidas operacionais e mantém rastreável o diagnóstico usado para classificar o chamado.
          </p>
        </div>
        <div className={`${estilos.etapa} ${etapaAtual === 2 ? estilos.etapaAtiva : ''}`}>
          <span className={estilos.etapaNumero}>3</span>
          <h3 className={estilos.etapaTitulo}>Aprovação e execução</h3>
          <p className={estilos.etapaTexto}>
            Bugs, melhorias e novas funcionalidades entram como demanda técnica antes de qualquer alteração real.
          </p>
        </div>
        <div className={estilos.etapa}>
          <span className={estilos.etapaNumero}>4</span>
          <h3 className={estilos.etapaTitulo}>Validação do solicitante</h3>
          <p className={estilos.etapaTexto}>
            O fechamento deve voltar ao usuário para confirmar se a orientação ou correção resolveu o problema.
          </p>
        </div>
      </section>

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
            <div className={estilos.itemHistorico} key={item.id}>
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
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
