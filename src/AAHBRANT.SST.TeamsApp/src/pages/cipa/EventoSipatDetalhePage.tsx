import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Button,
  Campo,
  CampoData,
  Card,
  Carregando,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  type Coluna,
} from '@ui';
import { api, type AtividadeSipat, type EventoSipatDetalhe } from '../../lib/api';

function atividadeVazia() {
  return { data: '', horario: '', temaPalestra: '', palestrante: '' };
}

export function EventoSipatDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<EventoSipatDetalhe | null>(null);
  const [novaAtividade, setNovaAtividade] = useState(atividadeVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setDetalhe(await api.cipa.eventosSipat.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar evento SIPAT.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function criarAtividade() {
    if (!id) return;
    if (!novaAtividade.data || !novaAtividade.temaPalestra.trim()) {
      setErro('Preencha data e tema da palestra.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.cipa.eventosSipat.criarAtividade(
        id,
        novaAtividade.data,
        novaAtividade.horario || null,
        novaAtividade.temaPalestra,
        novaAtividade.palestrante || null,
      );
      setNovaAtividade(atividadeVazia());
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar atividade.');
    } finally {
      setSalvando(false);
    }
  }

  if (!id) return <FeedbackInline tom="erro">Evento SIPAT não encontrado.</FeedbackInline>;

  const colunasAtividades: Coluna<AtividadeSipat>[] = [
    { chave: 'data', rotulo: 'Data', render: (a) => a.data?.slice(0, 10) ?? '' },
    { chave: 'horario', rotulo: 'Horário', render: (a) => a.horario ?? '—' },
    { chave: 'temaPalestra', rotulo: 'Tema' },
    { chave: 'palestrante', rotulo: 'Palestrante', render: (a) => a.palestrante ?? '—' },
  ];

  return (
    <div>
      <PageHeader titulo="Evento SIPAT" voltarPara="/operacao/cipa" rotuloVoltar="Voltar para CIPA" />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {!detalhe ? (
        <Carregando variante="detalhe" linhas={8} />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card
            densidade="compacta"
            titulo={`SIPAT ${detalhe.evento.anoReferencia} — ${detalhe.evento.obraNome}`}
            subtitulo={
              <>
                {detalhe.evento.dataInicio?.slice(0, 10)} a {detalhe.evento.dataFim?.slice(0, 10)}
                {detalhe.evento.tema ? ` · ${detalhe.evento.tema}` : ''}
              </>
            }
          >
            {detalhe.evento.programacao && <div>{detalhe.evento.programacao}</div>}
          </Card>

          <Card titulo="Nova atividade/palestra">
            <FormSection titulo="Dados da Atividade" primeira>
              <FormGrid>
                <Campo span={2}>
                  <Field label="Data" required>
                    <CampoData value={novaAtividade.data} onChange={(_, d) => setNovaAtividade({ ...novaAtividade, data: d.value })} />
                  </Field>
                </Campo>
                <Campo span={2}>
                  <Field label="Horário">
                    <Input value={novaAtividade.horario} onChange={(_, d) => setNovaAtividade({ ...novaAtividade, horario: d.value })} />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Tema da palestra" required>
                    <Input
                      value={novaAtividade.temaPalestra}
                      onChange={(_, d) => setNovaAtividade({ ...novaAtividade, temaPalestra: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Palestrante">
                    <Input value={novaAtividade.palestrante} onChange={(_, d) => setNovaAtividade({ ...novaAtividade, palestrante: d.value })} />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button appearance="primary" onClick={criarAtividade} disabled={salvando}>
                  Adicionar atividade
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Programação">
            <DataTable
              aria-label="Programação do evento SIPAT"
              colunas={colunasAtividades}
              linhas={detalhe.atividades}
              chaveLinha={(a) => a.id}
              vazio={{ titulo: 'Nenhuma atividade cadastrada ainda.' }}
            />
          </Card>
        </div>
      )}
    </div>
  );
}
