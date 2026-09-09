import { useEffect, useState } from 'react';
import {
  Button,
  CampoData,
  Campo,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  type Coluna,
} from '@ui';
import { Add24Regular } from '@fluentui/react-icons';
import { api, type NovaPgrRevisao, type PgrRevisao } from '../../lib/api';

function revisaoVazia(pgrId: string): NovaPgrRevisao {
  return { pgrId, dataRevisao: '', motivo: '', responsavelUsuarioId: null };
}

const colunas: Coluna<PgrRevisao>[] = [
  { chave: 'numeroRevisao', rotulo: 'Nº' },
  { chave: 'dataRevisao', rotulo: 'Data', render: (r) => r.dataRevisao?.slice(0, 10) ?? '' },
  { chave: 'motivo', rotulo: 'Motivo' },
];

// Onda 2 Task 8 (camada ui/): revisão do PGR (§16) é append-only — sem edição/exclusão, só registro
// incremental (por isso sem `useConfirmar`/`acoesLinha`).
export function PgrRevisoesTab({ pgrId }: { pgrId: string }) {
  const [revisoes, setRevisoes] = useState<PgrRevisao[]>([]);
  const [novaRevisao, setNovaRevisao] = useState<NovaPgrRevisao>(() => revisaoVazia(pgrId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);

  async function carregar() {
    try {
      setErro(null);
      setRevisoes(await api.pgrRevisoes.listar(pgrId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar revisões.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    setNovaRevisao(revisaoVazia(pgrId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pgrId]);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.pgrRevisoes.criar(novaRevisao);
      setNovaRevisao(revisaoVazia(pgrId));
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar revisão.');
    } finally {
      setCarregando(false);
    }
  }

  return (
    <Card titulo="Histórico de revisões">
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <FormSection titulo="Nova revisão" numero={1} primeira>
        <FormGrid>
          <Campo span={3}>
            <Field label="Data da revisão">
              <CampoData
                value={novaRevisao.dataRevisao}
                onChange={(_, d) => setNovaRevisao({ ...novaRevisao, dataRevisao: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Motivo">
              <Input value={novaRevisao.motivo} onChange={(_, d) => setNovaRevisao({ ...novaRevisao, motivo: d.value })} />
            </Field>
          </Campo>
        </FormGrid>
        <FormRodape>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Registrar revisão
          </Button>
        </FormRodape>
      </FormSection>

      <DataTable
        aria-label="Histórico de revisões do PGR"
        colunas={colunas}
        linhas={revisoes}
        chaveLinha={(r) => r.id}
        carregando={carregandoLista}
        vazio={{ titulo: 'Nenhuma revisão registrada ainda.' }}
      />
    </Card>
  );
}
