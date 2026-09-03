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
} from '@fluentui/react-components';
import { AddCircle24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type NovoRegistroHhtMensal, type Obra, type RegistroHhtMensal } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const nomesMes = [
  'Janeiro', 'Fevereiro', 'Março', 'Abril', 'Maio', 'Junho',
  'Julho', 'Agosto', 'Setembro', 'Outubro', 'Novembro', 'Dezembro',
];

function novoInicial(): NovoRegistroHhtMensal {
  const agora = new Date();
  return { obraId: '', ano: agora.getFullYear(), mes: agora.getMonth() + 1, horasHomemTrabalhadas: 0 };
}

export function HhtMensalTab({ obras }: { obras: Obra[] }) {
  const estilos = usePageStyles();
  const [registros, setRegistros] = useState<RegistroHhtMensal[]>([]);
  const [novo, setNovo] = useState<NovoRegistroHhtMensal>(novoInicial());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setRegistros(await api.registrosHht.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar registros de HHT.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!novo.obraId) {
      setErro('Selecione a obra.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.registrosHht.criar(novo);
      setNovo(novoInicial());
      await carregar();
      sucessoToast('HHT lançado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar HHT.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este registro de HHT? Essa ação não pode ser desfeita.'))) return;
    try {
      setErro(null);
      await api.registrosHht.excluir(id);
      await carregar();
      sucessoToast('Registro de HHT excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir registro.');
    }
  }

  return (
    <div>
      {dialogElement}
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Lançar HHT do mês</Text>
        </div>
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
          <Field label="Ano" required>
            <Input
              type="number"
              value={String(novo.ano)}
              onChange={(_, d) => setNovo({ ...novo, ano: Number(d.value) || novo.ano })}
            />
          </Field>
          <Field label="Mês" required>
            <Select value={String(novo.mes)} onChange={(_, d) => setNovo({ ...novo, mes: Number(d.value) })}>
              {nomesMes.map((nome, indice) => (
                <option key={nome} value={indice + 1}>
                  {nome}
                </option>
              ))}
            </Select>
          </Field>
          <Field label="Horas-Homem Trabalhadas (HHT)" required>
            <Input
              type="number"
              min={0}
              value={String(novo.horasHomemTrabalhadas)}
              onChange={(_, d) => setNovo({ ...novo, horasHomemTrabalhadas: Number(d.value) || 0 })}
            />
          </Field>
        </div>
        <div className={estilos.formActions}>
          <Button appearance="primary" icon={<AddCircle24Regular />} onClick={criar} disabled={carregando}>
            Lançar
          </Button>
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Histórico de HHT por obra</Text>
        </div>
        {carregandoLista ? (
          <ListaCarregando />
        ) : registros.length === 0 ? (
          <EstadoVazio mensagem="Nenhum registro de HHT cadastrado ainda." />
        ) : (
        <Table noNativeElements>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Obra</TableHeaderCell>
              <TableHeaderCell>Ano</TableHeaderCell>
              <TableHeaderCell>Mês</TableHeaderCell>
              <TableHeaderCell>HHT</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {registros.map((registro) => (
              <TableRow key={registro.id}>
                <TableCell>{registro.obraNome ?? '—'}</TableCell>
                <TableCell>{registro.ano}</TableCell>
                <TableCell>{nomesMes[registro.mes - 1]}</TableCell>
                <TableCell>{registro.horasHomemTrabalhadas.toLocaleString('pt-BR')}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={() => excluir(registro.id)}
                    aria-label="Excluir"
                  />
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
