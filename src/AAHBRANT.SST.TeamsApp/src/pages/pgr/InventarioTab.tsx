import { useState } from 'react';
import {
  Button, Card, Checkbox, DataTable, EstadoVazio, FeedbackInline, Select, StatusChip, Text, Textarea,
  type Coluna, type Tom,
} from '@ui';
import {
  api, nivelRiscoLabel, statusControleRiscoLabel,
  type AtividadeCaracterizada, type MedidaPrevencao, type RiscoClassificado,
} from '../../lib/api';

// Guia de conversão item 5 (mapeamento 1:1 pelo nome semântico do Fluent, preservando as mesmas
// cores da versão anterior): success→ok, informative→info, warning→atencao, severe→alerta,
// danger→alerta (5 valores do Fluent colapsam nos 4 tons de StatusChip; o rótulo textual continua
// distinguindo severo de perigoso, só a intensidade de cor colide — mesmo padrão já usado em
// AprEtapasTab.tsx/nivelRiscoAprCor).
const tomPorNivel: Record<number, Tom> = {
  1: 'ok',
  2: 'info',
  3: 'atencao',
  4: 'alerta',
  5: 'alerta',
};

const fases: { valor: 1 | 2 | 3; rotulo: string }[] = [
  { valor: 1, rotulo: 'Antes do trabalho' },
  { valor: 2, rotulo: 'Durante o trabalho' },
  { valor: 3, rotulo: 'Após o trabalho' },
];

const pendentes = (r: RiscoClassificado) => r.medidas.filter((m) => m.status === 2).length;
const temPendencia = (r: RiscoClassificado) => !r.avaliadoEmUtc || pendentes(r) > 0 || r.medidas.length === 0;
const dataCurta = (iso?: string | null) => (iso ? new Date(iso).toLocaleDateString('pt-BR') : '');

const colunasRiscos: Coluna<RiscoClassificado>[] = [
  { chave: 'perigo', rotulo: 'Perigo', render: (r) => r.perigoNome },
  {
    chave: 'nivel',
    rotulo: 'Nível de risco',
    render: (r) => <StatusChip tom={tomPorNivel[r.nivelRisco] ?? 'neutro'}>{nivelRiscoLabel[r.nivelRisco]}</StatusChip>,
  },
  {
    chave: 'avaliacao',
    rotulo: 'Avaliação',
    render: (r) =>
      r.avaliadoEmUtc ? <StatusChip tom="ok">Avaliado</StatusChip> : <StatusChip tom="alerta">Sem avaliação</StatusChip>,
  },
  {
    chave: 'medidas',
    rotulo: 'Medidas',
    render: (r) => {
      if (r.medidas.length === 0) return <StatusChip tom="neutro">Não separadas</StatusChip>;
      const validadas = r.medidas.filter((m) => m.status === 1).length;
      return (
        <span style={{ display: 'inline-flex', gap: 4, flexWrap: 'wrap' }}>
          <StatusChip tom="ok">{validadas} validadas</StatusChip>
          {pendentes(r) > 0 && <StatusChip tom="atencao">{pendentes(r)} pendentes</StatusChip>}
        </span>
      );
    },
  },
  { chave: 'controlesExistentes', rotulo: 'Controles existentes', render: (r) => r.controlesExistentes ?? '' },
  { chave: 'controlesAdicionais', rotulo: 'Controles adicionais', render: (r) => r.controlesAdicionais ?? '' },
  { chave: 'status', rotulo: 'Status', render: (r) => statusControleRiscoLabel[r.status] },
];

