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
import { api, type Equipe, type NovaEquipe, type Setor, type Trabalhador } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const equipeVazia: NovaEquipe = { setorId: '', nome: '', encarregadoId: null };

export function EquipesTab() {
  const estilos = usePageStyles();
  const [setores, setSetores] = useState<Setor[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaEquipe, setNovaEquipe] = useState<NovaEquipe>(equipeVazia);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [setoresResp, trabalhadoresResp, equipesResp] = await Promise.all([
        api.setores.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
      ]);
      setSetores(setoresResp);
      setTrabalhadores(trabalhadoresResp);
      setEquipes(equipesResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar equipes.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    if (!novaEquipe.setorId) {
      setErro('Selecione o setor da equipe.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.equipes.criar(novaEquipe);
      setNovaEquipe(equipeVazia);
      await carregar();
      sucessoToast('Equipe criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar equipe.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta equipe? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.equipes.excluir(id);
      await carregar();
      sucessoToast('Equipe excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir equipe.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Equipes cadastradas</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.form}>
        <Field label="Setor">
          <Select
            value={novaEquipe.setorId}
            onChange={(_, d) => setNovaEquipe({ ...novaEquipe, setorId: d.value })}
          >
            <option value="">Selecione o setor</option>
            {setores.map((s) => (
              <option key={s.id} value={s.id}>
                {s.obraNome} · {s.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Nome da equipe">
          <Input value={novaEquipe.nome} onChange={(_, d) => setNovaEquipe({ ...novaEquipe, nome: d.value })} />
        </Field>
        <Field label="Encarregado (opcional)">
          <Select
            value={novaEquipe.encarregadoId ?? ''}
            onChange={(_, d) => setNovaEquipe({ ...novaEquipe, encarregadoId: d.value || null })}
          >
            <option value="">Sem encarregado definido</option>
            {trabalhadores.map((t) => (
              <option key={t.id} value={t.id}>
                {t.nome}
              </option>
            ))}
          </Select>
        </Field>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
          Adicionar equipe
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : equipes.length === 0 ? (
        <EstadoVazio mensagem="Nenhuma equipe cadastrada ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Obra</TableHeaderCell>
            <TableHeaderCell>Setor</TableHeaderCell>
            <TableHeaderCell>Equipe</TableHeaderCell>
            <TableHeaderCell>Encarregado</TableHeaderCell>
            <TableHeaderCell>Funcionários</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {equipes.map((equipe) => (
            <TableRow key={equipe.id}>
              <TableCell>{equipe.obraNome}</TableCell>
              <TableCell>{equipe.setorNome}</TableCell>
              <TableCell>{equipe.nome}</TableCell>
              <TableCell>{equipe.encarregadoNome ?? '—'}</TableCell>
              <TableCell>{equipe.quantidadeTrabalhadores}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => excluir(equipe.id)}
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
