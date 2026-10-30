import { useEffect, useMemo, useRef, useState } from 'react';
import { Add24Regular, ArrowLeft24Regular, ArrowRight24Regular, DocumentPdf24Regular, Eye24Regular } from '@fluentui/react-icons';
import {
  Button, CalendarioMensal, Card, Checkbox, Dialog, DialogActions, DialogBody, DialogContent,
  DialogSurface, DialogTitle, FeedbackInline, Field, Input, PageHeader, Select, Spinner, StatusChip, Text, Textarea, designTokens,
} from '@ui';
import { api, type CatalogoTemaDds, type CronogramaMensalDds, type Obra, type TemaCronogramaDds } from '../../lib/api';
import { dataDoDia } from '../../lib/datasCalendario';
import { useObraAtiva } from '../../lib/ObraAtivaContext';
import { useUsuarioLogado } from '../../lib/UsuarioLogadoContext';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const linha = { display: 'flex', flexWrap: 'wrap' as const, alignItems: 'center', gap: 12 };
const falha = (e: unknown) => e instanceof Error ? e.message : 'Não foi possível concluir. Tente novamente.';

export function CronogramaMensalDdsTab() {
  const { obraAtivaId, definirObraAtiva } = useObraAtiva();
  const { usuario } = useUsuarioLogado();
  const podeEditar = usuario?.ehAdministrador || usuario?.permissoes.includes('dds:criar');
  const podeExportar = usuario?.ehAdministrador || usuario?.permissoes.includes('dds:exportar');
  const [obras, setObras] = useState<Obra[]>([]);
  const [catalogo, setCatalogo] = useState<CatalogoTemaDds[]>([]);
  const [mes, setMes] = useState(() => new Date(new Date().getFullYear(), new Date().getMonth(), 1));
  const [cronograma, setCronograma] = useState<CronogramaMensalDds | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [erroDia, setErroDia] = useState<string | null>(null);
  const [dia, setDia] = useState<string | null>(null);
  const [selecao, setSelecao] = useState<TemaCronogramaDds[]>([]);
  const [busca, setBusca] = useState('');
  const [novoTema, setNovoTema] = useState(false);
  const [nome, setNome] = useState('');
  const [descricao, setDescricao] = useState('');
  const [semanaCompleta, setSemanaCompleta] = useState(false);
  const [recarregar, setRecarregar] = useState(0);
  const [logo, setLogo] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const sucesso = useSucessoToast();
  const contexto = `${obraAtivaId}/${mes.getFullYear()}/${mes.getMonth() + 1}`;
  const contextoRef = useRef(contexto);
  const obra = obras.find(o => o.id === obraAtivaId);
  const ano = mes.getFullYear();
  const numeroMes = mes.getMonth() + 1;
  const nomeMes = mes.toLocaleDateString('pt-BR', { month: 'long', year: 'numeric' });
  const dias = useMemo(() => new Map(cronograma?.dias.map(d => [d.data, d.temas]) ?? []), [cronograma]);
  const temFimSemana = cronograma?.dias.some(d => [0, 6].includes(dataDoDia(d.data).getDay())) ?? false;

  useEffect(() => {
    let ativo = true;
    Promise.all([api.obras.listar(), api.catalogoTemasDds.listar()])
      .then(([o, t]) => { if (ativo) { setObras(o); setCatalogo(t); } })
      .catch(e => { if (ativo) setErro(falha(e)); });
    return () => { ativo = false; };
  }, [recarregar]);

  useEffect(() => {
    let ativo = true;
    contextoRef.current = contexto;
    // Limpa os dados da obra anterior enquanto a consulta do novo contexto está em andamento.
    // oxlint-disable-next-line react/set-state-in-effect
    setCronograma(null); setDia(null); setErro(null);
    if (!obraAtivaId) { setCarregando(false); return; }
    setCarregando(true);
    api.cronogramasMensaisDds.obter(obraAtivaId, ano, numeroMes)
      .then(c => { if (ativo) setCronograma(c); })
      .catch(e => { if (ativo) setErro(falha(e)); })
      .finally(() => { if (ativo) setCarregando(false); });
    return () => { ativo = false; };
  }, [obraAtivaId, ano, numeroMes, recarregar, contexto]);

  useEffect(() => {
    let ativo = true;
    let url: string | null = null;
    // Não exibir a logo da obra anterior enquanto a próxima imagem é baixada.
    // oxlint-disable-next-line react/set-state-in-effect
    setLogo(null);
    if (obra?.temLogo) api.obras.baixarLogo(obra.id).then(blob => {
      if (ativo) { url = URL.createObjectURL(blob); setLogo(url); }
    }).catch(() => { /* O servidor valida a logo novamente ao emitir o documento. */ });
    return () => { ativo = false; if (url) URL.revokeObjectURL(url); };
  }, [obra?.id, obra?.temLogo, recarregar]);

  function abrirDia(chave: string) {
    setDia(chave); setSelecao([...(dias.get(chave) ?? [])]); setBusca('');
    setNovoTema(false); setNome(''); setDescricao(''); setErroDia(null);
  }

  async function salvarDia() {
    if (!dia || !cronograma || !podeEditar) return;
    const atual = contexto;
    setOcupado(true); setErroDia(null);
    try {
      const novosDias = cronograma.dias.filter(d => d.data !== dia);
      if (selecao.length) novosDias.push({ data: dia, temas: selecao });
      const salvo = await api.cronogramasMensaisDds.salvar(obraAtivaId, ano, numeroMes, cronograma.versao, novosDias);
      if (contextoRef.current === atual) { setCronograma(salvo); setDia(null); sucesso('Programação do dia salva.'); }
    } catch (e) { if (contextoRef.current === atual) setErroDia(falha(e)); }
    finally { setOcupado(false); }
  }

  async function criarTema() {
    if (!nome.trim() || !podeEditar) return;
    const atual = contexto;
    setOcupado(true); setErroDia(null);
    try {
      const { id } = await api.catalogoTemasDds.criar(nome.trim(), descricao.trim() || null);
      const tema = { id, nome: nome.trim(), descricao: descricao.trim() || null };
      setCatalogo(c => [...c, tema]);
      if (contextoRef.current === atual) {
        setSelecao(s => [...s, { catalogoTemaDdsId: id, nome: tema.nome, descricao: tema.descricao }]);
        setNovoTema(false); setNome(''); setDescricao(''); setBusca('');
      }
    } catch (e) { if (contextoRef.current === atual) setErroDia(falha(e)); }
    finally { setOcupado(false); }
  }

  async function documento(emitir: boolean) {
    if (!cronograma?.id || !obra?.temLogo) return;
    const atual = contexto;
    const nomeArquivo = `cronograma-dds-${ano}-${String(numeroMes).padStart(2, '0')}.pdf`;
    const obter = () => api.cronogramasMensaisDds.pdf(cronograma.id!, cronograma.versao, semanaCompleta || temFimSemana, emitir);
    setOcupado(true); setErro(null);
    try {
      if (emitir) {
        salvarBlob(await obter(), nomeArquivo);
        const atualizado = await api.cronogramasMensaisDds.obter(obraAtivaId, ano, numeroMes);
        if (contextoRef.current === atual) { setCronograma(atualizado); sucesso('PDF emitido e guardado no histórico.'); }
      } else await visualizar({ titulo: 'Prévia do cronograma mensal de DDS', nomeArquivo, obter });
    } catch (e) { if (contextoRef.current === atual) setErro(falha(e)); }
    finally { setOcupado(false); }
  }

  const opcoes = useMemo(() => {
    const lista: TemaCronogramaDds[] = catalogo.map(t => ({ catalogoTemaDdsId: t.id, nome: t.nome, descricao: t.descricao }));
    for (const t of selecao) if (!lista.some(o => o.catalogoTemaDdsId === t.catalogoTemaDdsId)) lista.push(t);
    const normalizar = (texto: string) => texto.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
    return lista.filter(t => normalizar(t.nome).includes(normalizar(busca))).sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
  }, [catalogo, selecao, busca]);

  return <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
    {dialogoVisualizador}
    <PageHeader titulo="Cronograma mensal de DDS" subtitulo="Planejamento flexível dos temas de cada dia, sujeito às necessidades da obra." acoes={<div style={linha}>
      <Button icon={<Eye24Regular />} disabled={!cronograma?.id || !obra?.temLogo || ocupado || !podeExportar} onClick={() => documento(false)}>Visualizar documento</Button>
      <Button appearance="primary" icon={<DocumentPdf24Regular />} disabled={!cronograma?.id || !obra?.temLogo || ocupado || !podeExportar} onClick={() => documento(true)}>Emitir PDF</Button>
    </div>} />
    {erro && <FeedbackInline tom="erro">{erro} <Button size="small" onClick={() => setRecarregar(v => v + 1)}>Tentar novamente</Button></FeedbackInline>}
    <Card densidade="compacta">
      <div style={{ ...linha, justifyContent: 'space-between' }}>
        <div style={linha}>
          {logo && <img src={logo} alt={`Logo da obra ${obra?.nome}`} style={{ width: 100, height: 52, objectFit: 'contain' }} />}
          <Field label="Obra do cronograma">
            <Select aria-label="Obra do cronograma" value={obraAtivaId} disabled={ocupado} onChange={(_, d) => definirObraAtiva(d.value, obras.find(o => o.id === d.value)?.nome)}>
              <option value="">Selecione uma obra</option>
              {obras.map(o => <option key={o.id} value={o.id}>{o.nome}</option>)}
            </Select>
          </Field>
          <StatusChip tom="info">Planejamento preliminar</StatusChip>
        </div>
        <div style={linha}>
          <Button aria-label="Mês anterior" icon={<ArrowLeft24Regular />} disabled={ocupado || ano === 2000 && numeroMes === 1} onClick={() => setMes(new Date(ano, numeroMes - 2, 1))} />
          <Text size={500} weight="semibold" style={{ textTransform: 'capitalize', minWidth: 180, textAlign: 'center' }}>{nomeMes}</Text>
          <Button aria-label="Próximo mês" icon={<ArrowRight24Regular />} disabled={ocupado || ano === 2100 && numeroMes === 12} onClick={() => setMes(new Date(ano, numeroMes, 1))} />
          <Button disabled={ocupado} onClick={() => setMes(new Date(new Date().getFullYear(), new Date().getMonth(), 1))}>Mês atual</Button>
        </div>
      </div>
    </Card>
    {!obraAtivaId && <FeedbackInline tom="info">Selecione uma obra para planejar os temas e usar a respectiva logo no documento.</FeedbackInline>}
    {obra && !obra.temLogo && <FeedbackInline tom="aviso">Cadastre a logo em Administração → Obras para visualizar e emitir o PDF desta obra. Você já pode planejar os temas.</FeedbackInline>}
    {carregando && <Spinner label="Carregando cronograma..." />}
    {cronograma && <>
      <div style={{ ...linha, justifyContent: 'space-between' }}>
        <Text>{cronograma.dias.length} dias com temas · Clique em um dia para {podeEditar ? 'selecionar os temas' : 'consultar os temas'}.</Text>
        <Text size={200}>{cronograma.id ? 'Alterações salvas por dia' : 'Mês ainda sem programação'}</Text>
      </div>
      <Text size={200}>Este planejamento não registra DDS realizados nem gera pendências de execução.</Text>
      <CalendarioMensal mes={mes} primeiroDia={1} aoSelecionar={abrirDia}
        desabilitarDia={(_, fora) => fora || ocupado}
        rotuloDia={chave => dias.get(chave)?.map(t => t.nome).join(', ') || 'Sem temas planejados'}
        conteudoDia={(chave, data, fora) => fora ? null : <>
          {(dias.get(chave) ?? []).slice(0, 3).map(t => <span key={t.catalogoTemaDdsId} style={{ borderLeft: `3px solid ${designTokens.colorPrimary}`, padding: '3px 6px', fontSize: 12, lineHeight: '16px', overflowWrap: 'anywhere' }}>{t.nome}</span>)}
          {(dias.get(chave)?.length ?? 0) > 3 && <span style={{ fontSize: 11 }}>+{dias.get(chave)!.length - 3} temas</span>}
          {!dias.has(chave) && <span style={{ fontSize: 12, color: designTokens.colorNeutralMedium }}>{[0, 6].includes(data.getDay()) ? 'Sem programação' : '+ Selecionar temas'}</span>}
        </>} />
      <div style={{ ...linha, justifyContent: 'space-between' }}>
        <Checkbox label="Incluir sábado e domingo no documento" checked={semanaCompleta || temFimSemana} disabled={temFimSemana || ocupado} onChange={(_, d) => setSemanaCompleta(d.checked === true)} />
        <Text size={200}>{temFimSemana ? 'Há temas no fim de semana; esses dias serão incluídos.' : 'O documento usa colunas de segunda a sexta por padrão.'}</Text>
      </div>
      {cronograma.revisoes.length > 0 && <Card densidade="compacta">
        <Text weight="semibold">Documentos emitidos</Text>
        <Text size={200}>Cada revisão mantém os temas e a logo usados na emissão.</Text>
        <div style={linha}>{cronograma.revisoes.map(r => <Button key={r.id} disabled={!podeExportar || ocupado} onClick={() => visualizar({
          titulo: `Cronograma mensal · Revisão ${r.numero}`, nomeArquivo: `cronograma-dds-r${r.numero}.pdf`,
          obter: () => api.cronogramasMensaisDds.revisaoPdf(cronograma.id!, r.id),
        })}>Revisão {String(r.numero).padStart(2, '0')} · {new Date(r.emitidoEmUtc).toLocaleDateString('pt-BR')}</Button>)}</div>
      </Card>}
    </>}
    <Dialog open={dia !== null} onOpenChange={(_, d) => { if (!d.open && !ocupado) setDia(null); }}>
      <DialogSurface style={{ maxWidth: 660, width: '95vw' }}><DialogBody>
        <DialogTitle>{dia ? dataDoDia(dia).toLocaleDateString('pt-BR', { weekday: 'long', day: '2-digit', month: 'long' }) : 'Temas do dia'}</DialogTitle>
        <DialogContent>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
            <Text>Selecione um ou mais temas do catálogo para este dia.</Text>
            {erroDia && <FeedbackInline tom="erro">{erroDia}<Button size="small" disabled={ocupado} onClick={() => { setDia(null); setRecarregar(v => v + 1); }}>Recarregar mês e descartar seleção</Button></FeedbackInline>}
            <Input aria-label="Buscar tema de DDS" placeholder="Buscar no catálogo de temas..." value={busca} disabled={ocupado} onChange={(_, d) => setBusca(d.value)} />
            <div style={{ maxHeight: '38vh', overflowY: 'auto', display: 'flex', flexDirection: 'column' }}>
              {opcoes.map(t => <div key={t.catalogoTemaDdsId}>
                <Checkbox label={t.nome} checked={selecao.some(s => s.catalogoTemaDdsId === t.catalogoTemaDdsId)} disabled={ocupado || !podeEditar}
                  onChange={(_, d) => setSelecao(s => d.checked ? [...s, t] : s.filter(v => v.catalogoTemaDdsId !== t.catalogoTemaDdsId))} />
                {t.descricao && <Text size={200} style={{ display: 'block', margin: '0 12px 8px 36px', color: designTokens.colorNeutralMedium, whiteSpace: 'pre-wrap' }}>{t.descricao}</Text>}
              </div>)}
              {!opcoes.length && <Text>Nenhum tema encontrado.</Text>}
            </div>
            {podeEditar && <Button appearance="subtle" icon={<Add24Regular />} disabled={ocupado} onClick={() => setNovoTema(!novoTema)}>Cadastrar novo tema</Button>}
            {novoTema && <Card densidade="compacta">
              <Field label="Nome do novo tema" required><Input value={nome} maxLength={200} disabled={ocupado} onChange={(_, d) => setNome(d.value)} /></Field>
              <Field label="Descrição"><Textarea value={descricao} disabled={ocupado} onChange={(_, d) => setDescricao(d.value)} /></Field>
              <Button disabled={ocupado || !nome.trim()} onClick={criarTema}>Criar tema e selecionar</Button>
            </Card>}
            <Text size={200}>{selecao.length} tema(s) selecionado(s). Ao deixar vazio, a programação deste dia será removida.</Text>
          </div>
        </DialogContent>
        <DialogActions>
          <Button disabled={ocupado} onClick={() => setDia(null)}>Cancelar</Button>
          {podeEditar && <Button appearance="primary" disabled={ocupado || selecao.length > 50} onClick={salvarDia}>{ocupado ? 'Salvando...' : 'Salvar dia'}</Button>}
        </DialogActions>
      </DialogBody></DialogSurface>
    </Dialog>
  </div>;
}