// Onda 2 Task 8 (camada ui/): caracterização das atividades / inventário e classificação de riscos
// (§16) — leitura agregada dos registros de Atividade/Risco já cadastrados para a Obra do PGR. Uma
// DataTable por atividade. Ordem de Serviço (20/10/2026): cada risco abre um painel com a confirmação
// da avaliação e as medidas por fase; só medida validada vai para a OS.
export function InventarioTab({ atividades, aoAlterar }: { atividades: AtividadeCaracterizada[]; aoAlterar: () => Promise<void> }) {
  const [aberto, setAberto] = useState<string | null>(null);
  const [soPendencias, setSoPendencias] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [progresso, setProgresso] = useState<string | null>(null);

  if (atividades.length === 0) {
    return <EstadoVazio titulo="Nenhuma atividade com avaliação de risco cadastrada para a obra deste PGR." />;
  }

  const riscos = atividades.flatMap((a) => a.riscos);
  const avaliados = riscos.filter((r) => r.avaliadoEmUtc).length;
  const medidasPendentes = riscos.reduce((t, r) => t + pendentes(r), 0);
  const semMedidas = riscos.filter((r) => r.medidas.length === 0);

  // Uma chamada por risco, em sequência: a IA leva alguns segundos por risco e o Azure limita a taxa.
  async function separarTodas() {
    setErro(null);
    const falhas: string[] = [];
    for (let i = 0; i < semMedidas.length; i++) {
      setProgresso(`Separando medidas com IA: ${i + 1} de ${semMedidas.length}…`);
      try {
        await api.riscos.separarMedidasComIa(semMedidas[i].riscoId);
      } catch {
        falhas.push(semMedidas[i].perigoNome);
      }
    }
    setProgresso(null);
    if (falhas.length > 0) setErro(`Não foi possível separar: ${falhas.join(', ')}. Tente de novo pelo painel do risco.`);
    await aoAlterar();
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div style={{ display: 'flex', gap: 12, justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <StatusChip tom="ok">{avaliados} riscos avaliados</StatusChip>
          {riscos.length - avaliados > 0 && <StatusChip tom="alerta">{riscos.length - avaliados} sem avaliação</StatusChip>}
          {medidasPendentes > 0 && <StatusChip tom="atencao">{medidasPendentes} medidas aguardando validação</StatusChip>}
        </div>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
          <Checkbox
            label="Mostrar só o que tem pendência"
            checked={soPendencias}
            onChange={(_, d) => setSoPendencias(!!d.checked)}
          />
          {semMedidas.length > 0 && (
            <Button disabled={progresso !== null} onClick={() => void separarTodas()}>
              Separar medidas com IA ({semMedidas.length})
            </Button>
          )}
        </div>
      </div>

      {progresso && <FeedbackInline tom="info">{progresso}</FeedbackInline>}
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {atividades.map((atividade) => {
        const linhas = soPendencias ? atividade.riscos.filter(temPendencia) : atividade.riscos;
        if (soPendencias && linhas.length === 0) return null;
        return (
          <Card key={atividade.atividadeId} titulo={atividade.atividadeNome}>
            <DataTable
              aria-label={`Riscos da atividade ${atividade.atividadeNome}`}
              colunas={colunasRiscos}
              linhas={linhas}
              chaveLinha={(r) => r.riscoId}
              aoClicarLinha={(r) => setAberto((atual) => (atual === r.riscoId ? null : r.riscoId))}
              expansivel={{
                aberta: (r) => r.riscoId === aberto,
                render: (r) => <PainelRisco risco={r} aoAlterar={aoAlterar} />,
              }}
              vazio={{ titulo: 'Nenhum risco classificado para esta atividade.' }}
            />
          </Card>
        );
      })}
    </div>
  );
}

function PainelRisco({ risco, aoAlterar }: { risco: RiscoClassificado; aoAlterar: () => Promise<void> }) {
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [novaFase, setNovaFase] = useState<1 | 2 | 3>(1);
  const [novoTexto, setNovoTexto] = useState('');
  const [adicionando, setAdicionando] = useState(false);

  async function executar(acao: () => Promise<unknown>) {
    setOcupado(true);
    setErro(null);
    try {
      await acao();
      await aoAlterar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível salvar.');
    } finally {
      setOcupado(false);
    }
  }

  const textoPgr = [risco.controlesExistentes, risco.controlesAdicionais].filter(Boolean).join('\n');

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 12, padding: '4px 0' }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card densidade="compacta" titulo="Avaliação do risco">
        <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'center' }}>
          <Text>Probabilidade: <b>{risco.probabilidade}</b></Text>
          <Text>Severidade: <b>{risco.severidade}</b></Text>
          <StatusChip tom={tomPorNivel[risco.nivelRisco] ?? 'neutro'}>{nivelRiscoLabel[risco.nivelRisco]}</StatusChip>
          {risco.consequencia && <Text>Consequência: {risco.consequencia}</Text>}
          {risco.avaliadoEmUtc ? (
            <StatusChip tom="ok">
              Avaliado por {risco.avaliadoPorNome ?? '—'} em {dataCurta(risco.avaliadoEmUtc)}
            </StatusChip>
          ) : (
            <Button appearance="primary" disabled={ocupado} onClick={() => void executar(() => api.riscos.confirmarAvaliacao(risco.riscoId))}>
              Confirmar avaliação
            </Button>
          )}
        </div>
        {!risco.avaliadoEmUtc && (
          <Text size={200} style={{ display: 'block', marginTop: 6 }}>
            Confirma que você revisou a probabilidade, a severidade e a consequência deste risco. Fica registrado quem confirmou e quando.
          </Text>
        )}
      </Card>

      <Card densidade="compacta" titulo="Medidas de prevenção">
        {risco.medidas.length === 0 ? (
          <div style={{ display: 'flex', gap: 12, alignItems: 'center', flexWrap: 'wrap' }}>
            <Text>As medidas deste risco ainda não foram separadas por fase.</Text>
            <Button
              appearance="primary"
              disabled={ocupado}
              onClick={() => void executar(() => api.riscos.separarMedidasComIa(risco.riscoId))}
            >
              {ocupado ? 'Separando…' : 'Separar em antes / durante / após'}
            </Button>
          </div>
        ) : (
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: 12 }}>
            {fases.map((f) => (
              <div key={f.valor}>
                <Text weight="semibold" style={{ display: 'block', marginBottom: 6 }}>{f.rotulo}</Text>
                {risco.medidas.filter((m) => m.fase === f.valor).map((m) => (
                  <CartaoMedida key={m.id} medida={m} ocupado={ocupado} executar={executar} />
                ))}
              </div>
            ))}
          </div>
        )}

        <div style={{ marginTop: 12 }}>
          {adicionando ? (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxWidth: 560 }}>
              <Select value={String(novaFase)} onChange={(_, d) => setNovaFase(Number(d.value) as 1 | 2 | 3)} aria-label="Fase">
                {fases.map((f) => <option key={f.valor} value={f.valor}>{f.rotulo}</option>)}
              </Select>
              <Textarea value={novoTexto} onChange={(_, d) => setNovoTexto(d.value)} placeholder="Descreva a medida no imperativo…" maxLength={1000} />
              <div style={{ display: 'flex', gap: 8 }}>
                <Button
                  appearance="primary"
                  disabled={ocupado || novoTexto.trim().length === 0}
                  onClick={() => void executar(async () => {
                    await api.riscos.adicionarMedida(risco.riscoId, novaFase, novoTexto.trim());
                    setNovoTexto('');
                    setAdicionando(false);
                  })}
                >
                  Salvar medida
                </Button>
                <Button onClick={() => setAdicionando(false)}>Cancelar</Button>
              </div>
            </div>
          ) : (
            <Button onClick={() => setAdicionando(true)}>+ Adicionar medida</Button>
          )}
        </div>
        <Text size={200} style={{ display: 'block', marginTop: 8 }}>
          Só as medidas validadas vão para a Ordem de Serviço. Uma sugestão da IA que ninguém aceitou deixa a OS como minuta.
        </Text>
      </Card>

      {textoPgr && (
        <Card densidade="compacta" titulo="Texto original do PGR">
          <Text style={{ whiteSpace: 'pre-line' }}>{textoPgr}</Text>
          <Text size={200} style={{ display: 'block', marginTop: 6 }}>
            O texto original fica guardado e não é apagado. O que estava escrito aqui entra como validado; o que a IA acrescentar entra como sugestão.
          </Text>
        </Card>
      )}
    </div>
  );
}

