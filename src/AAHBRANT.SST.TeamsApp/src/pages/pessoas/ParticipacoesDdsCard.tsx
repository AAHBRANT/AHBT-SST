import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, FeedbackInline, StatusChip, type Coluna } from '@ui';
import { api, TipoFotoParticipante, type ParticipacaoDds } from '../../lib/api';

// Lista de DDS do perfil (aba Treinamentos): complementa o anel de assiduidade mostrando cada DDS da
// obra desde a admissão — tema, responsável e como a presença foi registrada, ou a falta.
type Filtro = 'todos' | 'presente' | 'ausente';

const rotuloMetodo: Record<number, string> = {
  [TipoFotoParticipante.Facial]: 'Facial',
  [TipoFotoParticipante.Biometria]: 'Digital',
  [TipoFotoParticipante.Pessoa]: 'Foto',
  [TipoFotoParticipante.DocumentoAssinado]: 'Lista assinada',
};

const secundario = { fontSize: 12, color: 'var(--sst-color-neutral-medium)' } as const;

function dataBr(iso?: string | null) {
  return iso ? new Date(iso).toLocaleDateString('pt-BR') : '—';
}

function horaBr(iso?: string | null) {
  return iso ? new Date(iso).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }) : '';
}

export function ParticipacoesDdsCard({ trabalhadorId }: { trabalhadorId: string }) {
  const navigate = useNavigate();
  const [lista, setLista] = useState<ParticipacaoDds[] | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [filtro, setFiltro] = useState<Filtro>('todos');

  useEffect(() => {
    let ativo = true;
    setLista(null);
    api.trabalhadores
      .listarParticipacoesDds(trabalhadorId)
      .then((r) => ativo && setLista(r))
      .catch((e) => ativo && setErro(e instanceof Error ? e.message : 'Falha ao carregar os DDS.'));
    return () => {
      ativo = false;
    };
  }, [trabalhadorId]);

  const visiveis = useMemo(
    () =>
      (lista ?? []).filter((d) =>
        filtro === 'todos' ? true : filtro === 'presente' ? d.presente : !d.presente && !d.semExpediente,
      ),
    [lista, filtro],
  );

  const colunas: Coluna<ParticipacaoDds>[] = [
    { chave: 'data', rotulo: 'Data', render: (d) => dataBr(d.data) },
    { chave: 'documento', rotulo: 'Nº doc.', render: (d) => d.numeroDocumento ?? '—' },
    {
      chave: 'tema',
      rotulo: 'Tema',
      render: (d) => (
        <div>
          <div>{d.tema}</div>
          <div style={secundario}>
            {d.semExpediente
              ? d.motivoSemExpediente ?? 'Sem expediente'
              : `${d.quantidadeAtividades} atividade(s) · ${d.quantidadeParticipantes} participante(s)`}
          </div>
        </div>
      ),
    },
    { chave: 'responsavel', rotulo: 'Responsável', render: (d) => d.responsavelNome ?? '—' },
    {
      chave: 'presenca',
      rotulo: 'Presença',
      render: (d) => {
        if (d.semExpediente) return <StatusChip tom="neutro">Não houve</StatusChip>;
        if (!d.presente) return <StatusChip tom="alerta">Ausente</StatusChip>;
        const metodo = d.metodoPresenca ? rotuloMetodo[d.metodoPresenca] ?? 'Registrada' : 'Registrada';
        const score =
          d.metodoPresenca === TipoFotoParticipante.Facial && d.scoreConfianca != null
            ? ` · ${d.scoreConfianca.toFixed(2).replace('.', ',')}`
            : '';
        return (
          <div>
            <StatusChip tom="ok">{`${metodo}${score}`}</StatusChip>
            {d.presencaRegistradaEm && <div style={secundario}>às {horaBr(d.presencaRegistradaEm)}</div>}
          </div>
        );
      },
    },
  ];

  const botoesFiltro: { valor: Filtro; rotulo: string }[] = [
    { valor: 'todos', rotulo: 'Todos' },
    { valor: 'presente', rotulo: 'Presente' },
    { valor: 'ausente', rotulo: 'Ausente' },
  ];

  return (
    <Card
      titulo="Participações em DDS"
      subtitulo="Cada DDS da obra desde a admissão, com a forma de registro da presença"
      acoes={
        <div style={{ display: 'flex', gap: 4 }}>
          {botoesFiltro.map((b) => (
            <Button
              key={b.valor}
              size="small"
              appearance={filtro === b.valor ? 'primary' : 'secondary'}
              onClick={() => setFiltro(b.valor)}
            >
              {b.rotulo}
            </Button>
          ))}
        </div>
      }
    >
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <DataTable
        linhas={visiveis}
        colunas={colunas}
        densidade="compacta"
        chaveLinha={(d) => d.ddsId}
        carregando={lista === null && !erro}
        vazio={{ titulo: 'Nenhum DDS neste filtro.' }}
        aoClicarLinha={(d) => navigate(`/prevencao/dds/dia/${d.ddsId}`)}
      />
    </Card>
  );
}
