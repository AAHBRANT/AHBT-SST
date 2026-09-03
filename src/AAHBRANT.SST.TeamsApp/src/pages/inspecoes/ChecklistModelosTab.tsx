import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Checkbox,
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
import { Add24Regular, ArrowSync24Regular, Delete24Regular, Dismiss24Regular } from '@fluentui/react-icons';
import {
  api,
  tipoInspecaoLabel,
  type ChecklistModelo,
  type NovoChecklistModeloItem,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const itemVazio: NovoChecklistModeloItem = {
  descricao: '',
  exigeFotografia: false,
  exigeResponsavel: false,
  exigePrazo: false,
};

export function ChecklistModelosTab() {
  const estilos = usePageStyles();
  const [checklists, setChecklists] = useState<ChecklistModelo[]>([]);
  const [nome, setNome] = useState('');
  const [tipoInspecao, setTipoInspecao] = useState(1);
  const [itens, setItens] = useState<NovoChecklistModeloItem[]>([]);
  const [itemAtual, setItemAtual] = useState<NovoChecklistModeloItem>(itemVazio);
  const [versionandoId, setVersionandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setChecklists(await api.checklistModelos.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar checklists.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function adicionarItem() {
    if (!itemAtual.descricao.trim()) return;
    setItens((atual) => [...atual, itemAtual]);
    setItemAtual(itemVazio);
  }

  function removerItem(indice: number) {
    setItens((atual) => atual.filter((_, i) => i !== indice));
  }

  function limparFormulario() {
    setNome('');
    setTipoInspecao(1);
    setItens([]);
    setItemAtual(itemVazio);
    setVersionandoId(null);
  }

  async function iniciarNovaVersao(checklist: ChecklistModelo) {
    try {
      setErro(null);
      const detalhe = await api.checklistModelos.obterDetalhe(checklist.id);
      setVersionandoId(checklist.id);
      setNome(detalhe.checklistModelo.nome);
      setTipoInspecao(detalhe.checklistModelo.tipoInspecao);
      setItens(
        detalhe.itens.map((i) => ({
          descricao: i.descricao,
          exigeFotografia: i.exigeFotografia,
          exigeResponsavel: i.exigeResponsavel,
          exigePrazo: i.exigePrazo,
        })),
      );
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar checklist para nova versão.');
    }
  }

  async function salvar() {
    if (itens.length === 0) {
      setErro('Adicione ao menos um item ao checklist.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      if (versionandoId) {
        await api.checklistModelos.novaVersao(versionandoId, itens);
        limparFormulario();
        await carregar();
        sucessoToast('Nova versão do checklist criada com sucesso.');
      } else {
        if (!nome.trim()) {
          setErro('Informe o nome do checklist.');
          setCarregando(false);
          return;
        }
        await api.checklistModelos.criar({ nome, tipoInspecao, itens });
        limparFormulario();
        await carregar();
        sucessoToast('Checklist criado com sucesso.');
      }
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar checklist.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string, evento: React.MouseEvent) {
    evento.stopPropagation();
    if (!(await confirmar('Excluir este modelo de checklist? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.checklistModelos.excluir(id);
      await carregar();
      sucessoToast('Checklist excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir checklist.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">
          {versionandoId ? `Nova versão de "${nome}"` : 'Novo checklist'}
        </Text>
        {versionandoId && (
          <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={limparFormulario}>
            Cancelar nova versão
          </Button>
        )}
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do Checklist</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col6}>
          <Field label="Nome do checklist">
            <Input value={nome} onChange={(_, d) => setNome(d.value)} disabled={!!versionandoId} />
          </Field>
        </div>
        <div className={estilos.col6}>
          <Field label="Tipo de inspeção">
            <Select
              value={String(tipoInspecao)}
              onChange={(_, d) => setTipoInspecao(Number(d.value))}
              disabled={!!versionandoId}
            >
              {Object.entries(tipoInspecaoLabel).map(([valor, rotulo]) => (
                <option key={valor} value={valor}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
        </div>
      </div>

      <div className={estilos.sectionTitle}>Itens do Checklist</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col6}>
          <Field label="Descrição do item">
            <Input
              value={itemAtual.descricao}
              onChange={(_, d) => setItemAtual({ ...itemAtual, descricao: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col2}>
          <Checkbox
            label="Exige fotografia"
            checked={itemAtual.exigeFotografia}
            onChange={(_, d) => setItemAtual({ ...itemAtual, exigeFotografia: !!d.checked })}
          />
        </div>
        <div className={estilos.col2}>
          <Checkbox
            label="Exige responsável"
            checked={itemAtual.exigeResponsavel}
            onChange={(_, d) => setItemAtual({ ...itemAtual, exigeResponsavel: !!d.checked })}
          />
        </div>
        <div className={estilos.col2}>
          <Checkbox
            label="Exige prazo"
            checked={itemAtual.exigePrazo}
            onChange={(_, d) => setItemAtual({ ...itemAtual, exigePrazo: !!d.checked })}
          />
        </div>
      </div>

      <div className={estilos.formActions}>
        <Button appearance="secondary" icon={<Add24Regular />} onClick={adicionarItem}>
          Adicionar item à lista
        </Button>
      </div>

      <Table noNativeElements style={{ marginBottom: 16 }}>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Descrição</TableHeaderCell>
            <TableHeaderCell>Foto</TableHeaderCell>
            <TableHeaderCell>Responsável</TableHeaderCell>
            <TableHeaderCell>Prazo</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item, indice) => (
            <TableRow key={indice}>
              <TableCell>{item.descricao}</TableCell>
              <TableCell>{item.exigeFotografia ? 'Sim' : '—'}</TableCell>
              <TableCell>{item.exigeResponsavel ? 'Sim' : '—'}</TableCell>
              <TableCell>{item.exigePrazo ? 'Sim' : '—'}</TableCell>
              <TableCell>
                <Button
                  appearance="subtle"
                  icon={<Delete24Regular />}
                  onClick={() => removerItem(indice)}
                  aria-label="Remover item"
                />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={salvar} disabled={carregando}>
          {versionandoId ? 'Salvar nova versão' : 'Criar checklist'}
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : checklists.length === 0 ? (
        <EstadoVazio mensagem="Nenhum checklist cadastrado ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Tipo</TableHeaderCell>
            <TableHeaderCell>Versão</TableHeaderCell>
            <TableHeaderCell>Itens</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {checklists.map((checklist) => (
            <TableRow key={checklist.id}>
              <TableCell>{checklist.nome}</TableCell>
              <TableCell>{tipoInspecaoLabel[checklist.tipoInspecao]}</TableCell>
              <TableCell>
                <Badge appearance="tint">v{checklist.versao}</Badge>
              </TableCell>
              <TableCell>{checklist.quantidadeItens}</TableCell>
              <TableCell>
                <div style={{ display: 'flex', gap: 4 }}>
                  <Button
                    appearance="subtle"
                    icon={<ArrowSync24Regular />}
                    onClick={() => iniciarNovaVersao(checklist)}
                    aria-label="Nova versão"
                  />
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(evento) => excluir(checklist.id, evento)}
                    aria-label="Excluir"
                  />
                </div>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
      )}
    </div>
  );
}
