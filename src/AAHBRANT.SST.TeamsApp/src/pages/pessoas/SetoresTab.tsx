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
import { api, type NovoSetor, type Obra, type Setor } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

export function SetoresTab() {
  const estilos = usePageStyles();
  const [obras, setObras] = useState<Obra[]>([]);
  const [setores, setSetores] = useState<Setor[]>([]);
  const [novoSetor, setNovoSetor] = useState<NovoSetor>({ obraId: '', nome: '' });
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [obrasResp, setoresResp] = await Promise.all([api.obras.listar(), api.setores.listar()]);
      setObras(obrasResp);
      setSetores(setoresResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar setores.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!novoSetor.obraId) {
      setErro('Selecione a obra do setor.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.setores.criar(novoSetor);
      setNovoSetor({ obraId: '', nome: '' });
      await carregar();
      sucessoToast('Setor criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar setor.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este setor? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.setores.excluir(id);
      await carregar();
      sucessoToast('Setor excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir setor.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Setores cadastrados</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do Setor</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col4}>
          <Field label="Obra">
            <Select value={novoSetor.obraId} onChange={(_, d) => setNovoSetor({ ...novoSetor, obraId: d.value })}>
              <option value="">Selecione a obra</option>
              {obras.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.nome}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className={estilos.col6}>
          <Field label="Nome do setor">
            <Input value={novoSetor.nome} onChange={(_, d) => setNovoSetor({ ...novoSetor, nome: d.value })} />
          </Field>
        </div>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar setor
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : setores.length === 0 ? (
        <EstadoVazio mensagem="Nenhum setor cadastrado ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Setor</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {setores.map((setor) => (
            <TableRow key={setor.id}>
              <TableCell>{setor.obraNome}</TableCell>
              <TableCell>{setor.nome}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(setor.id)}
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
