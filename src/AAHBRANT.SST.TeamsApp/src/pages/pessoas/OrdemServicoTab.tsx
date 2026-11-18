import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button, Card, Carregando, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle,
  FeedbackInline, StatusChip, Text, designTokens, type Tom,
} from '@ui';
import { api, metodoAutenticacaoAssinaturaLabel, nivelRiscoLabel, type MinutaOrdemServico, type OrdemServicoEmitida, type PendenciaOs, type UsuarioLogado } from '../../lib/api';
import { AssinaturaOrdemServicoDialog } from '../../components/assinatura/AssinaturaOrdemServicoDialog';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { useUsuarioLogado } from '../../lib/UsuarioLogadoContext';
import { RevisarSugestoesIaDialog, VincularProcedimentosDialog } from './ResolverPendenciasOsDialogs';

const tomPorNivel: Record<number, Tom> = { 1: 'ok', 2: 'info', 3: 'atencao', 4: 'alerta', 5: 'alerta' };
const ehPendenciaAtividadesFuncao = (p: PendenciaOs) =>
  p.tipo === 'AtividadesFuncaoNaoConfirmadas' || p.tipo === 'AtividadesFuncaoSemDecisao' || p.tipo === 'AtividadesFuncaoNenhumaConfirmada';
// Pendências que se resolvem no Inventário do PGR, abrindo direto na atividade (17/11/2026).
const ehPendenciaDoInventario = (p: PendenciaOs) => p.tipo === 'RiscoSemAvaliacao' || p.tipo === 'MedidasNaoSeparadas';
// Pendências resolvidas em lote, sem sair da OS (17/11/2026).
type Lote = { tipo: 'procedimentos' | 'sugestoes'; atividadeIds: string[]; pgrId?: string | null };
const lotePorTipo: Record<string, Lote['tipo']> = { SemProcedimento: 'procedimentos', SugestoesPendentes: 'sugestoes' };
const data = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString('pt-BR') : '—');
const dataHora = (iso: string) => new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
const situacaoOs: Record<OrdemServicoEmitida['situacao'], { tom: Tom; texto: string }> = {
  AguardandoFuncionario: { tom: 'atencao', texto: 'Aguardando funcionário' },
  AguardandoTecnico: { tom: 'atencao', texto: 'Aguardando técnico' },
  Valida: { tom: 'ok', texto: 'Válida' },
};

