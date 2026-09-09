import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Campo,
  Checkbox,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormSection,
  Input,
  PageHeader,
  PainelLateral,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, ArrowSync24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  tipoInspecaoLabel,
  type ChecklistModelo,
  type NovoChecklistModeloItem,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const itemVazio: NovoChecklistModeloItem = {
  descricao: '',
  exigeFotografia: false,
  exigeResponsavel: false,
  exigePrazo: false,
};

// Onda 2 Task 10 (camada ui/): o formulário de criação/nova versão (mais os itens que ele acumula
// antes de salvar) empurrava a lista pra baixo — mesmo golden rule já aplicado em
// TrabalhadoresTab.tsx/AprsTab.tsx (spec §4.2), mesmo sem a task marcar a conversão 2 literalmente:
// vira PainelLateral. Erro do painel é estado PRÓPRIO (erroPainel), nunca o erro de nível de página.
export function ChecklistModelosTab() {
  const [checklists, setChecklists] = useState<ChecklistModelo[]>([]);
  const [nome, setNome] = useState('');
  const [tipoInspecao, setTipoInspecao] = useState(1);
  const [itens, setItens] = useState<NovoChecklistModeloItem[]>([]);
  const [itemAtual, setItemAtual] = useState<NovoChecklistModeloItem>(itemVazio);
  const [versionandoId, setVersionandoId] = useState<string | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
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

  // Todo caminho de fechar o painel limpa o formulário e o erro dele — senão reabrir mostra
  // rascunho e mensagem de uma tentativa anterior (regra dos 3 pilotos).
  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
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
      setPainelAberto(true);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar checklist para nova versão.');
    }
  }

  async function salvar() {
    if (itens.length === 0) {
      setErroPainel('Adicione ao menos um item ao checklist.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      if (versionandoId) {
        await api.checklistModelos.novaVersao(versionandoId, itens);
        fecharPainel();
        await carregar();
        sucessoToast('Nova versão do checklist criada com sucesso.');
      } else {
        if (!nome.trim()) {
          setErroPainel('Informe o nome do checklist.');
          setCarregando(false);
          return;
        }
        await api.checklistModelos.criar({ nome, tipoInspecao, itens });
        fecharPainel();
        await carregar();
        sucessoToast('Checklist criado com sucesso.');
      }
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao salvar checklist.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este modelo de checklist? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.checklistModelos.excluir(id);
      await carregar();
      sucessoToast('Checklist excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir checklist.');
    }
  }

  const colunasItens: Coluna<NovoChecklistModeloItem & { indice: number }>[] = [
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'exigeFotografia', rotulo: 'Foto', render: (i) => (i.exigeFotografia ? 'Sim' : '—') },
    { chave: 'exigeResponsavel', rotulo: 'Responsável', render: (i) => (i.exigeResponsavel ? 'Sim' : '—') },
    { chave: 'exigePrazo', rotulo: 'Prazo', render: (i) => (i.exigePrazo ? 'Sim' : '—') },
  ];

  const colunasChecklists: Coluna<ChecklistModelo>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'tipo', rotulo: 'Tipo', render: (c) => tipoInspecaoLabel[c.tipoInspecao] },
    { chave: 'versao', rotulo: 'Versão', render: (c) => <StatusChip tom="neutro">v{c.versao}</StatusChip> },
    { chave: 'quantidadeItens', rotulo: 'Itens' },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Checklists de inspeção"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Novo checklist
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card>
        <DataTable
          aria-label="Checklists cadastrados"
          colunas={colunasChecklists}
          linhas={checklists}
          chaveLinha={(c) => c.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum checklist cadastrado ainda.',
            descricao: 'Cadastre o primeiro checklist para começar.',
            acao: { rotulo: 'Novo checklist', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(c) => (
            <div style={{ display: 'flex', gap: 4 }}>
              <Button
                appearance="subtle"
                icon={<ArrowSync24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  iniciarNovaVersao(c);
                }}
                aria-label="Nova versão"
              />
              <Button
                appearance="subtle"
                icon={<Delete24Regular />}
                onClick={(evento) => {
                  evento.stopPropagation();
                  excluir(c.id);
                }}
                aria-label="Excluir"
              />
            </div>
          )}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo={versionandoId ? `Nova versão de "${nome}"` : 'Novo checklist'}
        largura="lg"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={salvar} disabled={carregando}>
              {versionandoId ? 'Salvar nova versão' : 'Criar checklist'}
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}

        <FormSection titulo="Dados do checklist" numero={1} primeira>
          <FormGrid>
            <Campo span={6}>
              <Field label="Nome do checklist">
                <Input value={nome} onChange={(_, d) => setNome(d.value)} disabled={!!versionandoId} />
              </Field>
            </Campo>
            <Campo span={6}>
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
            </Campo>
          </FormGrid>
        </FormSection>

        <FormSection titulo="Itens do checklist" numero={2}>
          <FormGrid>
            <Campo span={6}>
              <Field label="Descrição do item">
                <Input
                  value={itemAtual.descricao}
                  onChange={(_, d) => setItemAtual({ ...itemAtual, descricao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Checkbox
                label="Exige fotografia"
                checked={itemAtual.exigeFotografia}
                onChange={(_, d) => setItemAtual({ ...itemAtual, exigeFotografia: !!d.checked })}
              />
            </Campo>
            <Campo span={2}>
              <Checkbox
                label="Exige responsável"
                checked={itemAtual.exigeResponsavel}
                onChange={(_, d) => setItemAtual({ ...itemAtual, exigeResponsavel: !!d.checked })}
              />
            </Campo>
            <Campo span={2}>
              <Checkbox
                label="Exige prazo"
                checked={itemAtual.exigePrazo}
                onChange={(_, d) => setItemAtual({ ...itemAtual, exigePrazo: !!d.checked })}
              />
            </Campo>
          </FormGrid>

          <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
            <Button appearance="secondary" icon={<Add24Regular />} onClick={adicionarItem}>
              Adicionar item à lista
            </Button>
          </div>

          <DataTable
            aria-label="Itens adicionados a este checklist"
            colunas={colunasItens}
            linhas={itens.map((item, indice) => ({ ...item, indice }))}
            chaveLinha={(i) => String(i.indice)}
            vazio={{ titulo: 'Nenhum item adicionado ainda.' }}
            densidade="compacta"
            acoesLinha={(i) => (
              <Button
                appearance="subtle"
                size="small"
                icon={<Delete24Regular />}
                onClick={() => removerItem(i.indice)}
                aria-label="Remover item"
              />
            )}
          />
        </FormSection>
      </PainelLateral>
    </div>
  );
}
