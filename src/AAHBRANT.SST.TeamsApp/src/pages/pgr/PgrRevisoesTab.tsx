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
import { Add24Regular } from '@fluentui/react-icons';
import { api, type NovaPgrRevisao, type PgrRevisao } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

function revisaoVazia(pgrId: string): NovaPgrRevisao {
  return { pgrId, dataRevisao: '', motivo: '', responsavelUsuarioId: null };
}

// Revisão do PGR (§16) é append-only — sem edição/exclusão, só registro incremental.
export function PgrRevisoesTab({ pgrId }: { pgrId: string }) {
  const estilos = usePageStyles();
  const [revisoes, setRevisoes] = useState<PgrRevisao[]>([]);
  const [novaRevisao, setNovaRevisao] = useState<NovaPgrRevisao>(() => revisaoVazia(pgrId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setRevisoes(await api.pgrRevisoes.listar(pgrId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar revisões.');
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
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Histórico de revisões</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Data da revisão">
          <CampoData
            value={novaRevisao.dataRevisao}
            onChange={(_, d) => setNovaRevisao({ ...novaRevisao, dataRevisao: d.value })}
          />
        </Field>
        <Field label="Motivo">
          <Input
            value={novaRevisao.motivo}
            onChange={(_, d) => setNovaRevisao({ ...novaRevisao, motivo: d.value })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Registrar revisão
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nº</TableHeaderCell>
            <TableHeaderCell>Data</TableHeaderCell>
            <TableHeaderCell>Motivo</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {revisoes.map((revisao) => (
            <TableRow key={revisao.id}>
              <TableCell>{revisao.numeroRevisao}</TableCell>
              <TableCell>{revisao.dataRevisao?.slice(0, 10)}</TableCell>
              <TableCell>{revisao.motivo}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
