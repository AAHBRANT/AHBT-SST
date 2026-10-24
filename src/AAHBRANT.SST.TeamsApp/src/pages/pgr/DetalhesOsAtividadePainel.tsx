import { useEffect, useState } from 'react';
import {
  Button, Card, Carregando, Checkbox, FeedbackInline, Input, StatusChip, Text, Textarea,
} from '@ui';
import {
  api, type CatalogoEpc, type DetalhesOsAtividade, type ItemAtividadeOs, type MaterialApoio,
} from '../../lib/api';

const tiposItem: { valor: 1 | 2 | 3; rotulo: string; dica: string }[] = [
  { valor: 1, rotulo: 'Interromper a atividade e avisar a liderança quando', dica: 'Ex.: vento forte ou chuva' },
  { valor: 2, rotulo: 'Não executar sem capacitação, autorização ou liberação', dica: 'Ex.: trabalho acima de 2 m' },
  { valor: 3, rotulo: 'Autorizações e liberações exigidas', dica: 'Ex.: PT emitida, APR assinada' },
];

const dataCurta = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString('pt-BR') : '');

// Detalhes de uma atividade do PGR para a Ordem de Serviço (20/10/2026). Mesma regra das medidas:
// o que vem do PGR, da Matriz de Treinamento ou é escrito pela SST entra validado; sugestão da IA fica
// amarela até a SST aceitar, editar ou descartar.
export function DetalhesOsAtividadePainel({ atividadeId, aoAlterar }: { atividadeId: string; aoAlterar: () => Promise<void> }) {
  const [detalhes, setDetalhes] = useState<DetalhesOsAtividade | null>(null);
  const [epcsCatalogo, setEpcsCatalogo] = useState<CatalogoEpc[]>([]);
  const [documentos, setDocumentos] = useState<MaterialApoio[]>([]);
  const [condicoes, setCondicoes] = useState('');
  const [ferramentas, setFerramentas] = useState('');
  const [epcIds, setEpcIds] = useState<string[]>([]);
  const [documentoIds, setDocumentoIds] = useState<string[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [salvo, setSalvo] = useState(false);

  async function carregar() {
    const d = await api.riscos.obterDetalhesOs(atividadeId);
    setDetalhes(d);
    setCondicoes(d.condicoes ?? '');
    setFerramentas(d.ferramentas.join('\n'));
    setEpcIds(d.epcs.map((e) => e.id));
    setDocumentoIds(d.documentos.map((x) => x.id));
  }

  useEffect(() => {
    void (async () => {
      try {
        const [, epcs, docs] = await Promise.all([carregar(), api.catalogosEpc.listar(), api.materiaisApoio.listar()]);
        setEpcsCatalogo(epcs);
        setDocumentos(docs.filter((m) => (m.tipo === 2 || m.tipo === 3) && m.situacao === 1));
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar os detalhes da atividade.');
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [atividadeId]);

  async function executar(acao: () => Promise<unknown>) {
    setOcupado(true);
    setErro(null);
    setSalvo(false);
    try {
      await acao();
      await carregar();
      await aoAlterar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível salvar.');
    } finally {
      setOcupado(false);
    }
  }

  if (!detalhes) return erro ? <FeedbackInline tom="erro">{erro}</FeedbackInline> : <Carregando variante="detalhe" linhas={3} />;

  const alternar = (lista: string[], id: string) => (lista.includes(id) ? lista.filter((x) => x !== id) : [...lista, id]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {salvo && <FeedbackInline tom="sucesso" aoFechar={() => setSalvo(false)}>Detalhes salvos.</FeedbackInline>}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 12 }}>
        <Card densidade="compacta" titulo="Em quais condições é executada">
          <Textarea value={condicoes} onChange={(_, d) => setCondicoes(d.value)} maxLength={2000} style={{ width: '100%' }} />
          {detalhes.condicoesDoPgr && condicoes === (detalhes.condicoes ?? '') && (
            <StatusChip tom="ok">Do PGR (ambiente e jornada do GHE)</StatusChip>
          )}
        </Card>

        <Card densidade="compacta" titulo="Ferramentas e equipamentos">
          <Textarea
            value={ferramentas}
            onChange={(_, d) => setFerramentas(d.value)}
            placeholder="Uma por linha"
            maxLength={2000}
            style={{ width: '100%' }}
          />
        </Card>

        <Card densidade="compacta" titulo="EPCs">
          {epcsCatalogo.length === 0 ? (
            <Text size={200}>Nenhum EPC no catálogo.</Text>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', maxHeight: 180, overflowY: 'auto' }}>
              {epcsCatalogo.map((e) => (
                <Checkbox key={e.id} label={e.nome} checked={epcIds.includes(e.id)} onChange={() => setEpcIds((l) => alternar(l, e.id))} />
              ))}
            </div>
          )}
        </Card>

        <Card densidade="compacta" titulo="Procedimentos aplicáveis">
          {documentos.length === 0 ? (
            <Text size={200}>Nenhum procedimento vigente. Cadastre em Gestão SST › Documentos &amp; Procedimentos.</Text>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', maxHeight: 180, overflowY: 'auto' }}>
              {documentos.map((m) => (
                <Checkbox
                  key={m.id}
                  label={`${m.codigo ?? ''} — ${m.nome} · rev. ${m.revisao ?? ''}`}
                  checked={documentoIds.includes(m.id)}
                  onChange={() => setDocumentoIds((l) => alternar(l, m.id))}
                />
              ))}
            </div>
          )}
          <Text size={200}>Só aparecem documentos vigentes.</Text>
        </Card>
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <Button
          appearance="primary"
          disabled={ocupado}
          onClick={() => void executar(async () => {
            await api.riscos.salvarDetalhesOs(atividadeId, {
              condicoes: condicoes.trim() || null,
              ferramentas: ferramentas.split('\n'),
              epcIds,
              documentoIds,
            });
            setSalvo(true);
          })}
        >
          Salvar condições, ferramentas, EPCs e procedimentos
        </Button>
      </div>

      <Card densidade="compacta" titulo="Treinamentos exigidos (Matriz de Treinamento)">
        {detalhes.treinamentos.length === 0 ? (
          <Text size={200}>Nenhum treinamento na Matriz para as funções deste GHE.</Text>
        ) : (
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
            {detalhes.treinamentos.map((t, i) => (
              <StatusChip key={i} tom="ok">{t.curso} · {t.funcao}</StatusChip>
            ))}
          </div>
        )}
      </Card>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 12 }}>
        {tiposItem.map((t) => (
          <BlocoItens
            key={t.valor}
            tipo={t}
            itens={detalhes.itens.filter((i) => i.tipo === t.valor)}
            ocupado={ocupado}
            executar={executar}
            atividadeId={atividadeId}
          />
        ))}
      </div>

      <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
        <Button disabled={ocupado} onClick={() => void executar(() => api.riscos.sugerirItensOs(atividadeId))}>
          {ocupado ? 'Aguarde…' : 'Sugerir itens com IA'}
        </Button>
      </div>
    </div>
  );
}

function BlocoItens({ tipo, itens, ocupado, executar, atividadeId }: {
  tipo: { valor: 1 | 2 | 3; rotulo: string; dica: string };
  itens: ItemAtividadeOs[];
  ocupado: boolean;
  executar: (acao: () => Promise<unknown>) => Promise<void>;
  atividadeId: string;
}) {
  const [texto, setTexto] = useState('');
  const [requisito, setRequisito] = useState('');
  const restricao = tipo.valor === 2;

  return (
    <Card densidade="compacta" titulo={tipo.rotulo}>
      {itens.map((i) => <CartaoItem key={i.id} item={i} ocupado={ocupado} executar={executar} />)}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, marginTop: 6 }}>
        <Input value={texto} onChange={(_, d) => setTexto(d.value)} placeholder={tipo.dica} maxLength={500} />
        {restricao && (
          <Input value={requisito} onChange={(_, d) => setRequisito(d.value)} placeholder="O que é exigido (ex.: NR-35 válido + PT emitida)" maxLength={300} />
        )}
        <Button
          size="small"
          disabled={ocupado || !texto.trim() || (restricao && !requisito.trim())}
          onClick={() => void executar(async () => {
            await api.riscos.adicionarItemOs(atividadeId, tipo.valor, texto.trim(), restricao ? requisito.trim() : undefined);
            setTexto('');
            setRequisito('');
          })}
        >
          + Adicionar
        </Button>
      </div>
    </Card>
  );
}

