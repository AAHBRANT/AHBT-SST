import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  Card,
  CampoData,
  ChipCheckboxGroup,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular, PeopleTeamAdd24Regular } from '@fluentui/react-icons';
import { api, StatusApr, statusAprLabel, type Apr, type Atividade, type Equipe, type NovaApr, type Trabalhador } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { hojeIso } from '../../lib/datas';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

function aprVazia(): NovaApr {
  return {
    atividadeId: '',
    local: '',
    maquinasEquipamentos: '',
    pgrReferencia: '',
    equipeId: null,
    data: hojeIso(),
    validade: null,
    responsaveisIds: [],
  };
}

// Mapeamento 1:1 pelo nome semântico do Fluent (Guia de conversão item 5), preservando as mesmas
// cores da versão anterior (Badge color=): informative→info, warning→atencao, success→ok,
// danger→alerta, subtle→neutro. Reutilizado em AprDetalhePage.tsx para lista e detalhe ficarem
// visualmente coerentes.
const tomPorStatusApr: Record<number, Tom> = {
  [StatusApr.EmElaboracao]: 'neutro',
  [StatusApr.AguardandoAprovacao]: 'atencao',
  [StatusApr.Aprovada]: 'ok',
  [StatusApr.Reprovada]: 'alerta',
  [StatusApr.Encerrada]: 'info',
};

