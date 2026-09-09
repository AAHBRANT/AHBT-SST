import { useEffect, useState } from 'react';
import {
  Button,
  Campo,
  Card,
  CampoData,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  StatusControleRisco,
  statusControleRiscoLabel,
  type NovoPlanoAcaoItem,
  type PlanoAcaoItem,
  type RiscoClassificado,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function itemVazio(pgrId: string): NovoPlanoAcaoItem {
  return {
    pgrId,
    riscoId: null,
    descricao: '',
    responsavelUsuarioId: null,
    prazo: null,
    status: StatusControleRisco.Pendente,
  };
}

// Onda 2 Task 8 (camada ui/): plano de ação do PGR — mesmo formato de AprEtapasTab.tsx (Card +
// FormSection + DataTable). Status permanece um `Select` editável por linha (não vira StatusChip):
// é o mesmo padrão de edição inline já usado noutros módulos (spec, "edição inline por linha resolvida
// via render condicional, sem gap de componente") — trocar por chip perderia a ação de mudar o status
// direto na lista, que a tela original já oferecia.
export function PlanoAcaoTab({ pgrId, riscosDisponiveis }: { pgrId: string; riscosDisponiveis: RiscoClassificado[] }) {
  const [itens, setItens] = useState<PlanoAcaoItem[]>([]);
  const [novoItem, setNovoItem] = useState<NovoPlanoAcaoItem>(() => itemVazio(pgrId));
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      setItens(await api.planoAcao.listar(pgrId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar plano de ação.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
    setNovoItem(itemVazio(pgrId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pgrId]);

  function nomePerigo(riscoId?: string | null) {
    if (!riscoId) return '—';
    return riscosDisponiveis.find((r) => r.riscoId === riscoId)?.perigoNome ?? riscoId;
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.planoAcao.criar(novoItem);
      setNovoItem(itemVazio(pgrId));
      await carregar();
      sucessoToast('Item do plano de ação criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar item do plano de ação.');
    } finally {
      setCarregando(false);
    }
  }

  async function mudarStatus(item: PlanoAcaoItem, status: number) {
    try {
      setErro(null);
      await api.planoAcao.atualizar(item.id, { ...item, status });
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao atualizar status do item.');
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este item do plano de ação? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.planoAcao.excluir(id);
      await carregar();
      sucessoToast('Item do plano de ação excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir item do plano de ação.');
    }
  }

  const colunas: Coluna<PlanoAcaoItem>[] = [
    { chave: 'descricao', rotulo: 'Descrição' },
    { chave: 'risco', rotulo: 'Risco', render: (item) => nomePerigo(item.riscoId) },
    { chave: 'prazo', rotulo: 'Prazo', render: (item) => item.prazo?.slice(0, 10) ?? '' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (item) => (
        <Select
          value={item.status}
          onChange={(_, d) => mudarStatus(item, Number(d.value))}
          style={{ minWidth: 140 }}
        >
          {Object.entries(statusControleRiscoLabel).map(([valor, rotulo]) => (
            <option key={valor} value={valor}>
              {rotulo}
            </option>
          ))}
        </Select>
      ),
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Plano de ação">
        <FormSection titulo="Novo item do plano de ação" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Risco relacionado">
                <Select
                  value={novoItem.riscoId ?? ''}
                  onChange={(_, d) => setNovoItem({ ...novoItem, riscoId: d.value || null })}
                >
                  <option value="">Nenhum</option>
                  {riscosDisponiveis.map((risco) => (
                    <option key={risco.riscoId} value={risco.riscoId}>
                      {risco.perigoNome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Descrição da ação">
                <Input
                  value={novoItem.descricao}
                  onChange={(_, d) => setNovoItem({ ...novoItem, descricao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Prazo">
                <CampoData
                  value={novoItem.prazo ?? ''}
                  onChange={(_, d) => setNovoItem({ ...novoItem, prazo: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Status">
                <Select
                  value={novoItem.status}
                  onChange={(_, d) => setNovoItem({ ...novoItem, status: Number(d.value) })}
                >
                  {Object.entries(statusControleRiscoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar item
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="Itens do plano de ação"
          colunas={colunas}
          linhas={itens}
          chaveLinha={(item) => item.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum item cadastrado no plano de ação ainda.' }}
          acoesLinha={(item) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(item.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      {dialogElement}
    </>
  );
}
