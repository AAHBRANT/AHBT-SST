import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelCriacaoInline,
  FormGrid,
  FormRodape,
  FormSection,
  Campo,
  FeedbackInline,
  Legenda,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Textarea } from '@fluentui/react-components';
import { Add24Regular, Delete24Regular, Edit24Regular } from '@fluentui/react-icons';
import {
  api,
  CategoriaNovidade,
  type NovidadeVersao,
  type NovidadeVersaoItemInput,
  type NovaNovidadeVersao,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const categoriaLabel: Record<number, string> = {
  [CategoriaNovidade.Correcao]: 'Correção',
  [CategoriaNovidade.Melhoria]: 'Melhoria',
  [CategoriaNovidade.Novidade]: 'Novidade',
};

function hoje() {
  return new Date().toISOString().slice(0, 10);
}

const itemVazio: NovidadeVersaoItemInput = { categoria: CategoriaNovidade.Melhoria, descricao: '', antes: '', agora: '' };

function formularioVazio(): NovaNovidadeVersao {
  return { titulo: '', versao: '', dataPublicacao: hoje(), itens: [{ ...itemVazio }] };
}

// Tela de cadastro do pop-up de novidades (requisito do usuário, 18/09) — mesmo padrão de
// PainelCriacaoInline + DataTable já usado em FuncoesTab.tsx. Qualquer usuário logado pode cadastrar
// (decisão do usuário ao aprovar o design): não há tela de aprovação nem rascunho, o que é salvo já
// fica visível no pop-up de todo mundo que ainda não viu.
export function NovidadesTab() {
  const [novidades, setNovidades] = useState<NovidadeVersao[]>([]);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [painelAberto, setPainelAberto] = useState(false);
  const [editandoId, setEditandoId] = useState<string | null>(null);
  const [formulario, setFormulario] = useState<NovaNovidadeVersao>(formularioVazio());
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setNovidades(await api.novidades.listar());
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar novidades.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setEditandoId(null);
    setFormulario(formularioVazio());
  }

  function abrirParaCriar() {
    setEditandoId(null);
    setFormulario(formularioVazio());
    setPainelAberto(true);
  }

  function abrirParaEditar(novidade: NovidadeVersao) {
    setEditandoId(novidade.id);
    setFormulario({
      titulo: novidade.titulo,
      versao: novidade.versao,
      dataPublicacao: novidade.dataPublicacao.slice(0, 10),
      itens: novidade.itens.map((i) => ({
        categoria: i.categoria,
        descricao: i.descricao,
        antes: i.antes ?? '',
        agora: i.agora ?? '',
      })),
    });
    setPainelAberto(true);
  }

  function atualizarItem(indice: number, alteracao: Partial<NovidadeVersaoItemInput>) {
    setFormulario((atual) => ({
      ...atual,
      itens: atual.itens.map((item, i) => (i === indice ? { ...item, ...alteracao } : item)),
    }));
  }

  function adicionarItem() {
    setFormulario((atual) => ({ ...atual, itens: [...atual.itens, { ...itemVazio }] }));
  }

  function removerItem(indice: number) {
    setFormulario((atual) => ({ ...atual, itens: atual.itens.filter((_, i) => i !== indice) }));
  }

  async function salvar() {
    try {
      setSalvando(true);
      setErroPainel(null);
      const payload: NovaNovidadeVersao = {
        ...formulario,
        dataPublicacao: new Date(formulario.dataPublicacao).toISOString(),
        itens: formulario.itens.map((item) => ({
          ...item,
          antes: item.antes?.trim() ? item.antes : null,
          agora: item.agora?.trim() ? item.agora : null,
        })),
      };

      if (editandoId) {
        await api.novidades.atualizar(editandoId, payload);
        sucessoToast('Novidade atualizada com sucesso.');
      } else {
        await api.novidades.criar(payload);
        sucessoToast('Novidade cadastrada com sucesso.');
      }
      await carregar();
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao salvar a novidade.');
    } finally {
      setSalvando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta novidade? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.novidades.excluir(id);
      await carregar();
      sucessoToast('Novidade excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir novidade.');
    }
  }

  const formularioValido =
    formulario.titulo.trim().length > 0 &&
    formulario.versao.trim().length > 0 &&
    formulario.itens.length > 0 &&
    formulario.itens.every((i) => i.descricao.trim().length > 0);

  const colunas: Coluna<NovidadeVersao>[] = [
    { chave: 'versao', rotulo: 'Versão' },
    { chave: 'titulo', rotulo: 'Título' },
    {
      chave: 'dataPublicacao',
      rotulo: 'Publicado em',
      render: (n) => new Date(n.dataPublicacao).toLocaleDateString('pt-BR'),
    },
    { chave: 'itens', rotulo: 'Itens', render: (n) => String(n.itens.length) },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Novidades da versão"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : abrirParaCriar())}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-novidade"
          >
            {painelAberto ? 'Fechar' : 'Cadastrar novidade'}
          </Button>
        }
      />
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div id="painel-nova-novidade">
        <PainelCriacaoInline aberto={painelAberto} titulo={editandoId ? 'Editar novidade' : 'Nova novidade'}>
          <FormSection titulo="Dados da versão" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={3}>
                <Field label="Versão">
                  <Input
                    value={formulario.versao}
                    placeholder="Ex.: 5.11.0"
                    onChange={(_, d) => setFormulario({ ...formulario, versao: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Título">
                  <Input
                    value={formulario.titulo}
                    placeholder="Ex.: Melhorias no módulo de Uniformes"
                    onChange={(_, d) => setFormulario({ ...formulario, titulo: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={3}>
                <Field label="Publicado em">
                  <Input
                    type="date"
                    value={formulario.dataPublicacao}
                    onChange={(_, d) => setFormulario({ ...formulario, dataPublicacao: d.value })}
                  />
                </Field>
              </Campo>
            </FormGrid>
          </FormSection>

          <FormSection titulo="Itens (o que mudou)" numero={2}>
            <Legenda>Cada item vira uma linha clicável no pop-up. "Antes" e "Agora" são opcionais.</Legenda>
            {formulario.itens.map((item, indice) => (
              <div key={indice} style={{ marginTop: 12 }}>
              <Card>
                <FormGrid>
                  <Campo span={3}>
                    <Field label="Categoria">
                      <select
                        value={item.categoria}
                        onChange={(e) => atualizarItem(indice, { categoria: Number(e.target.value) })}
                        style={{ height: 32, borderRadius: 6 }}
                      >
                        {Object.entries(categoriaLabel).map(([valor, rotulo]) => (
                          <option key={valor} value={valor}>
                            {rotulo}
                          </option>
                        ))}
                      </select>
                    </Field>
                  </Campo>
                  <Campo span={8}>
                    <Field label="Descrição (o que aparece na lista)">
                      <Input
                        value={item.descricao}
                        placeholder="Ex.: Corrigido bug de assinatura em branco no PDF de inspeção"
                        onChange={(_, d) => atualizarItem(indice, { descricao: d.value })}
                      />
                    </Field>
                  </Campo>
                  <Campo span={1}>
                    <Field label=" ">
                      <Button
                        appearance="subtle"
                        icon={<Delete24Regular />}
                        onClick={() => removerItem(indice)}
                        disabled={formulario.itens.length === 1}
                        aria-label="Remover item"
                      />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Antes (opcional)">
                      <Textarea
                        value={item.antes ?? ''}
                        resize="vertical"
                        rows={2}
                        onChange={(_, d) => atualizarItem(indice, { antes: d.value })}
                      />
                    </Field>
                  </Campo>
                  <Campo span={6}>
                    <Field label="Agora (opcional)">
                      <Textarea
                        value={item.agora ?? ''}
                        resize="vertical"
                        rows={2}
                        onChange={(_, d) => atualizarItem(indice, { agora: d.value })}
                      />
                    </Field>
                  </Campo>
                </FormGrid>
              </Card>
              </div>
            ))}
            <Button appearance="secondary" icon={<Add24Regular />} onClick={adicionarItem} style={{ marginTop: 12 }}>
              Adicionar item
            </Button>

            <FormRodape info="Ao salvar, a novidade já aparece no pop-up de quem ainda não a viu.">
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={salvar} disabled={salvando || !formularioValido}>
                {editandoId ? 'Salvar alterações' : 'Cadastrar novidade'}
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card>
        <DataTable
          aria-label="Novidades cadastradas"
          colunas={colunas}
          linhas={novidades}
          chaveLinha={(n) => n.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma novidade cadastrada ainda',
            acao: { rotulo: 'Cadastrar novidade', aoClicar: abrirParaCriar },
          }}
          acoesLinha={(n) => (
            <>
              <Button appearance="subtle" icon={<Edit24Regular />} onClick={() => abrirParaEditar(n)} aria-label="Editar" />
              <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(n.id)} aria-label="Excluir" />
            </>
          )}
        />
      </Card>
    </div>
  );
}