// Onda 2 Task 11 (camada ui/): lista + formulário de criação de APR. Nada de Fluent cru nem de
// pageStyles aqui — lista em DataTable, seleção de responsáveis em ChipCheckboxGroup.
export function AprsTab() {
  const souAdministrador = useSouAdministrador();
  const navigate = useNavigate();
  const [aprs, setAprs] = useState<Apr[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaApr, setNovaApr] = useState<NovaApr>(aprVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [montandoEquipe, setMontandoEquipe] = useState(false);
  const [nomeNovaEquipe, setNomeNovaEquipe] = useState('');
  const [encarregadoNovaEquipe, setEncarregadoNovaEquipe] = useState('');
  const [salvandoEquipe, setSalvandoEquipe] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, ativs, trabs, equips] = await Promise.all([
        api.aprs.listar(),
        api.atividades.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
      ]);
      setAprs(lista);
      setAtividades(ativs);
      setTrabalhadores(trabs);
      setEquipes(equips);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar APRs.');
    } finally {
      setCarregandoLista(false);
    }
  }

  const obraIdAtividadeSelecionada = atividades.find((a) => a.id === novaApr.atividadeId)?.obraId;
  const equipesDaObra = obraIdAtividadeSelecionada
    ? equipes.filter((e) => e.obraId === obraIdAtividadeSelecionada)
    : equipes;

  useEffect(() => {
    carregar();
  }, []);

  // Equipe na APR (24/09/2026): escolher a equipe marca os membros dela como responsáveis — dá
  // para desmarcar ou incluir alguém depois. "Nenhuma" não mexe na seleção.
  function escolherEquipe(equipeId: string) {
    const equipe = equipes.find((e) => e.id === equipeId);
    setNovaApr((atual) => ({
      ...atual,
      equipeId: equipeId || null,
      responsaveisIds: equipe ? [...equipe.trabalhadorIds] : atual.responsaveisIds,
    }));
  }

  const responsaveisSelecionados = trabalhadores.filter((t) => novaApr.responsaveisIds.includes(t.id));
  // Trabalhador pertence a uma equipe só: quem já está em outra será movido para a nova.
  const seraoMovidos = responsaveisSelecionados.filter((t) => t.equipeId);

  function abrirMontagemEquipe() {
    setNomeNovaEquipe('');
    setEncarregadoNovaEquipe('');
    setMontandoEquipe(true);
  }

  async function salvarEquipe() {
    if (!obraIdAtividadeSelecionada) return;
    try {
      setSalvandoEquipe(true);
      setErro(null);
      const { id } = await api.equipes.criarComMembros({
        obraId: obraIdAtividadeSelecionada,
        nome: nomeNovaEquipe.trim(),
        encarregadoId: encarregadoNovaEquipe || null,
        trabalhadorIds: novaApr.responsaveisIds,
      });
      const [equips, trabs] = await Promise.all([api.equipes.listar(), api.trabalhadores.listar()]);
      setEquipes(equips);
      setTrabalhadores(trabs);
      setNovaApr((atual) => ({ ...atual, equipeId: id }));
      setMontandoEquipe(false);
      sucessoToast(`Equipe "${nomeNovaEquipe.trim()}" criada.`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar a equipe.');
    } finally {
      setSalvandoEquipe(false);
    }
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.aprs.criar({
        ...novaApr,
        validade: novaApr.validade || null,
      });
      setNovaApr(aprVazia());
      await carregar();
      sucessoToast('APR criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar APR.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta APR? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.aprs.excluir(id);
      await carregar();
      sucessoToast('APR excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir APR.');
    }
  }

  const colunas: Coluna<Apr>[] = [
    { chave: 'numeroApr', rotulo: 'Nº APR', render: (a) => a.numeroApr ?? '-' },
    { chave: 'atividadeNome', rotulo: 'Atividade' },
    { chave: 'local', rotulo: 'Local' },
    { chave: 'data', rotulo: 'Data', render: (a) => a.data?.slice(0, 10) ?? '' },
    { chave: 'validade', rotulo: 'Validade', render: (a) => a.validade?.slice(0, 10) ?? '' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => <StatusChip tom={tomPorStatusApr[a.status] ?? 'neutro'}>{statusAprLabel[a.status]}</StatusChip>,
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <Card titulo="Análise Preliminar de Risco (APR)">
        <FormSection titulo="Dados da APR" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Atividade">
                <Select
                  value={novaApr.atividadeId}
                  onChange={(_, d) => setNovaApr({ ...novaApr, atividadeId: d.value })}
                >
                  <option value="">Selecione</option>
                  {atividades.map((atividade) => (
                    <option key={atividade.id} value={atividade.id}>
                      {atividade.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Local / Frente">
                <Input value={novaApr.local} onChange={(_, d) => setNovaApr({ ...novaApr, local: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Máquinas / Equip.">
                <Input
                  value={novaApr.maquinasEquipamentos ?? ''}
                  onChange={(_, d) => setNovaApr({ ...novaApr, maquinasEquipamentos: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="PGR / Procedimento ref.">
                <Input
                  value={novaApr.pgrReferencia ?? ''}
                  onChange={(_, d) => setNovaApr({ ...novaApr, pgrReferencia: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Equipe">
                <Select
                  value={novaApr.equipeId ?? ''}
                  onChange={(_, d) => escolherEquipe(d.value)}
                >
                  <option value="">Nenhuma</option>
                  {equipesDaObra.map((equipe) => (
                    <option key={equipe.id} value={equipe.id}>
                      {equipe.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data">
                <CampoData value={novaApr.data} onChange={(_, d) => setNovaApr({ ...novaApr, data: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade">
                <CampoData
                  value={novaApr.validade ?? ''}
                  onChange={(_, d) => setNovaApr({ ...novaApr, validade: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              {!montandoEquipe ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                  <Button
                    icon={<PeopleTeamAdd24Regular />}
                    onClick={abrirMontagemEquipe}
                    disabled={!obraIdAtividadeSelecionada || novaApr.responsaveisIds.length === 0}
                  >
                    Salvar seleção como equipe
                  </Button>
                  <span style={{ fontSize: 12, opacity: 0.75 }}>
                    {!obraIdAtividadeSelecionada
                      ? 'Escolha a atividade para saber de qual obra é a equipe.'
                      : novaApr.responsaveisIds.length === 0
                        ? 'Marque os responsáveis que formam a equipe.'
                        : `${novaApr.responsaveisIds.length} responsável(is) marcado(s).`}
                  </span>
                </div>
              ) : (
                <FormGrid>
                  <Campo span={4}>
                    <Field label="Nome da equipe" required>
                      <Input
                        value={nomeNovaEquipe}
                        onChange={(_, d) => setNomeNovaEquipe(d.value)}
                        placeholder="Ex.: Armação – Frente 2"
                      />
                    </Field>
                  </Campo>
                  <Campo span={4}>
                    <Field label="Encarregado">
                      <Select value={encarregadoNovaEquipe} onChange={(_, d) => setEncarregadoNovaEquipe(d.value)}>
                        <option value="">Sem encarregado definido</option>
                        {responsaveisSelecionados.map((t) => (
                          <option key={t.id} value={t.id}>
                            {t.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                  <Campo span={12}>
                    <span style={{ fontSize: 12 }}>
                      {responsaveisSelecionados.length} membro(s): {responsaveisSelecionados.map((t) => t.nome).join(', ')}.
                      {seraoMovidos.length > 0 &&
                        ` ${seraoMovidos.length} já está(ão) em outra equipe e será(ão) movido(s) para esta.`}
                    </span>
                  </Campo>
                  <Campo span={12}>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <Button
                        appearance="primary"
                        onClick={salvarEquipe}
                        disabled={salvandoEquipe || nomeNovaEquipe.trim() === ''}
                      >
                        Criar equipe
                      </Button>
                      <Button onClick={() => setMontandoEquipe(false)} disabled={salvandoEquipe}>
                        Cancelar
                      </Button>
                    </div>
                  </Campo>
                </FormGrid>
              )}
            </Campo>
            <Campo span={12}>
              <Field label="Responsáveis">
                <ChipCheckboxGroup
                  aria-label="Responsáveis"
                  opcoes={trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome }))}
                  selecionados={novaApr.responsaveisIds}
                  aoMudar={(atualizar) => setNovaApr((atual) => ({ ...atual, responsaveisIds: atualizar(atual.responsaveisIds) }))}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar APR
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="APRs cadastradas"
          colunas={colunas}
          linhas={aprs}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhuma APR cadastrada ainda.' }}
          aoClicarLinha={(a) => navigate(`/operacao/apr/${a.id}`)}
          acoesLinha={(a) => (
            <>
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais) — ver PoliticasAutorizacao. */}
              {souAdministrador && (
                <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(a.id)} aria-label="Excluir" />
              )}
            </>
          )}
        />
      </Card>
      {dialogElement}
    </>
  );
}
