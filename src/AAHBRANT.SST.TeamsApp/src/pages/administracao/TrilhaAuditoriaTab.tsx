import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  Text,
  type Coluna,
} from '@ui';
import { Filter24Regular } from '@fluentui/react-icons';
import { api, type TrilhaAuditoria } from '../../lib/api';

// Onda 2 Task 17 (camada ui/): arquivo citado no Guia de conversão (seção 4) como referência real de
// `estilos.erro` → `FeedbackInline`. Filtro + tabela vira `FormSection`/`FormGrid`+`DataTable`; o
// realce em negrito da linha selecionada (comparado a `detalheId`) não tem equivalente em `DataTable`
// (sem hook de estilo por linha) — removido de propósito, já que o card de detalhe abaixo já indica
// visualmente qual registro está selecionado (mesma categoria de simplificação visual aceita em
// outras tasks, spec §5.2).
export function TrilhaAuditoriaTab() {
  const [registros, setRegistros] = useState<TrilhaAuditoria[]>([]);
  const [entidadeTipo, setEntidadeTipo] = useState('');
  const [dataInicio, setDataInicio] = useState('');
  const [dataFim, setDataFim] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [detalheId, setDetalheId] = useState<string | null>(null);

  async function carregar() {
    try {
      setCarregando(true);
      setErro(null);
      const dados = await api.trilhaAuditoria.listar({
        entidadeTipo: entidadeTipo || undefined,
        dataInicio: dataInicio || undefined,
        dataFim: dataFim || undefined,
      });
      setRegistros(dados);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar trilha de auditoria.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const detalhe = registros.find((r) => r.id === detalheId) ?? null;

  const colunas: Coluna<TrilhaAuditoria>[] = [
    { chave: 'timestamp', rotulo: 'Data/Hora', render: (r) => new Date(r.timestamp).toLocaleString('pt-BR') },
    { chave: 'usuario', rotulo: 'Usuário', render: (r) => r.usuarioNome ?? '—' },
    { chave: 'acao', rotulo: 'Ação' },
    { chave: 'entidade', rotulo: 'Entidade', render: (r) => `${r.entidadeTipo} (${r.entidadeId})` },
  ];

  return (
    <div>
      <PageHeader titulo="Trilha de auditoria" />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card>
        <FormSection titulo="Filtros" primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Tipo de entidade">
                <Input value={entidadeTipo} onChange={(_, d) => setEntidadeTipo(d.value)} placeholder="Ex.: Usuario" />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data início">
                <CampoData value={dataInicio} onChange={(_, d) => setDataInicio(d.value)} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data fim">
                <CampoData value={dataFim} onChange={(_, d) => setDataFim(d.value)} />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Filter24Regular />} onClick={carregar} disabled={carregando}>
              Filtrar
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Registros de trilha de auditoria"
          colunas={colunas}
          linhas={registros}
          chaveLinha={(r) => r.id}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum registro encontrado para os filtros selecionados.' }}
          aoClicarLinha={(r) => setDetalheId(r.id)}
        />
      </Card>

      {detalhe && (
        <div style={{ marginTop: 16 }}>
          <Card titulo={`Detalhe — ${detalhe.acao} em ${detalhe.entidadeTipo}`}>
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 16 }}>
              <div>
                <Text weight="semibold">Antes</Text>
                <pre style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>
                  {detalhe.dadosAntesJson ? JSON.stringify(JSON.parse(detalhe.dadosAntesJson), null, 2) : '—'}
                </pre>
              </div>
              <div>
                <Text weight="semibold">Depois</Text>
                <pre style={{ whiteSpace: 'pre-wrap', fontSize: 12 }}>
                  {detalhe.dadosDepoisJson ? JSON.stringify(JSON.parse(detalhe.dadosDepoisJson), null, 2) : '—'}
                </pre>
              </div>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
