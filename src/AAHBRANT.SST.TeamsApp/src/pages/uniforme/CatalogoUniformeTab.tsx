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
import { api, type CatalogoUniforme, type NovoCatalogoUniforme } from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';
import { ListaCarregando } from '../../components/ListaCarregando';

const itemVazio: NovoCatalogoUniforme = { nome: '', categoria: '' };

// Catálogo de Uniforme (peça em si, sem tamanho embutido — o tamanho é uma dimensão do estoque e
// do cadastro do trabalhador, ver EstoqueUniformeTab.tsx e pages/pessoas/TamanhosUniformeSecao.tsx).
// Mesmo padrão de CatalogoTab.tsx (EPI), sem foto de item (não pedido para uniforme).
export function CatalogoUniformeTab() {
  const estilos = usePageStyles();
  const [itens, setItens] = useState<CatalogoUniforme[]>([]);
  const [novoItem, setNovoItem] = useState<NovoCatalogoUniforme>(itemVazio);
  const [edicaoId, setEdicaoId] = useState<string | null>(null);
  const [edicao, setEdicao] = useState<CatalogoUniforme | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setItens(await api.catalogosUniforme.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar catálogo de uniforme.');
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
      await api.catalogosUniforme.criar(novoItem);
      setNovoItem(itemVazio);
      await carregar();
      sucessoToast('Peça de uniforme cadastrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  function iniciarEdicao(item: CatalogoUniforme) {
    setEdicaoId(item.id);
    setEdicao({ ...item });
  }

  async function salvarEdicao() {
    if (!edicao) return;
    try {
      setCarregando(true);
      setErro(null);
      await api.catalogosUniforme.atualizar(edicao);
      setEdicaoId(null);
      setEdicao(null);
      await carregar();
      sucessoToast('Peça de uniforme atualizada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar peça de uniforme.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta peça do catálogo de uniforme? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.catalogosUniforme.excluir(id);
      await carregar();
      sucessoToast('Peça de uniforme excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir peça de uniforme.');
    }
  }

  return (
    <div className={estilos.card}>
      {dialogElement}
      <div className={estilos.toolbar}>
        <Text weight="semibold">Catálogo de Uniforme</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={`${estilos.sectionTitle} ${estilos.sectionTitleFirst}`}>Dados da Peça</div>
      <div className={estilos.formGrid}>
        <div className={estilos.col6}>
          <Field label="Nome (ex.: Camisa, Calça, Bota)">
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
      </div>
      <div className={estilos.formActions}>
        <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || !novoItem.nome.trim()}>
          Adicionar peça
        </Button>
      </div>

      {carregandoLista ? (
        <ListaCarregando />
      ) : itens.length === 0 ? (
        <EstadoVazio mensagem="Nenhuma peça cadastrada no catálogo de uniforme ainda." />
      ) : (
      <Table noNativeElements>
        <TableHeader>
          <TableRow>
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
        Clique em uma linha para editar. O estoque (por obra e tamanho) é controlado na aba Estoque.
      </Text>
    </div>
  );
}