function CartaoMedida({ medida, ocupado, executar }: {
  medida: MedidaPrevencao;
  ocupado: boolean;
  executar: (acao: () => Promise<unknown>) => Promise<void>;
}) {
  const [editando, setEditando] = useState(false);
  const [texto, setTexto] = useState(medida.texto);
  const pendente = medida.status === 2;
  const origem = medida.origem === 1 ? 'Texto do PGR' : medida.origem === 3 ? 'Escrita pela SST' : 'Sugestão da IA';

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
        <Textarea value={texto} onChange={(_, d) => setTexto(d.value)} maxLength={1000} style={{ width: '100%' }} />
      ) : (
        <Text>{medida.texto}</Text>
      )}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 6, marginTop: 6, flexWrap: 'wrap' }}>
        {pendente ? <StatusChip tom="atencao">Sugestão da IA</StatusChip> : <StatusChip tom="ok">Validada</StatusChip>}
        {pendente ? (
          <span style={{ display: 'inline-flex', gap: 4 }}>
            <Button
              size="small"
              appearance="primary"
              disabled={ocupado}
              onClick={() => void executar(() => api.riscos.decidirMedida(medida.id, true, editando ? texto : undefined))}
            >
              Aceitar
            </Button>
            {!editando && <Button size="small" disabled={ocupado} onClick={() => setEditando(true)}>Editar</Button>}
            <Button size="small" disabled={ocupado} onClick={() => void executar(() => api.riscos.decidirMedida(medida.id, false))}>
              Descartar
            </Button>
          </span>
        ) : (
          <Text size={100}>
            {medida.validadoPorNome ?? origem}{medida.validadoEmUtc ? ` · ${dataCurta(medida.validadoEmUtc)}` : ''}
          </Text>
        )}
      </div>
    </div>
  );
}
