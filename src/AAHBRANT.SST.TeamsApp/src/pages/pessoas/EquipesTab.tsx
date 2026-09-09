import { useEffect, useState } from 'react';
import {
  Button,
  Field,
  Input,
  Card,
  PageHeader,
  DataTable,
  PainelLateral,
  FormGrid,
  Campo,
  FeedbackInline,
  SeletorPesquisavel,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { api, type Equipe, type NovaEquipe, type Setor, type Trabalhador } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

const equipeVazia: NovaEquipe = { setorId: '', nome: '', encarregadoId: null };

// Camada ui/ (Onda 2, Task 1): formulário de criação foi para um PainelLateral. Setor e Encarregado
// (lista de trabalhadores, potencialmente grande) viram SeletorPesquisavel (spec §3); Encarregado é
// opcional, então o `opcaoVazia` "Sem encarregado definido" preserva o caminho de volta ao vazio que
// o <select> original tinha (aprendizado do piloto 2).
export function EquipesTab() {
  const [setores, setSetores] = useState<Setor[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaEquipe, setNovaEquipe] = useState<NovaEquipe>(equipeVazia);
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

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    if (!novaEquipe.setorId) {
      setErroPainel('Selecione o setor da equipe.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.equipes.criar(novaEquipe);
      setNovaEquipe(equipeVazia);
      await carregar();
      sucessoToast('Equipe criada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar equipe.');
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

  const opcoesSetores = setores.map((s) => ({ id: s.id, rotulo: `${s.obraNome} · ${s.nome}` }));
  const opcoesTrabalhadores = trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome }));

  const colunas: Coluna<Equipe>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (e) => e.obraNome },
    { chave: 'setor', rotulo: 'Setor', render: (e) => e.setorNome },
    { chave: 'nome', rotulo: 'Equipe' },
    { chave: 'encarregado', rotulo: 'Encarregado', render: (e) => e.encarregadoNome ?? '—' },
    { chave: 'quantidade', rotulo: 'Funcionários', render: (e) => e.quantidadeTrabalhadores },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Equipes cadastradas"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar equipe
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
          aria-label="Equipes cadastradas"
          colunas={colunas}
          linhas={equipes}
          chaveLinha={(e) => e.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma equipe cadastrada ainda',
            acao: { rotulo: 'Adicionar equipe', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(e) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(e.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova equipe"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar equipe
            </Button>
          </>
        }
      >
        {erroPainel && (
          <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
            {erroPainel}
          </FeedbackInline>
        )}
        <FormGrid>
          <Campo span={12}>
            <Field label="Setor" required>
              <SeletorPesquisavel
                placeholder="Selecione o setor"
                opcaoVazia="Selecione o setor"
                opcoes={opcoesSetores}
                valor={novaEquipe.setorId}
                aoMudar={(id) => setNovaEquipe({ ...novaEquipe, setorId: id })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Nome da equipe">
              <Input value={novaEquipe.nome} onChange={(_, d) => setNovaEquipe({ ...novaEquipe, nome: d.value })} />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Encarregado (opcional)">
              <SeletorPesquisavel
                placeholder="Sem encarregado definido"
                opcaoVazia="Sem encarregado definido"
                opcoes={opcoesTrabalhadores}
                valor={novaEquipe.encarregadoId ?? ''}
                aoMudar={(id) => setNovaEquipe({ ...novaEquipe, encarregadoId: id || null })}
              />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
