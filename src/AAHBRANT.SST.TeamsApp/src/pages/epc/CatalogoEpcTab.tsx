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
import { api, type CatalogoEpc, type NovoCatalogoEpc } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { FotoCatalogoEpc } from './FotoCatalogoEpc';

const itemVazio: NovoCatalogoEpc = { nome: '', categoria: '' };

// Catálogo de EPC (Equipamento de Proteção Coletiva) — mesmo padrão de CatalogoUniformeTab.tsx/
// CatalogoTab.tsx (EPI), incluindo foto do item (decisão do usuário, 2026-09-07: "os 3 devem ser
// bem parecidos"). Sem tamanho embutido (EPC não é peça vestível).
export function CatalogoEpcTab() {
  const estilos = usePageStyles();
  const [itens, setItens] = useState<CatalogoEpc[]>([]);
  const [novoItem, setNovoItem] = useState<NovoCatalogoEpc>(itemVazio);
  const [fotoNovoItem, setFotoNovoItem] = useState<File | null>(null);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoEpc | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setItens(await api.catalogosEpc.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de EPC.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.catalogosEpc.criar(novoItem);
      if (fotoNovoItem) {
        await api.catalogosEpc.anexarFoto(id, fotoNovoItem);
      }
      setNovoItem(itemVazio);
      setFotoNovoItem(null);
      await carregar();
      sucessoToast('Item de EPC cadastrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar item de EPC.');
    } finally {
      setCarregando(false);
    }
  }

  async function trocarFoto(itemId: string, arquivo: File) {
    try {
      setErro(null);
      await api.catalogosEpc.anexarFoto(itemId, arquivo);
      await carregar();
      sucessoToast('Foto do item atualizada.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto do item.');
    }
  }

  function iniciarEdicao(item: CatalogoEpc) {
    setEdicaoId(item.id);
    setEdicao({ ...item });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosEpc.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Item de EPC atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar item de EPC.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este item do catálogo de EPC? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosEpc.excluir(id);
      await carregar();
      sucessoToast('Item de EPC excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir item de EPC.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Catálogo de EPC</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados do Item</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col6}>
          <Field label="Nome (ex.: Extintor, Guarda-corpo, Placa de sinalização)">
            <Input value={novoItem.nome} onChange={(_, d) => setNovoItem({ ...novoItem, nome: d.value })} />
          </Field>
        </div>
        <div className={estilos.col6}>
          <Field label="Categoria (opcional)">
            <Input
              value={novoItem.categoria ?? ''}
              onChange={(_, d) => setNovoItem({ ...novoItem, categoria: d.value })}
            />
          </Field>
        </div>
        <div className={estilos.col6}>
          <Field label="Foto do item">
            <SeletorFotoCamera
              rotulo={fotoNovoItem ? fotoNovoItem.name : 'Tirar foto ou escolher arquivo'}
              tiposAceitos="image/jpeg,image/png"
              aoSelecionarArquivo={(arquivo) => setFotoNovoItem(arquivo)}
              aoErroValidacao={setErro}
            />
          </Field>
        </div>
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !novoItem.nome.trim()}>
          Adicionar item
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : itens.length === 0 ? (
        <EstadoVazio mensagem="Nenhum item cadastrado no catálogo de EPC ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Foto</TableHeaderCell>
            <TableHeaderCell>Nome</TableHeaderCell>
            <TableHeaderCell>Categoria</TableHeaderCell>
            <TableHeaderCell></TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {itens.map((item) =>
            edicaoId === item.id && edicao ? (
              <TableRow key={item.id}>
                <TableCell>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                    <FotoCatalogoEpc catalogoEpcId={item.id} temFoto={item.temFoto} tamanho={36} />
                    <SeletorFotoCamera
                      apenasIcone
                      tamanho="small"
                      rotulo="Trocar foto"
                      tiposAceitos="image/jpeg,image/png"
                      aoSelecionarArquivo={(arquivo) => trocarFoto(item.id, arquivo)}
                      aoErroValidacao={setErro}
                    />
                  </div>
                </TableCell>
                <TableCell>
                  <Input value={edicao.nome} onChange={(_, d) => setEdicao({ ...edicao, nome: d.value })} />
                </TableCell>
                <TableCell>
                  <Input
                    value={edicao.categoria ?? ''}
                    onChange={(_, d) => setEdicao({ ...edicao, categoria: d.value })}
                  />
                </TableCell>
                <TableCell>
                  <Button appearance="subtle" icon={<Save24Regular />} onClick={salvarEdicao} disabled={carregando} aria-label="Salvar" />
                </TableCell>
              </TableRow>
            ) : (
              <TableRow key={item.id} onClick={() => iniciarEdicao(item)} style={{ cursor: 'pointer' }}>
                <TableCell>
                  <FotoCatalogoEpc catalogoEpcId={item.id} temFoto={item.temFoto} tamanho={36} />
                </TableCell>
                <TableCell>{item.nome}</TableCell>
                <TableCell>{item.categoria}</TableCell>
                <TableCell>
                  <Button
                    appearance="subtle"
                    icon={<Delete24Regular />}
                    onClick={(e) => {
                      e.stopPropagation();
                      excluir(item.id);
                    }}
                    aria-label="Excluir"
                  />
                </TableCell>
              </TableRow>
            ),
          )}
        </TableBody>
      </Table>
      )}
      <Text size={200} style={{ display: 'block', marginTop: 8 }}>
        Clique em uma linha para editar. O estoque (por obra) é controlado na aba Estoque.
      </Text>
    </div>
  );
}
