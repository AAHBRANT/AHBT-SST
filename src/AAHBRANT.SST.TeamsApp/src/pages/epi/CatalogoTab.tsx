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
import { Add24Regular, Delete24Regular, Save24Regular } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type NovoCatalogoEpi } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

const epiVazio: NovoCatalogoEpi = {
  nome: '',
  fabricante: '',
  certificadoAprovacaoNumero: '',
  certificadoAprovacaoValidade: '',
  vidaUtilEmMeses: 12,
};

// Catálogo de EPI (item + estoque) do módulo dedicado /epi — antes vivia como aba dentro de
// Pessoas (CatalogoEpiTab), mas o catálogo não é dado de uma pessoa, e sim operacional/compartilhado
// entre entregas; com o módulo próprio de EPI aprovado pelo usuário, a gestão de catálogo/estoque
// passou para cá por inteiro.
export function CatalogoTab() {
  const estilos = usePageStyles();
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [novoEpi, setNovoEpi] = useState<NovoCatalogoEpi>(epiVazio);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoEpi | null>(null);
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

  function iniciarEdicao(epi: CatalogoEpi) {
    setEdicaoId(epi.id);
    setEdicao({ ...epi });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosEpi.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar EPI de catálogo.');
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
        <Text weight="semibold">Catálogo de EPIs</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Nome">
          <Input value={novoEpi.nome} onChange={(_, d) => setNovoEpi({ ...novoEpi, nome: d.value })} />
        </Field>
        <Field label="Fabricante">
          <Input
            value={novoEpi.fabricante ?? ''}
            onChange={(_, d) => setNovoEpi({ ...novoEpi, fabricante: d.value })}
          />
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
            <TableHeaderCell>Fabricante</TableHeaderCell>
            <TableHeaderCell>Nº do CA</TableHeaderCell>
            <TableHeaderCell>Validade do CA</TableHeaderCell>
            <TableHeaderCell>Vida útil (meses)</TableHeaderCell>
            <TableHeaderCell>Estoque total</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {epis.map((epi) =>
            edicaoId === epi.id && edicao ? (
              <TableRow key={epi.id}>
                <TableCell>
                  <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
                </TableCell>
                <TableCell>
                  <Input
                    value={edicao.fabricante ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, fabricante: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <Input
                    value={edicao.certificadoAprovacaoNumero ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoNumero: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <Input
                    type="date"
                    value={edicao.certificadoAprovacaoValidade?.slice(0, 10) ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, certificadoAprovacaoValidade: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <Input
                    type="number"
                    value={String(edicao.vidaUtilEmMeses)}
                    onChange={(_, d) => setEdicao({ ...edicao, vidaUtilEmMeses: Number(d.value) })}
                  />
                </TableCell>
                <TableCell>{edicao.saldoTotal}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Save24Regular />}
                    onClick={salvarEdicao}
                    disabled={carregando}
                    aria-label="Salvar"
                  />
                </TableCell>
              </TableRow>
            ) : (
              <TableRow key={epi.id} onClick={() => iniciarEdicao(epi)} style={{ cursor: 'pointer' }}>
                <TableCell>{epi.nome}</TableCell>
                <TableCell>{epi.fabricante}</TableCell>
                <TableCell>{epi.certificadoAprovacaoNumero}</TableCell>
                <TableCell>{epi.certificadoAprovacaoValidade?.slice(0, 10)}</TableCell>
                <TableCell>{epi.vidaUtilEmMeses}</TableCell>
                <TableCell>{epi.saldoTotal}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(e) => {
                      e.stopPropagation();
                      excluir(epi.id);
                    }}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ),
          )}
        </TableBody>
      </Table>
      <Text size={200} style={{ display: 'block', marginTop: 8 }}>
        Clique em uma linha para editar os dados do EPI. O estoque é controlado por Obra na aba
        Estoque.
      </Text>
    </div>
  );
}