function CartaoItem({ item, ocupado, executar }: {
  item: ItemAtividadeOs;
  ocupado: boolean;
  executar: (acao: () => Promise<unknown>) => Promise<void>;
}) {
  const [editando, setEditando] = useState(false);
  const [texto, setTexto] = useState(item.texto);
  const [requisito, setRequisito] = useState(item.requisito ?? '');
  const pendente = item.status === 2;

  return (
    <div
      style={{
        border: '1px solid var(--colorNeutralStroke2)',
        borderRadius: 6,
        padding: 8,
        marginBottom: 6,
        background: pendente ? 'var(--colorPaletteYellowBackground1)' : undefined,
      }}
    >
      {editando ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
          <Input value={texto} onChange={(_, d) => setTexto(d.value)} maxLength={500} />
          {item.tipo === 2 && <Input value={requisito} onChange={(_, d) => setRequisito(d.value)} maxLength={300} />}
        </div>
      ) : (
        <Text>
          {item.texto}
          {item.requisito && <> → <b>{item.requisito}</b></>}
        </Text>
      )}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 6, marginTop: 6, flexWrap: 'wrap' }}>
        {pendente ? <StatusChip tom="atencao">Sugestão da IA</StatusChip> : <StatusChip tom="ok">Validado</StatusChip>}
        {pendente ? (
          <span style={{ display: 'inline-flex', gap: 4 }}>
            <Button
              size="small"
              appearance="primary"
              disabled={ocupado}
              onClick={() => void executar(() => api.riscos.decidirItemOs(item.id, true, editando ? texto : undefined, editando ? requisito : undefined))}
            >
              Aceitar
            </Button>
            {!editando && <Button size="small" disabled={ocupado} onClick={() => setEditando(true)}>Editar</Button>}
            <Button size="small" disabled={ocupado} onClick={() => void executar(() => api.riscos.decidirItemOs(item.id, false))}>
              Descartar
            </Button>
          </span>
        ) : (
          <Text size={100}>
            {item.validadoPorNome}{item.validadoEmUtc ? ` · ${dataCurta(item.validadoEmUtc)}` : ''}
          </Text>
        )}
      </div>
    </div>
  );
}
