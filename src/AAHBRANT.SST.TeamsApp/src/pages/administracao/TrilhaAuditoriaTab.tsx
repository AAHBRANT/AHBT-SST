import { useEffect, useState } from 'react';
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
import { Filter24Regular } from '@fluentui/react-icons';
import { api, type TrilhaAuditoria } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

export function TrilhaAuditoriaTab() {
  const estilos = usePageStyles();
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
  }, []);

  const detalhe = registros.find((r) => r.id === detalheId) ?? null;

  return (
    <div>
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Trilha de auditoria</Text>
        </div>

        {erro && <Text className={estilos.erro}>{erro}</Text>}

        <div className={estilos.form}>
          <Field label="Tipo de entidade">
            <Input value={entidadeTipo} onChange={(_, d) => setEntidadeTipo(d.value)} placeholder="Ex.: Usuario" />
          </Field>
          <Field label="Data início">
            <CampoData value={dataInicio} onChange={(_, d) => setDataInicio(d.value)} />
          </Field>
          <Field label="Data fim">
            <CampoData value={dataFim} onChange={(_, d) => setDataFim(d.value)} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Filter24Regular />} onClick={carregar} disabled={carregando}>
            Filtrar
          </Button>
        </div>

        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Data/Hora</TableHeaderCell>
              <TableHeaderCell>Usuário</TableHeaderCell>
              <TableHeaderCell>Ação</TableHeaderCell>
              <TableHeaderCell>Entidade</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {registros.map((registro) => (
              <TableRow
                key={registro.id}
                onClick={() => setDetalheId(registro.id)}
                style={{ cursor: 'pointer', fontWeight: registro.id === detalheId ? 600 : 400 }}
              >
                <TableCell>{new Date(registro.timestamp).toLocaleString('pt-BR')}</TableCell>
                <TableCell>{registro.usuarioNome ?? '—'}</TableCell>
                <TableCell>{registro.acao}</TableCell>
                <TableCell>
                  {registro.entidadeTipo} ({registro.entidadeId})
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {detalhe && (
        <div className={estilos.card}>
          <div className={estilos.toolbar}>
            <Text weight="semibold">
              Detalhe — {detalhe.acao} em {detalhe.entidadeTipo}
            </Text>
          </div>
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
        </div>
      )}
    </div>
  );
}
