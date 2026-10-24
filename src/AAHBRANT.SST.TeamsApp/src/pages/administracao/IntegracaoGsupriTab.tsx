import { useEffect, useState } from 'react';
import { Button, Card, DataTable, Field, FeedbackInline, Input, Legenda, Select, Text, type Coluna } from '@ui';
import { gsupri, type PainelGsupri, type RecebimentoGsupri, type VinculoProdutoGsupri } from '../../lib/gsupri';
import { useUsuarioLogado } from '../../lib/UsuarioLogadoContext';

const status: Record<string, string> = { Pendente: 'Com pendências', Processado: 'Processado', Cancelado: 'Cancelado', PendenteRegularizacao: 'Regularização necessária' };
const vazio: VinculoProdutoGsupri = { codigoExterno: '', unidade: '', categoria: 'EPI', catalogoId: null, tamanho: '', fatorConversao: 1 };
const grade = { display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: 16 };

export function IntegracaoGsupriTab() {
  const { usuario, carregando } = useUsuarioLogado();
  if (carregando) return <Legenda>Carregando permissões…</Legenda>;
  if (!usuario?.ehAdministrador) return <FeedbackInline tom="aviso">A configuração da integração é restrita à administração.</FeedbackInline>;
  return <Painel />;
}

function Painel() {
  const [painel, setPainel] = useState<PainelGsupri | null>(null);
  const [pagina, setPagina] = useState(1);
  const [obras, setObras] = useState<{ id: string; nome: string }[]>([]);
  const [catalogos, setCatalogos] = useState<Record<string, { id: string; nome: string }[]>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [aberto, setAberto] = useState<string | null>(null);
  const [formulario, setFormulario] = useState<'obra' | 'produto' | null>(null);
  const [obraCodigo, setObraCodigo] = useState('');
  const [obraId, setObraId] = useState('');
  const [produto, setProduto] = useState<VinculoProdutoGsupri>(vazio);

  useEffect(() => {
    let ativo = true;
    setOcupado(true);
    Promise.all([gsupri.painel(pagina), gsupri.opcoes()])
      .then(([p, o]) => { if (ativo) { setPainel(p); setObras(o.obras); setCatalogos(o.catalogos); setErro(null); } })
      .catch(e => { if (ativo) setErro(e instanceof Error ? e.message : 'Falha ao carregar integração.'); })
      .finally(() => { if (ativo) setOcupado(false); });
    return () => { ativo = false; };
  }, [pagina]);

  async function executar(acao: () => Promise<unknown>, mensagem?: string) {
    setOcupado(true); setErro(null); setAviso(null);
    try {
      await acao(); setPainel(await gsupri.painel(pagina));
      if (mensagem) { setAviso(mensagem); setFormulario(null); }
    } catch (e) { setErro(e instanceof Error ? e.message : 'Não foi possível concluir a operação.'); }
    finally { setOcupado(false); }
  }
  const colunas: Coluna<RecebimentoGsupri>[] = [
    { chave: 'recebimentoId', rotulo: 'Recebimento' }, { chave: 'numeroNota', rotulo: 'Nota fiscal' },
    { chave: 'obraCodigo', rotulo: 'Obra', render: r => painel?.obras.find(o => o.obraId === r.obraId)?.nome ?? r.obraCodigo },
    { chave: 'recebidoEm', rotulo: 'Recebido em', render: r => new Date(r.recebidoEm).toLocaleString('pt-BR') },
    { chave: 'status', rotulo: 'Situação', render: r => status[r.status] ?? r.status },
  ];
  return <div style={{ display: 'grid', gap: 20 }}>
    {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
    {aviso && <FeedbackInline tom="sucesso">{aviso}</FeedbackInline>}
    <Card titulo="Estoque conectado ao G-SUPRI" subtitulo="Recebimentos de EPI, EPC e uniformes, sem repetir o lançamento de estoque.">
      <FeedbackInline tom="info">{painel?.habilitada ? 'Recepção habilitada. A chegada dos dados depende do envio pelo G-SUPRI.' : 'Aguardando ativação da integração com a equipe do G-SUPRI.'}</FeedbackInline>
      <p>Vincule os códigos de obras e produtos uma vez. Os próximos recebimentos reconhecidos serão processados automaticamente. Materiais sem liberação permanecem pendentes.</p>
      <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
        <Button disabled={ocupado} onClick={() => void executar(async () => {})}>Atualizar</Button>
        <Button disabled={ocupado} onClick={() => { setObraCodigo(''); setObraId(''); setFormulario('obra'); }}>Vincular obra</Button>
        <Button disabled={ocupado} onClick={() => { setProduto(vazio); setFormulario('produto'); }}>Vincular produto</Button>
      </div>
    </Card>
    {formulario === 'obra' && <Card titulo="Vincular obra do G-SUPRI">
      <form onSubmit={e => { e.preventDefault(); void executar(() => gsupri.obra(obraCodigo, obraId), 'Obra vinculada. Reprocesse os recebimentos pendentes dessa obra.'); }}>
        <div style={grade}>
          <Field label="Código da obra no G-SUPRI"><Input aria-label="Código da obra no G-SUPRI" required maxLength={100} value={obraCodigo} onChange={(_, d) => setObraCodigo(d.value)} /></Field>
          <Field label="Obra no SST"><Select aria-label="Obra no SST" required value={obraId} onChange={(_, d) => setObraId(d.value)}><option value="">Selecione</option>{obras.map(o => <option key={o.id} value={o.id}>{o.nome}</option>)}</Select></Field>
        </div>
        <p><Legenda>Confira o destino antes de salvar. O vínculo é permanente para preservar o histórico.</Legenda></p>
        <Button type="submit" appearance="primary" disabled={ocupado || !obraId || !obraCodigo.trim()}>Salvar vínculo da obra</Button>{' '}
        <Button disabled={ocupado} onClick={() => setFormulario(null)}>Cancelar</Button>
      </form>
    </Card>}
    {formulario === 'produto' && <Card titulo="Vincular produto do G-SUPRI">
      <form onSubmit={e => { e.preventDefault(); void executar(() => gsupri.produto(produto), 'Produto vinculado. Reprocesse os recebimentos pendentes para aplicar o vínculo.'); }}>
        <div style={grade}>
          <Field label="Código do produto no G-SUPRI"><Input aria-label="Código do produto no G-SUPRI" required maxLength={100} value={produto.codigoExterno} onChange={(_, d) => setProduto({ ...produto, codigoExterno: d.value })} /></Field>
          <Field label="Unidade recebida"><Input aria-label="Unidade recebida" required maxLength={10} placeholder="UN, PAR, CX…" value={produto.unidade} onChange={(_, d) => setProduto({ ...produto, unidade: d.value })} /></Field>
          <Field label="Destino do material"><Select aria-label="Destino do material" value={produto.categoria} onChange={(_, d) => setProduto({ ...produto, categoria: d.value, catalogoId: null, tamanho: '' })}><option>EPI</option><option>EPC</option><option value="UNIFORME">Uniforme</option><option value="IGNORAR">Fora do escopo SST</option></Select></Field>
          {produto.categoria !== 'IGNORAR' && <>
            <Field label="Produto no catálogo SST"><Select aria-label="Produto no catálogo SST" required value={produto.catalogoId ?? ''} onChange={(_, d) => setProduto({ ...produto, catalogoId: d.value || null })}><option value="">Selecione</option>{(catalogos[produto.categoria] ?? []).map(p => <option value={p.id} key={p.id}>{p.nome}</option>)}</Select></Field>
            <Field label="Unidades de estoque por unidade recebida"><Input aria-label="Fator de conversão" type="number" min="0.000001" max="1000000" step="0.000001" required value={String(produto.fatorConversao)} onChange={(_, d) => setProduto({ ...produto, fatorConversao: Number(d.value) })} /></Field>
            {produto.categoria === 'UNIFORME' && <Field label="Tamanho"><Input aria-label="Tamanho" required maxLength={20} value={produto.tamanho} onChange={(_, d) => setProduto({ ...produto, tamanho: d.value })} /></Field>}
          </>}
        </div>
        <p><Legenda>Exemplo: uma caixa com 100 unidades usa fator 100. O vínculo é permanente. Para EPI com numeração, selecione um cadastro específico para aquele tamanho.</Legenda></p>
        <Button type="submit" appearance="primary" disabled={ocupado}>Salvar vínculo do produto</Button>{' '}
        <Button disabled={ocupado} onClick={() => setFormulario(null)}>Cancelar</Button>
      </form>
    </Card>}
    <Card titulo="Recebimentos" subtitulo={`${painel?.totalRecebimentos ?? 0} recebimento(s) registrado(s). Abra uma linha para conferir os itens.`}>
      <DataTable aria-label="Recebimentos G-SUPRI" colunas={colunas} linhas={painel?.recebimentos ?? []} chaveLinha={r => r.id} carregando={ocupado && !painel}
        vazio={{ titulo: 'Aguardando o primeiro recebimento', descricao: 'As notas e os materiais aparecerão aqui quando a equipe do G-SUPRI conectar o envio.' }}
        aoClicarLinha={r => setAberto(aberto === r.id ? null : r.id)} expansivel={{ aberta: r => aberto === r.id, render: r => <div style={{ display: 'grid', gap: 12 }}>
          <Text>Pedido {r.pedidoId} · Versão {r.versao}</Text>
          {r.pendencia && <FeedbackInline tom="aviso">{r.pendencia}</FeedbackInline>}
          {!r.obraId && <Button disabled={ocupado} onClick={() => { setObraCodigo(r.obraCodigo); setObraId(''); setFormulario('obra'); }}>Resolver vínculo da obra</Button>}
          <DataTable aria-label={`Itens de ${r.recebimentoId}`} linhas={r.itens} chaveLinha={i => i.itemId} colunas={[
            { chave: 'descricao', rotulo: 'Material' }, { chave: 'unidade', rotulo: 'Unidade de origem' },
            { chave: 'quantidadeRecebida', rotulo: 'Recebido' }, { chave: 'quantidadeAplicada', rotulo: 'Aplicado no estoque' },
            { chave: 'pendencia', rotulo: 'Pendência', render: i => i.pendencia ?? '—' },
          ]} acoesLinha={i => !painel?.produtos.some(p => p.codigoExterno === i.produtoCodigo && p.unidade === i.unidade) ?
            <Button disabled={ocupado} onClick={() => { setProduto({ ...vazio, codigoExterno: i.produtoCodigo, unidade: i.unidade }); setFormulario('produto'); }}>Vincular material</Button> : null} />
          <Legenda>“Aplicado” está na unidade de estoque, após a conversão de embalagens.</Legenda>
          <Button disabled={ocupado} onClick={() => void executar(() => gsupri.reprocessar(r.id), 'Recebimento reavaliado. Confira a situação atual na lista.')}>Reprocessar recebimento</Button>
        </div> }} />
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginTop: 12 }}>
        <Button disabled={ocupado || pagina === 1} onClick={() => setPagina(pagina - 1)}>Anterior</Button><Legenda>Página {pagina}</Legenda>
        <Button disabled={ocupado || pagina * 25 >= (painel?.totalRecebimentos ?? 0)} onClick={() => setPagina(pagina + 1)}>Próxima</Button>
      </div>
    </Card>
    <Card titulo="Vínculos cadastrados" subtitulo="Configuração usada nos próximos recebimentos.">
      <DataTable aria-label="Obras vinculadas G-SUPRI" linhas={painel?.obras ?? []} chaveLinha={o => o.codigoExterno} colunas={[{ chave: 'codigoExterno', rotulo: 'Código no G-SUPRI' }, { chave: 'nome', rotulo: 'Obra no SST' }]} vazio={{ titulo: 'Nenhuma obra vinculada' }} />
      <DataTable aria-label="Produtos vinculados G-SUPRI" linhas={painel?.produtos ?? []} chaveLinha={p => `${p.codigoExterno}/${p.unidade}`} colunas={[
        { chave: 'codigoExterno', rotulo: 'Código no G-SUPRI' }, { chave: 'unidade', rotulo: 'Unidade' },
        { chave: 'categoria', rotulo: 'Destino', render: p => p.categoria === 'IGNORAR' ? 'Fora do escopo' : `${p.categoria} · ${catalogos[p.categoria]?.find(c => c.id === p.catalogoId)?.nome ?? 'Catálogo indisponível'}` },
        { chave: 'tamanho', rotulo: 'Tamanho' }, { chave: 'fatorConversao', rotulo: 'Fator' },
      ]} vazio={{ titulo: 'Nenhum produto vinculado' }} />
    </Card>
  </div>;
}
