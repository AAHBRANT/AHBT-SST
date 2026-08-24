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
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type NovoCatalogoEpi } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const epiVazio: NovoCatalogoEpi = {
  nome: '',
  certificadoAprovacaoNumero: '',
  certificadoAprovacaoValidade: '',
  vidaUtilEmMeses: 12,
};

export function CatalogoEpiTab() {
  const estilos = usePageStyles();
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [novoEpi, setNovoEpi] = useState<NovoCatalogoEpi>(epiVazio);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      setEpis(await api.catalogosEpi.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de EPI.');
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosEpi.criar(novoEpi);
      setNovoEpi(epiVazio);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar EPI de catálogo.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    try {
      await api.catalogosEpi.excluir(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir EPI de catálogo.');
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">EPIs (catálogo)</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={novoEpi.nome} onChange={(_, d) => setNovoEpi({ ...novoEpi, nome: d.value })} />
        </Field>
        <Field label="Nº do CA">
          <Input
            value={novoEpi.certificadoAprovacaoNumero ?? ''}
            onChange={(_, d) => setNovoEpi({ ...novoEpi, certificadoAprovacaoNumero: d.value })}
          />
        </Field>
        <Field label="Validade do CA">
          <Input
            type="date"
            value={novoEpi.certificadoAprovacaoValidade ?? ''}
            onChange={(_, d) => setNovoEpi({ ...novoEpi, certificadoAprovacaoValidade: d.value })}
          />
        </Field>
        <Field label="Vida útil (meses)">
          <Input
            type="number"
            value={String(novoEpi.vidaUtilEmMeses)}
            onChange={(_, d) => setNovoEpi({ ...novoEpi, vidaUtilEmMeses: Number(d.value) })}
          />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar EPI
        </Button>
      </div>

      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Nº do CA</TableHeaderCell>
            <TableHeaderCell>Validade do CA</TableHeaderCell>
            <TableHeaderCell>Vida útil (meses)</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {epis.map((epi) => (
            <TableRow key={epi.id}>
              <TableCell>{epi.nome}</TableCell>
              <TableCell>{epi.certificadoAprovacaoNumero}</TableCell>
              <TableCell>{epi.certificadoAprovacaoValidade?.slice(0, 10)}</TableCell>
              <TableCell>{epi.vidaUtilEmMeses}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(epi.id)}
                  aria-label="Excluir"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
