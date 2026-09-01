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
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  TipoArea,
  tipoAreaLabel,
  statusAreaLabel,
  type AreaSst,
  type NovaAreaSst,
  type Obra,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const areaVazia: NovaAreaSst = {
  codigo: '',
  nome: '',
  tipo: TipoArea.AreaDeTrabalho,
  obraId: '',
  detalhesLocalizacao: '',
  riscos: [],
  requisitos: [],
  status: 1,
};

export function AreasSstTab() {
  const estilos = usePageStyles();
  const [areas, setAreas] = useState<AreaSst[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novaArea, setNovaArea] = useState<NovaAreaSst>(areaVazia);
  const [riscosTexto, setRiscosTexto] = useState('');
  const [requisitosTexto, setRequisitosTexto] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [ars, obrs] = await Promise.all([api.areasSst.listar(), api.obras.listar()]);
      setAreas(ars);
      setObras(obrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar áreas.');
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

  function dividirLista(texto: string): string[] {
    return texto
      .split(',')
      .map((item) => item.trim())
      .filter((item) => item.length > 0);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.areasSst.criar({
        ...novaArea,
        riscos: dividirLista(riscosTexto),
        requisitos: dividirLista(requisitosTexto),
      });
      setNovaArea(areaVazia);
      setRiscosTexto('');
      setRequisitosTexto('');
      await carregar();
      sucessoToast('Área criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar área.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta área? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.areasSst.excluir(id);
      await carregar();
      sucessoToast('Área excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir área.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Áreas de SST cadastradas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Código">
          <Input value={novaArea.codigo} onChange={(_, d) => setNovaArea({ ...novaArea, codigo: d.value })} />
        </Field>
        <Field label="Nome">
          <Input value={novaArea.nome} onChange={(_, d) => setNovaArea({ ...novaArea, nome: d.value })} />
        </Field>
        <Field label="Tipo">
          <Select
            value={String(novaArea.tipo)}
            onChange={(_, d) => setNovaArea({ ...novaArea, tipo: Number(d.value) })}
          >
            {Object.entries(tipoAreaLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Obra">
          <Select value={novaArea.obraId} onChange={(_, d) => setNovaArea({ ...novaArea, obraId: d.value })}>
            <option value="">Selecione</option>
            {obras.map((obra) => (
              <option key={obra.id} value={obra.id}>
                {obra.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Detalhes da localização">
          <Input
            value={novaArea.detalhesLocalizacao ?? ''}
            onChange={(_, d) => setNovaArea({ ...novaArea, detalhesLocalizacao: d.value })}
          />
        </Field>
        <Field label="Riscos (separados por vírgula)">
          <Input value={riscosTexto} onChange={(_, d) => setRiscosTexto(d.value)} />
        </Field>
        <Field label="Requisitos (separados por vírgula)">
          <Input value={requisitosTexto} onChange={(_, d) => setRequisitosTexto(d.value)} />
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar área
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : areas.length === 0 ? (
        <EstadoVazio mensagem="Nenhuma área cadastrada ainda." />
      ) : (
      <Table>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Código</TableHeaderCell>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Tipo</TableHeaderCell>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Status</TableHeaderCell>
            <TableHeaderCell>Riscos</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {areas.map((area) => (
            <TableRow key={area.id}>
              <TableCell>{area.codigo}</TableCell>
              <TableCell>{area.nome}</TableCell>
              <TableCell>{tipoAreaLabel[area.tipo]}</TableCell>
              <TableCell>{nomeObra(area.obraId)}</TableCell>
              <TableCell>{statusAreaLabel[area.status]}</TableCell>
              <TableCell>{area.riscos.join(', ')}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(area.id)}
                  aria-label="Excluir"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      )}
    </div>
  );
}
