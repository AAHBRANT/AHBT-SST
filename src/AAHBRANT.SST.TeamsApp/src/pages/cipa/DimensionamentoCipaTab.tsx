import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  Textarea,
} from '@fluentui/react-components';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, nivelRiscoLabel, type DimensionamentoCipa, type NovoDimensionamentoCipa, type Obra } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

function vazio(): NovoDimensionamentoCipa {
  return { obraId: '', cnae: '', grauRisco: 1, numeroFuncionarios: 0, numeroTitulares: 0, numeroSuplentes: 0, observacoes: '' };
}

// Dimensionamento CIPA: número de titulares/suplentes é sempre informado manualmente por quem
// cadastra — este sistema não calcula automaticamente o Quadro I da NR-5 (deve ser validado por
// técnico/engenheiro de segurança do trabalho habilitado). Ver disclosure completo em Cipa.cs.
export function DimensionamentoCipaTab() {
  const estilos = usePageStyles();
  const [lista, setLista] = useState<DimensionamentoCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovoDimensionamentoCipa>(vazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [listaDim, listaObras] = await Promise.all([api.cipa.dimensionamento.listar(), api.obras.listar()]);
      setLista(listaDim);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar dimensionamentos.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  async function criar() {
    if (!novo.obraId || !novo.cnae.trim() || novo.numeroFuncionarios <= 0) {
      setErro('Preencha obra, CNAE e número de funcionários.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.cipa.dimensionamento.criar({ ...novo, observacoes: novo.observacoes || null });
      setNovo(vazio());
      await carregar();
      sucessoToast('Dimensionamento criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar dimensionamento.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este dimensionamento? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.dimensionamento.excluir(id);
      await carregar();
      sucessoToast('Dimensionamento excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir dimensionamento.');
    }
  }

  return (
    <div>
      {dialogElement}
      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Novo dimensionamento</Text>
        </div>
        {erro && <Text className={estilos.erro}>{erro}</Text>}
        <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
          O número de titulares/suplentes é definido manualmente por quem cadastra, conforme o Quadro
          I da NR-5 para o CNAE e grau de risco informados. Recomenda-se validação por técnico ou
          engenheiro de segurança do trabalho habilitado.
        </Text>
        <div className={estilos.form}>
          <Field label="Obra" required>
            <Select value={novo.obraId} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
              <option value="">Selecione</option>
              {obras.map((obra) => (
                <option key={obra.id} value={obra.id}>
                  {obra.nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="CNAE" required>
            <Input value={novo.cnae} onChange={(_, d) => setNovo({ ...novo, cnae: d.value })} />
          </Field>
          <Field label="Grau de risco">
            <Select value={String(novo.grauRisco)} onChange={(_, d) => setNovo({ ...novo, grauRisco: Number(d.value) })}>
              {Object.entries(nivelRiscoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Número de funcionários" required>
            <Input
              type="number"
              value={String(novo.numeroFuncionarios)}
              onChange={(_, d) => setNovo({ ...novo, numeroFuncionarios: Number(d.value) })}
            />
          </Field>
          <Field label="Titulares" required>
            <Input
              type="number"
              value={String(novo.numeroTitulares)}
              onChange={(_, d) => setNovo({ ...novo, numeroTitulares: Number(d.value) })}
            />
          </Field>
          <Field label="Suplentes" required>
            <Input
              type="number"
              value={String(novo.numeroSuplentes)}
              onChange={(_, d) => setNovo({ ...novo, numeroSuplentes: Number(d.value) })}
            />
          </Field>
          <Field label="Observações">
            <Textarea value={novo.observacoes ?? ''} onChange={(_, d) => setNovo({ ...novo, observacoes: d.value })} />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
            Adicionar dimensionamento
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Dimensionamentos cadastrados</Text>
        </div>
        {carregandoLista ? (
          <ListaCarregando />
        ) : lista.length === 0 ? (
          <EstadoVazio mensagem="Nenhum dimensionamento cadastrado ainda." />
        ) : (
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>CNAE</TableHeaderCell>
              <TableHeaderCell>Grau de risco</TableHeaderCell>
              <TableHeaderCell>Funcionários</TableHeaderCell>
              <TableHeaderCell>Titulares</TableHeaderCell>
              <TableHeaderCell>Suplentes</TableHeaderCell>
              <TableHeaderCell>Data do cálculo</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {lista.map((d) => (
              <TableRow key={d.id}>
                <TableCell>{nomeObra(d.obraId)}</TableCell>
                <TableCell>{d.cnae}</TableCell>
                <TableCell>{nivelRiscoLabel[d.grauRisco]}</TableCell>
                <TableCell>{d.numeroFuncionarios}</TableCell>
                <TableCell>{d.numeroTitulares}</TableCell>
                <TableCell>{d.numeroSuplentes}</TableCell>
                <TableCell>{d.dataCalculo?.slice(0, 10)}</TableCell>
                <TableCell>
                  <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(d.id)} aria-label="Excluir" />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        )}
      </div>
    </div>
  );
}