// Aba "Ordem de Serviço" do perfil do trabalhador (20/10/2026): minuta da OS na obra dele, montada
// só com conteúdo validado, e as pendências que bloqueiam a emissão. Uma OS por trabalhador por obra.
// Emitir (03/11) gera número e cópia congelada da OS; assinam o funcionário (digital/facial) e depois o
// técnico de segurança. O histórico lista as OS emitidas com a situação de cada uma e o PDF.
export function OrdemServicoTab({ trabalhadorId, aoIrParaAba }: {
  trabalhadorId: string;
  aoIrParaAba: (aba: 'geral' | 'treinamentos') => void;
}) {
  const [minuta, setMinuta] = useState<MinutaOrdemServico | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [popAberto, setPopAberto] = useState(false);
  const [previaAberta, setPreviaAberta] = useState(false);
  const [emitidas, setEmitidas] = useState<OrdemServicoEmitida[]>([]);
  const [emitindo, setEmitindo] = useState(false);
  const [erroEmissao, setErroEmissao] = useState<string | null>(null);
  const [assinando, setAssinando] = useState<OrdemServicoEmitida | null>(null);
  const [lote, setLote] = useState<Lote | null>(null);
  const [logoObraUrl, setLogoObraUrl] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const { usuario } = useUsuarioLogado();
  const navigate = useNavigate();

  // abrirPop: com pendência, o pop-up explicativo abre sozinho (ao entrar na aba e ao voltar de um lote).
  async function carregarMinuta(abrirPop: boolean) {
    try {
      const m = await api.ordensServico.obterMinuta(trabalhadorId);
      setMinuta(m);
      if (abrirPop) setPopAberto(m.pendencias.length > 0);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao montar a Ordem de Serviço.');
    }
  }

  async function carregarEmitidas() {
    try {
      setEmitidas(await api.ordensServico.listar(trabalhadorId));
    } catch {
      // O histórico é complementar: a minuta continua na tela mesmo se ele falhar.
    }
  }

  async function emitir() {
    try {
      setEmitindo(true);
      setErroEmissao(null);
      const os = await api.ordensServico.emitir(trabalhadorId);
      await carregarEmitidas();
      setAssinando(os);
    } catch (e) {
      setErroEmissao(e instanceof Error ? e.message : 'Falha ao emitir a Ordem de Serviço.');
    } finally {
      setEmitindo(false);
    }
  }

  function verPdf(os: OrdemServicoEmitida) {
    void visualizar({ titulo: `Ordem de Serviço ${os.numero}`, nomeArquivo: `${os.numero}.pdf`, obter: () => api.ordensServico.baixarPdf(trabalhadorId, os.id) });
  }

  useEffect(() => {
    void carregarMinuta(true);
    void carregarEmitidas();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  useEffect(() => {
    const obraId = minuta?.obra?.id;
    if (!obraId) {
      setLogoObraUrl(null);
      return;
    }
    let ativo = true;
    let urlCriada: string | null = null;
    void api.obras.baixarLogo(obraId)
      .then((blob) => {
        if (!ativo || blob.size === 0) return;
        urlCriada = URL.createObjectURL(blob);
        setLogoObraUrl(urlCriada);
      })
      .catch(() => {
        if (ativo) setLogoObraUrl(null);
      });
    return () => {
      ativo = false;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [minuta?.obra?.id]);

  if (!minuta) return erro ? <FeedbackInline tom="erro">{erro}</FeedbackInline> : <Carregando variante="detalhe" linhas={6} />;

  // Abre a atividade no Inventário do PGR ("Riscos e medidas"), já com os "Detalhes para a OS" abertos.
  function abrirAtividadeNoPgr(pgrId: string, atividadeId: string) {
    setPopAberto(false);
    setLote(null);
    navigate(`/prevencao/pgr/${pgrId}?aba=inventario&atividade=${atividadeId}`);
  }

  function irPara(p: PendenciaOs) {
    setPopAberto(false);
    const tipoLote = lotePorTipo[p.tipo];
    if (tipoLote && p.atividadeIds?.length) {
      setLote({ tipo: tipoLote, atividadeIds: p.atividadeIds, pgrId: p.destinoId });
      return;
    }
    if (p.destino === 'pgr' && p.destinoId && ehPendenciaDoInventario(p) && p.atividadeIds?.length)
      abrirAtividadeNoPgr(p.destinoId, p.atividadeIds[0]);
    else if (p.destino === 'pgr' && p.destinoId) navigate(`/prevencao/pgr/${p.destinoId}${ehPendenciaAtividadesFuncao(p) ? '?aba=atividades-funcao' : ''}`);
    else if (p.destino === 'pgr') navigate('/gestao-sst?secao=pgr');
    else if (p.destino === 'treinamentos') aoIrParaAba('treinamentos');
    else if (p.destino === 'aso') aoIrParaAba('geral');
  }

  function fecharLote(alterou: boolean) {
    setLote(null);
    // Volta para a lista de pendências já atualizada: a SST vê o que sumiu e o que ainda falta.
    if (alterou) void carregarMinuta(true);
    else setPopAberto(true);
  }

  const rotuloDestino = (p: PendenciaOs) =>
    ehPendenciaAtividadesFuncao(p) ? 'Confirmar atividades da função'
    : p.tipo === 'SemProcedimento' && p.atividadeIds?.length ? 'Vincular procedimentos'
    : p.tipo === 'SugestoesPendentes' && p.atividadeIds?.length ? 'Revisar sugestões'
    : ehPendenciaDoInventario(p) && p.atividadeIds?.length ? 'Abrir no Inventário'
    : p.destino === 'pgr' ? 'Abrir no PGR' : p.destino === 'treinamentos' ? 'Ver treinamentos' : 'Ver ASO';

  const riscos = minuta.atividades.flatMap((a) => a.riscos);
  const medidas = riscos.reduce((t, r) => t + r.antes.length + r.durante.length + r.apos.length, 0);
  const itens = minuta.atividades.reduce((t, a) => t + a.interromperQuando.length + a.restricoes.length + a.autorizacoes.length, 0);
  const procedimentos = new Set(minuta.atividades.flatMap((a) => a.procedimentos)).size;
  // false = prévia: a função ainda não foi confirmada pelo engenheiro (ou a decisão está parcial).
  const previaPorConfirmacao = minuta.atividadesDaFuncaoConfirmadas === false;
  const pendSst = minuta.pendencias.filter((p) => p.quemResolve === 'SST');
  const pendRh = minuta.pendencias.filter((p) => p.quemResolve === 'RH');
  // Só na transição (nenhuma decisão do engenheiro) as pendências por atividade ficam escondidas.
  const pendenciasPorAtividadeOcultas = minuta.pendencias.some((p) => p.tipo === 'AtividadesFuncaoNaoConfirmadas');
  const base = [
    minuta.pgr ? `PGR ${minuta.pgr.nome}` : 'sem PGR vigente',
    minuta.ghes.length ? `GHE ${minuta.ghes.map((g) => String(g.numero).padStart(2, '0')).join(', ')}` : null,
    `função ${minuta.trabalhador.funcao}`,
    `obra ${minuta.obra.nome}`,
  ].filter(Boolean).join(' · ');

  const linhaResumo = (rotulo: string, conteudo: React.ReactNode) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, padding: '6px 0', borderBottom: '1px solid var(--colorNeutralStroke2)', flexWrap: 'wrap' }}>
      <Text>{rotulo}</Text>
      <span style={{ display: 'inline-flex', gap: 6, flexWrap: 'wrap' }}>{conteudo}</span>
    </div>
  );

  const ListaPendencias = ({ titulo, lista }: { titulo: string; lista: PendenciaOs[] }) =>
    lista.length === 0 ? null : (
      <div style={{ marginTop: 12 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 6 }}>{titulo}</Text>
        {lista.map((p, i) => (
          <div
            key={i}
            style={{
              border: '1px solid var(--colorNeutralStroke2)',
              borderLeft: `4px solid ${p.tipo === 'SugestoesPendentes' ? 'var(--colorPaletteMarigoldBorderActive)' : 'var(--colorPaletteRedBorderActive)'}`,
              borderRadius: 6, padding: '8px 10px', marginBottom: 8,
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start', flexWrap: 'wrap' }}>
              <Text weight="semibold">{p.titulo}</Text>
              {p.destino && <Button size="small" onClick={() => irPara(p)}>{rotuloDestino(p)}</Button>}
            </div>
            <Text size={200} style={{ display: 'block', marginTop: 2 }}>{p.porQueBloqueia}</Text>
          </div>
        ))}
      </div>
    );

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <Card>
        <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
            {logoObraUrl && (
              <div style={{
                width: 68, height: 52, borderRadius: 6, border: '1px solid var(--colorNeutralStroke2)',
                background: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 4, flexShrink: 0,
              }}>
                <img src={logoObraUrl} alt={`Logo ${minuta.obra.nome}`} style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} />
              </div>
            )}
            <div>
              <Text weight="semibold" size={500} style={{ display: 'block', marginBottom: 6 }}>Ordem de Serviço (NR-01)</Text>
              {minuta.podeEmitir
                ? <StatusChip tom="ok">PRONTA PARA EMISSÃO</StatusChip>
                : <StatusChip tom="alerta">MINUTA — EMISSÃO BLOQUEADA</StatusChip>}
              <Text weight="semibold" size={400} style={{ display: 'block', marginTop: 4 }}>
                {minuta.podeEmitir
                  ? 'Todos os dados estão validados'
                  : `${minuta.pendencias.length} pendência(s) impedem a emissão da OS`}
              </Text>
              <Text size={200}>Base: {base}</Text>
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {!minuta.podeEmitir && <Button onClick={() => setPopAberto(true)}>Ver pendências</Button>}
            <Button onClick={() => setPreviaAberta(true)}>Ver prévia</Button>
            <Button
              appearance="primary"
              disabled={emitindo}
              onClick={() => (minuta.podeEmitir ? void emitir() : setPopAberto(true))}
            >
              {emitindo ? 'Emitindo…' : 'Emitir OS'}
            </Button>
          </div>
        </div>
        {previaPorConfirmacao && (
          <div style={{ marginTop: 12 }}>
            <FeedbackInline
              tom="aviso"
              acao={(() => {
                const p = minuta.pendencias.find((x) => ehPendenciaAtividadesFuncao(x) && x.destino === 'pgr');
                return p ? { rotulo: 'Confirmar atividades da função', aoClicar: () => irPara(p) } : undefined;
              })()}
            >
              <b>Prévia.</b> A confirmação {pendenciasPorAtividadeOcultas ? 'das' : 'de parte das'} atividades desta função ainda está
              pendente. A lista abaixo ainda pode mudar
              {pendenciasPorAtividadeOcultas ? ' e as pendências de procedimento aparecem depois da confirmação' : ''}.
            </FeedbackInline>
          </div>
        )}
        {erroEmissao && (
          <FeedbackInline tom="erro" aoFechar={() => setErroEmissao(null)}>{erroEmissao}</FeedbackInline>
        )}
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, alignItems: 'start' }}>
        <Card titulo="O que vai na OS deste trabalhador">
          {linhaResumo('Atividades da função (PGR)', previaPorConfirmacao
            ? <StatusChip tom="atencao">Aguardando confirmação</StatusChip>
            : <StatusChip tom={minuta.atividades.length ? 'ok' : 'alerta'}>{minuta.atividades.length} atividade(s)</StatusChip>)}
          {linhaResumo('Riscos', <StatusChip tom="ok">{riscos.length}</StatusChip>)}
          {linhaResumo('Medidas de prevenção validadas', <StatusChip tom="ok">{medidas}</StatusChip>)}
          {linhaResumo('Parada, restrições e autorizações', <StatusChip tom="ok">{itens}</StatusChip>)}
          {linhaResumo('EPIs da função', <StatusChip tom="ok">{minuta.epis.length}</StatusChip>)}
          {linhaResumo('Procedimentos vigentes', <StatusChip tom={procedimentos ? 'ok' : 'atencao'}>{procedimentos}</StatusChip>)}
          {linhaResumo('Treinamentos exigidos', minuta.treinamentos.length === 0
            ? <StatusChip tom="neutro">Nenhum na Matriz</StatusChip>
            : minuta.treinamentos.map((t) => (
              <StatusChip key={t.curso} tom={t.situacao === 'Valido' ? 'ok' : 'alerta'}>
                {t.norma ?? t.curso} {t.situacao === 'Valido' ? 'válido' : t.situacao === 'Vencido' ? 'vencido' : 'não feito'}
              </StatusChip>
            )))}
          {linhaResumo('ASO', minuta.aso
            ? <StatusChip tom={minuta.aso.valido ? 'ok' : 'alerta'}>{minuta.aso.valido ? `Apto até ${data(minuta.aso.validade)}` : 'Vencido ou sem aptidão'}</StatusChip>
            : <StatusChip tom="alerta">Sem ASO</StatusChip>)}
          {linhaResumo('Políticas', minuta.politicas.length === 0
            ? <StatusChip tom="neutro">Nenhuma</StatusChip>
            : minuta.politicas.map((p) => <StatusChip key={p.titulo} tom="ok">{p.titulo} rev. {p.revisao}</StatusChip>))}

          <ListaPendencias titulo="Pendências — responsável de SST" lista={pendSst} />
          <ListaPendencias titulo="Pendências — trabalhador / RH" lista={pendRh} />
        </Card>

        <Card titulo="Histórico de OS emitidas">
          {emitidas.length === 0 && <Text size={200}>Nenhuma OS emitida ainda.</Text>}
          {emitidas.map((os) => (
            <div key={os.id} style={{ padding: '8px 0', borderBottom: '1px solid var(--colorNeutralStroke2)', display: 'flex', flexDirection: 'column', gap: 4 }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap', alignItems: 'center' }}>
                <Text weight="semibold">{os.numero} · rev. {os.revisao} · {data(os.emitidaEm)}</Text>
                <span style={{ display: 'inline-flex', gap: 6, flexWrap: 'wrap' }}>
                  <StatusChip tom={situacaoOs[os.situacao].tom}>{situacaoOs[os.situacao].texto}</StatusChip>
                  {!os.vigente && <StatusChip tom="neutro">Substituída</StatusChip>}
                </span>
              </div>
              <Text size={200}>
                Funcionário: {os.funcionario ? `${os.funcionario.nome} — ${dataHora(os.funcionario.assinadoEm)} (${metodoAutenticacaoAssinaturaLabel[os.funcionario.metodo]})` : 'aguardando'}
              </Text>
              <Text size={200}>
                Técnico de segurança: {os.tecnico ? `${os.tecnico.nome} — ${dataHora(os.tecnico.assinadoEm)}` : 'aguardando'}
              </Text>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                {os.situacao !== 'Valida' && <Button size="small" appearance="primary" onClick={() => setAssinando(os)}>Assinar</Button>}
                <Button size="small" onClick={() => verPdf(os)}>Ver PDF</Button>
              </div>
            </div>
          ))}
        </Card>
      </div>

      {assinando && (
        <AssinaturaOrdemServicoDialog
          open
          ordemServico={assinando}
          trabalhadorId={trabalhadorId}
          trabalhadorNome={minuta.trabalhador.nome}
          onClose={() => { setAssinando(null); void carregarEmitidas(); }}
        />
      )}
      {dialogoVisualizador}

      {lote?.tipo === 'procedimentos' && <VincularProcedimentosDialog atividadeIds={lote.atividadeIds} aoFechar={fecharLote} />}
      {lote?.tipo === 'sugestoes' && (
        <RevisarSugestoesIaDialog
          atividadeIds={lote.atividadeIds}
          aoFechar={fecharLote}
          aoEditarNoPgr={lote.pgrId ? (atividadeId) => abrirAtividadeNoPgr(lote.pgrId!, atividadeId) : undefined}
        />
      )}

      <Dialog open={popAberto} onOpenChange={(_, d) => setPopAberto(d.open)}>
        <DialogSurface style={{ maxWidth: 640 }}>
          <DialogBody>
            <DialogTitle>⚠ Esta OS ainda não pode ser emitida</DialogTitle>
            <DialogContent>
              <Text style={{ display: 'block', textAlign: 'justify' }}>
                A Ordem de Serviço só pode ser emitida quando todos os riscos tiverem avaliação, todas as medidas
                tiverem sido validadas pelo responsável de SST e o trabalhador estiver com treinamentos e ASO em dia.
                Ela fica como <b>minuta</b> até {minuta.pendencias.length === 1 ? 'a pendência abaixo ser resolvida' : <>as <b>{minuta.pendencias.length} pendências</b> abaixo serem resolvidas</>}.
                {pendenciasPorAtividadeOcultas && ' As pendências de procedimento aparecem depois da confirmação das atividades.'}
              </Text>
              <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginTop: 10 }}>
                {pendSst.length > 0 && <StatusChip tom="alerta">Cadastro do PGR: {pendSst.length}</StatusChip>}
                {pendRh.length > 0 && <StatusChip tom="alerta">Do trabalhador: {pendRh.length}</StatusChip>}
              </div>
              {[
                {
                  titulo: 'Cadastro do PGR — responsável de SST',
                  dica: 'Resolvido uma vez, vale para todos os trabalhadores desta função na obra.',
                  lista: pendSst,
                },
                { titulo: 'Do trabalhador — RH', dica: 'Vale só para este trabalhador.', lista: pendRh },
              ].filter((g) => g.lista.length > 0).map((g) => (
                <div key={g.titulo} style={{ marginTop: 14 }}>
                  <Text weight="semibold" style={{ display: 'block' }}>{g.titulo}</Text>
                  <Text size={200} style={{ display: 'block', color: 'var(--colorNeutralForeground3)' }}>{g.dica}</Text>
                  {g.lista.map((p, i) => (
                    <div
                      key={i}
                      style={{
                        border: '1px solid var(--colorNeutralStroke2)',
                        borderLeft: `4px solid ${p.tipo === 'SugestoesPendentes' ? 'var(--colorPaletteMarigoldBorderActive)' : 'var(--colorPaletteRedBorderActive)'}`,
                        borderRadius: 4, padding: '8px 10px', marginTop: 8,
                      }}
                    >
                      <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'flex-start', flexWrap: 'wrap' }}>
                        <Text weight="semibold" style={{ flex: '1 1 260px' }}>{p.titulo}</Text>
                        {p.destino && (
                          <Button size="small" appearance={lotePorTipo[p.tipo] ? 'primary' : 'secondary'} onClick={() => irPara(p)}>
                            {rotuloDestino(p)}
                          </Button>
                        )}
                      </div>
                      <Text size={200} style={{ display: 'block', marginTop: 2 }}>{p.porQueBloqueia}</Text>
                    </div>
                  ))}
                </div>
              ))}
            </DialogContent>
            <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center', padding: '8px 0', borderTop: '1px solid var(--colorNeutralStroke2)', marginTop: 10 }}>
              <Text>Atividades da função (PGR)</Text>
              {previaPorConfirmacao
                ? <StatusChip tom="atencao">Aguardando confirmação</StatusChip>
                : <StatusChip tom={minuta.atividades.length ? 'ok' : 'alerta'}>{minuta.atividades.length} atividade(s)</StatusChip>}
            </div>
            <DialogActions>
              <Button onClick={() => { setPopAberto(false); setPreviaAberta(true); }}>Ver minuta</Button>
              <Button appearance="primary" onClick={() => setPopAberto(false)}>Entendi</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={previaAberta} onOpenChange={(_, d) => setPreviaAberta(d.open)}>
        <DialogSurface style={{ maxWidth: 940, width: '95vw' }}>
          <DialogBody>
            <DialogTitle>{minuta.podeEmitir ? 'Prévia da Ordem de Serviço' : 'Minuta da Ordem de Serviço (não emitida)'}</DialogTitle>
            <DialogContent>
              <PreviaOs minuta={minuta} logoObraUrl={logoObraUrl} ultimaOs={emitidas[0]} usuario={usuario} />
            </DialogContent>
            <DialogActions>
              <Button appearance="primary" onClick={() => setPreviaAberta(false)}>Fechar</Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  );
}

