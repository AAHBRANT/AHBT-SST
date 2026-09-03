import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, type EventoSipatDetalhe } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function atividadeVazia() {
  return { data: '', horario: '', temaPalestra: '', palestrante: '' };
}

export function EventoSipatDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
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

  if (!id) return <Text>Evento SIPAT não encontrado.</Text>;

  return (
    <div>
      <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => navigate('/operacao/cipa')} style={{ marginBottom: 12 }}>
        Voltar para CIPA
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      {!detalhe ? (
        <Text>Carregando...</Text>
      ) : (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <Text size={500} weight="semibold" style={{ display: 'block', marginBottom: 8 }}>
              SIPAT {detalhe.evento.anoReferencia} — {detalhe.evento.obraNome}
            </Text>
            <Text size={200}>
              {detalhe.evento.dataInicio?.slice(0, 10)} a {detalhe.evento.dataFim?.slice(0, 10)}
              {detalhe.evento.tema ? ` · ${detalhe.evento.tema}` : ''}
            </Text>
            {detalhe.evento.programacao && (
              <Text size={200} style={{ display: 'block', marginTop: 8 }}>
                {detalhe.evento.programacao}
              </Text>
            )}
          </div>

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Nova atividade/palestra</Text>
            </div>
            <div className={estilos.form}>
              <Field label="Data" required>
                <CampoData value={novaAtividade.data} onChange={(_, d) => setNovaAtividade({ ...novaAtividade, data: d.value })} />
              </Field>
              <Field label="Horário">
                <Input value={novaAtividade.horario} onChange={(_, d) => setNovaAtividade({ ...novaAtividade, horario: d.value })} />
              </Field>
              <Field label="Tema da palestra" required>
                <Input
                  value={novaAtividade.temaPalestra}
                  onChange={(_, d) => setNovaAtividade({ ...novaAtividade, temaPalestra: d.value })}
                />
              </Field>
              <Field label="Palestrante">
                <Input value={novaAtividade.palestrante} onChange={(_, d) => setNovaAtividade({ ...novaAtividade, palestrante: d.value })} />
              </Field>
            </div>
            <div className={estilos.formActions}>
              <Button appearance="primary" onClick={criarAtividade} disabled={salvando}>
                Adicionar atividade
              </Button>
            </div>
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Programação</Text>
            </div>
            <Table noNativeElements>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Data</TableHeaderCell>
                  <TableHeaderCell>Horário</TableHeaderCell>
                  <TableHeaderCell>Tema</TableHeaderCell>
                  <TableHeaderCell>Palestrante</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {detalhe.atividades.map((a) => (
                  <TableRow key={a.id}>
                    <TableCell>{a.data?.slice(0, 10)}</TableCell>
                    <TableCell>{a.horario ?? '—'}</TableCell>
                    <TableCell>{a.temaPalestra}</TableCell>
                    <TableCell>{a.palestrante ?? '—'}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </>
      )}
    </div>
  );
}