// Mesmo conteúdo e ordem do modelo aprovado (v6), com cabeçalho institucional (logo da obra), grade de campos e selo de assinatura padrão.
function PreviaOs({ minuta, logoObraUrl, ultimaOs, usuario }: {
  minuta: MinutaOrdemServico;
  logoObraUrl?: string | null;
  ultimaOs?: OrdemServicoEmitida;
  usuario?: UsuarioLogado | null;
}) {
  const corMarca = designTokens.colorPrimary;
  const secao = (n: number, titulo: string) => (
    <div style={{ background: corMarca, color: designTokens.colorWhite, padding: '4px 10px', fontWeight: 700, fontSize: 12, marginTop: 14, letterSpacing: 0.3 }}>
      {n}. {titulo.toUpperCase()}
    </div>
  );
  const linhaGrade = (rotulo: string, valor?: string | null) => (
    <div style={{ display: 'grid', gridTemplateColumns: '160px 1fr', border: '1px solid var(--colorNeutralStroke2)', marginTop: -1 }}>
      <div style={{ background: 'var(--colorNeutralBackground3)', padding: '5px 8px', fontWeight: 600, fontSize: 12, borderRight: '1px solid var(--colorNeutralStroke2)' }}>
        {rotulo}
      </div>
      <div style={{ padding: '5px 8px', fontSize: 12 }}>
        {valor && valor.trim() ? valor : '—'}
      </div>
    </div>
  );
  const lista = (itens: string[]) => (
    itens.length ? (
      <ul style={{ margin: '4px 0', paddingLeft: 18 }}>
        {itens.map((i, idx) => <li key={`${idx}-${i}`} style={{ marginBottom: 2 }}>{i}</li>)}
      </ul>
    ) : <Text size={200}>—</Text>
  );

  return (
    <div style={{
      fontSize: 13, lineHeight: 1.45, border: '1px solid var(--colorNeutralStroke2)',
      borderRadius: 6, padding: '16px 20px', background: 'var(--colorNeutralBackground1)',
    }}>
      {/* Cabeçalho institucional com a logo da obra e bloco central 100% centralizado */}
      <div style={{
        display: 'grid', gridTemplateColumns: '160px 1fr 160px', gap: 12, alignItems: 'center',
        paddingBottom: 10, borderBottom: `2px solid ${corMarca}`, marginBottom: 6,
      }}>
        <div style={{ display: 'flex', justifyContent: 'flex-start' }}>
          <div style={{
            width: 100, height: 60, border: '1px solid var(--colorNeutralStroke2)', borderRadius: 4,
            background: '#fff', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 4,
          }}>
            {logoObraUrl ? (
              <img src={logoObraUrl} alt={`Logo ${minuta.obra.nome}`} style={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain' }} />
            ) : (
              <Text size={100} style={{ color: 'var(--colorNeutralForeground3)', textAlign: 'center' }}>{minuta.obra.nome}</Text>
            )}
          </div>
        </div>
        <div style={{ textAlign: 'center', display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center' }}>
          <Text weight="bold" size={400} style={{ display: 'block', textAlign: 'center', color: corMarca, textTransform: 'uppercase' }}>
            {minuta.obra.nome}
          </Text>
          <Text weight="semibold" size={300} style={{ display: 'block', textAlign: 'center', marginTop: 2 }}>
            Ordem de Serviço de Segurança e Saúde no Trabalho
          </Text>
          <Text size={100} style={{ display: 'block', textAlign: 'center', marginTop: 2, color: 'var(--colorNeutralForeground3)' }}>
            NR-01, item 1.4.1, alínea &quot;c&quot; • CLT, art. 157, inciso II
          </Text>
        </div>
        <div style={{ textAlign: 'right', fontSize: 11, lineHeight: 1.35 }}>
          <div><b>Situação:</b> {minuta.podeEmitir ? 'VÁLIDA' : 'MINUTA'}</div>
          <div><b>Data:</b> {new Date().toLocaleDateString('pt-BR')}</div>
          {minuta.pgr && <div><b>Revisão PGR:</b> {minuta.pgr.revisao ?? 0}</div>}
        </div>
      </div>

      {secao(1, 'Identificação da empresa e da obra')}
      <div style={{ marginTop: 4 }}>
        {linhaGrade('Obra / Contrato', minuta.obra.nome)}
        {linhaGrade('Cliente', minuta.obra.cliente)}
        {linhaGrade('CNPJ', minuta.obra.cnpj)}
        {linhaGrade('Endereço da obra', minuta.obra.endereco)}
      </div>

      {secao(2, 'Identificação do trabalhador')}
      <div style={{ marginTop: 4 }}>
        {linhaGrade('Nome', minuta.trabalhador.nome)}
        {linhaGrade('Matrícula / CPF', `${minuta.trabalhador.matricula || '—'} • CPF ${minuta.trabalhador.cpf || minuta.trabalhador.cpfMascarado}`)}
        {linhaGrade('Função / CBO', `${minuta.trabalhador.funcao}${minuta.trabalhador.cbo ? ` • CBO ${minuta.trabalhador.cbo}` : ''}`)}
        {linhaGrade('Admissão', data(minuta.trabalhador.dataAdmissao))}
      </div>

      {secao(3, 'Atividades da função (conforme PGR)')}
      <div style={{ border: '1px solid var(--colorNeutralStroke2)', padding: '8px 10px', marginTop: 4 }}>
        {minuta.pgr && (
          <Text size={200} style={{ display: 'block', fontStyle: 'italic', marginBottom: 4 }}>
            Fonte: {minuta.pgr.nome}{minuta.pgr.revisao != null ? ` — revisão ${minuta.pgr.revisao}` : ''}
          </Text>
        )}
        {minuta.ghes.map((g) => (
          <Text key={g.numero} style={{ display: 'block', whiteSpace: 'pre-line', textAlign: 'justify', marginBottom: 4 }}>
            <b>GHE {String(g.numero).padStart(2, '0')}:</b> {g.descricaoAtividades || '— descrição não informada no PGR —'}
          </Text>
        ))}
        <Text size={100} style={{ display: 'block', marginTop: 4, color: 'var(--colorNeutralForeground3)' }}>
          Esta OS não amplia as atividades acima. Atividades fora desta lista não estão autorizadas por este documento.
        </Text>
      </div>

      {secao(4, 'Atividades da função: riscos, prevenção e requisitos')}
      {minuta.atividades.map((a) => (
        <div key={a.id} style={{ border: '1px solid var(--colorNeutralStroke2)', borderRadius: 4, overflow: 'hidden', marginTop: 8 }}>
          <div style={{ background: '#ebe9ad', color: '#1f1f1f', padding: '5px 10px', fontWeight: 700, fontSize: 12.5 }}>
            {a.nome}
          </div>
          <div style={{ padding: '8px 10px' }}>
            <Text size={200} style={{ display: 'block', whiteSpace: 'pre-line', marginBottom: 6 }}>
              <b>Condições de execução:</b> {a.condicoes || '—'}
            </Text>
            {a.riscos.map((r) => (
              <div
                key={r.id}
                style={{
                  border: '1px solid var(--colorNeutralStroke2)', borderRadius: 4,
                  padding: '6px 8px', marginTop: 6, background: 'var(--colorNeutralBackground2)',
                }}
              >
                <div style={{ marginBottom: 4 }}>
                  <StatusChip tom={tomPorNivel[r.nivel] ?? 'neutro'}>{nivelRiscoLabel[r.nivel]}</StatusChip>{' '}
                  <b>{r.perigo}</b>{r.consequencia ? ` — ${r.consequencia}` : ''}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))', gap: 8 }}>
                  <div><Text size={200} weight="semibold" style={{ color: corMarca }}>Antes do trabalho</Text>{lista(r.antes)}</div>
                  <div><Text size={200} weight="semibold" style={{ color: corMarca }}>Durante o trabalho</Text>{lista(r.durante)}</div>
                  <div><Text size={200} weight="semibold" style={{ color: corMarca }}>Após o trabalho</Text>{lista(r.apos)}</div>
                </div>
              </div>
            ))}
            {a.ferramentas.length > 0 && (
              <Text size={200} style={{ display: 'block', marginTop: 6 }}><b>Ferramentas e equipamentos:</b> {a.ferramentas.join(', ')}</Text>
            )}
            {a.epcs.length > 0 && (
              <Text size={200} style={{ display: 'block', marginTop: 4 }}><b>EPCs:</b> {a.epcs.join(', ')}</Text>
            )}
            <Text size={200} style={{ display: 'block', marginTop: 4 }}><b>Procedimentos aplicáveis:</b> {a.procedimentos.join('; ') || '—'}</Text>
            {a.interromperQuando.length > 0 && (
              <>
                <Text size={200} weight="semibold" style={{ display: 'block', marginTop: 4 }}>Interromper a atividade e comunicar a liderança quando</Text>
                {lista(a.interromperQuando)}
              </>
            )}
            {a.restricoes.length > 0 && (
              <>
                <Text size={200} weight="semibold" style={{ display: 'block', marginTop: 4 }}>Não executar sem capacitação, autorização ou liberação</Text>
                {lista(a.restricoes.map((r) => (r.requisito ? `${r.texto} → ${r.requisito}` : r.texto)))}
              </>
            )}
            {a.autorizacoes.length > 0 && (
              <>
                <Text size={200} weight="semibold" style={{ display: 'block', marginTop: 4 }}>Autorizações exigidas</Text>
                {lista(a.autorizacoes)}
              </>
            )}
          </div>
        </div>
      ))}

      {secao(5, 'EPIs obrigatórios')}
      <div style={{ border: '1px solid var(--colorNeutralStroke2)', padding: '6px 10px', marginTop: 4 }}>
        {lista(minuta.epis.map((e) => `${e.nome}${e.ca ? ` — CA ${e.ca}` : ''}`))}
      </div>

      {secao(6, 'Qualificações e autorizações do trabalhador')}
      <div style={{ border: '1px solid var(--colorNeutralStroke2)', padding: '6px 10px', marginTop: 4 }}>
        {lista([
          ...minuta.treinamentos.map((t) => `${t.curso}: ${t.situacao === 'Valido' ? `válido até ${data(t.validade)}` : t.situacao === 'Vencido' ? `vencido em ${data(t.validade)}` : 'não realizado'}`),
          minuta.aso ? `ASO: ${minuta.aso.valido ? `apto até ${data(minuta.aso.validade)}` : 'vencido ou sem aptidão'}` : 'ASO: não cadastrado',
        ])}
      </div>

      {secao(7, 'Responsabilidades do trabalhador')}
      <div style={{ border: '1px solid var(--colorNeutralStroke2)', padding: '6px 10px', marginTop: 4 }}>
        {lista([
          'Cumprir as disposições legais e regulamentares de SST e as instruções desta OS (NR-01, item 1.4.2).',
          'Utilizar máquinas, ferramentas e equipamentos somente para os quais esteja capacitado e autorizado, obedecendo aos procedimentos e normas da empresa.',
          'Usar os EPIs fornecidos, guardá-los, conservá-los e comunicar qualquer alteração que os torne impróprios (NR-06).',
          'Comunicar prontamente acidentes, incidentes, quase acidentes, condições inseguras, falhas de equipamentos e mudanças nas condições de trabalho.',
          'Participar das orientações e capacitações aplicáveis e colaborar com as medidas de prevenção.',
          'Exercer o direito de recusa diante de risco grave e iminente, comunicando imediatamente a liderança (NR-01, item 1.4.3).',
        ])}
      </div>

      {secao(8, 'Políticas corporativas aplicáveis')}
      <div style={{ border: '1px solid var(--colorNeutralStroke2)', padding: '6px 10px', marginTop: 4 }}>
        {lista(minuta.politicas.map((p) => `${p.codigo ? `${p.codigo} — ` : ''}${p.titulo} (rev. ${p.revisao ?? '—'})${p.formaConsulta ? ` • Consulta: ${p.formaConsulta}` : ''}`))}
      </div>

      {secao(9, 'Ciência e responsabilidade')}
      <div style={{ border: '1px solid var(--colorNeutralStroke2)', padding: '8px 10px', marginTop: 4 }}>
        <Text style={{ display: 'block', textAlign: 'justify' }}>
          Declaro ter recebido esta Ordem de Serviço, ter sido informado(a) de forma clara sobre as atividades, os riscos, as medidas
          de prevenção e os limites da minha função, e comprometo-me a cumpri-la. Estou ciente de que a recusa injustificada em observar
          as instruções de segurança e em usar os EPIs constitui ato faltoso (CLT, art. 158, parágrafo único).
        </Text>
        {(() => {
          const funcAssinatura = ultimaOs?.funcionario;
          const tecAssinatura = ultimaOs?.tecnico;
          const nomeInstrutor = tecAssinatura?.nome
            || minuta.tecnicoResponsavel?.nome
            || (usuario?.ehInstrutor || usuario?.nome ? usuario.nome : null)
            || 'Técnico / Instrutor de SST';
          const cargoInstrutor = tecAssinatura?.funcao
            || [
              minuta.tecnicoResponsavel?.funcao || usuario?.formacaoInstrutor || 'Técnico de Segurança do Trabalho',
              minuta.tecnicoResponsavel?.registro || usuario?.registroInstrutor || null,
            ].filter(Boolean).join(' • ');

          const blocoAssinatura = (
            papel: string,
            nome: string,
            cargo: string,
            assinadoEm?: string | null,
            metodo?: number | null,
          ) => (
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'stretch' }}>
              <div style={{ textAlign: 'center', fontWeight: 700, fontSize: 11, letterSpacing: 0.3, marginBottom: 6 }}>
                {papel}
              </div>
              <div style={{
                border: `1.5px solid ${corMarca}`,
                borderRadius: 5,
                background: '#ffffff',
                display: 'flex',
                overflow: 'hidden',
                minHeight: 54,
                margin: '0 6px',
              }}>
                <div style={{ width: 5, background: corMarca, flexShrink: 0 }} />
                <div style={{ padding: '6px 10px', display: 'flex', flexDirection: 'column', justifyContent: 'center', flex: 1 }}>
                  <div style={{ color: corMarca, fontWeight: 700, fontSize: 10, letterSpacing: 0.6 }}>
                    {assinadoEm ? 'ASSINADO DIGITALMENTE' : 'IDENTIFICAÇÃO DO SIGNATÁRIO (ASSINATURA ELETRÔNICA)'}
                  </div>
                  <div style={{ fontWeight: 700, fontSize: 11.5, color: '#1f1f1f', textTransform: 'uppercase', marginTop: 1 }}>
                    {nome}
                  </div>
                  <div style={{ fontSize: 10.5, color: '#555555', marginTop: 1 }}>
                    {assinadoEm
                      ? `Assinado em ${dataHora(assinadoEm)} · ${metodo ? metodoAutenticacaoAssinaturaLabel[metodo] ?? 'biometria' : 'biometria'}`
                      : `${cargo} · Aguardando coleta biométrica na emissão`}
                  </div>
                </div>
              </div>
              <div style={{ borderTop: '1px solid #1f1f1f', marginTop: 6, paddingTop: 4, textAlign: 'center' }}>
                <div style={{ fontWeight: 700, fontSize: 12, textTransform: 'uppercase' }}>{nome}</div>
                <div style={{ fontSize: 11, color: 'var(--colorNeutralForeground3)' }}>{cargo}</div>
              </div>
            </div>
          );

          return (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))', gap: 24, marginTop: 20, paddingTop: 8 }}>
              {blocoAssinatura(
                'TRABALHADOR(A)',
                funcAssinatura?.nome || minuta.trabalhador.nome,
                `${minuta.trabalhador.funcao}${minuta.trabalhador.cbo ? ` • CBO ${minuta.trabalhador.cbo}` : ''}`,
                funcAssinatura?.assinadoEm,
                funcAssinatura?.metodo,
              )}
              {blocoAssinatura(
                'TÉCNICO DE SEGURANÇA DO TRABALHO / INSTRUTOR',
                nomeInstrutor,
                cargoInstrutor,
                tecAssinatura?.assinadoEm,
                tecAssinatura?.metodo,
              )}
            </div>
          );
        })()}
      </div>
    </div>
  );
}
