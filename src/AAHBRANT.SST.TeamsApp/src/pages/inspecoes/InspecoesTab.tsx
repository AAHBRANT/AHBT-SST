import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
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
  PageHeader,
  PainelCriacaoInline,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { hojeIso } from '../../lib/datas';
import {
  api,
  statusInspecaoLabel,
  tipoInspecaoLabel,
  type Atividade,
  type ChecklistModelo,
  type Inspecao,
  type NovaInspecao,
  type Obra,
  type Usuario,
} from '../../lib/api';

function criarInspecaoVazia(): NovaInspecao {
  return {
    checklistModeloId: '',
    obraId: '',
    atividadeId: null,
    data: hojeIso(),
    responsavelUsuarioId: '',
  };
}

// EmAndamento/Concluida (StatusInspecao) — mesmo julgamento do guia item 5: em andamento ainda não
// terminou (info), concluída é o estado positivo (ok).
const tomPorStatusInspecao: Record<number, Tom> = {
  1: 'info',
  2: 'ok',
};

// Onda D do spec de formulário inline (2026-09-11): o PainelLateral (drawer) saiu — mesmo padrão
// da Onda A/AtividadesTab.tsx. Agora é um PainelCriacaoInline, que cresce acima da lista só até a
// altura do próprio formulário, em vez de cobrir a tela com uma gaveta. O botão "Ver inspeção" que
// só repetia a navegação da linha (guia item 1) sai — a linha inteira já navega via aoClicarLinha.
export function InspecoesTab() {
  const navigate = useNavigate();
  const [inspecoes, setInspecoes] = useState<Inspecao[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [checklists, setChecklists] = useState<ChecklistModelo[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaInspecao, setNovaInspecao] = useState<NovaInspecao>(() => criarInspecaoVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras, listaAtividades, listaChecklists, listaUsuarios] = await Promise.all([
        api.inspecoes.listar(),
        api.obras.listar(),
        api.atividades.listar(),
        api.checklistModelos.listar(),
        api.usuarios.listar(),
      ]);
      setInspecoes(lista);
      setObras(listaObras);
      setAtividades(listaAtividades);
      setChecklists(listaChecklists);
      setUsuarios(listaUsuarios);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeções.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const atividadesDaObra = atividades.filter((a) => a.obraId === novaInspecao.obraId);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovaInspecao(criarInspecaoVazia());
  }

  async function criar() {
    if (!novaInspecao.checklistModeloId || !novaInspecao.obraId || !novaInspecao.data || !novaInspecao.responsavelUsuarioId) {
      setErroPainel('Preencha checklist, obra, data e responsável.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.inspecoes.criar({
        ...novaInspecao,
        atividadeId: novaInspecao.atividadeId || null,
      });
      fecharPainel();
      await carregar();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar inspeção.');
    } finally {
      setCarregando(false);
    }
  }

  const colunas: Coluna<Inspecao>[] = [
    { chave: 'tipoInspecao', rotulo: 'Tipo', render: (i) => tipoInspecaoLabel[i.tipoInspecao] },
    { chave: 'obraNome', rotulo: 'Obra' },
    {
      chave: 'checklist',
      rotulo: 'Checklist',
      render: (i) => `${i.checklistModeloNome} (v${i.checklistModeloVersao})`,
    },
    { chave: 'data', rotulo: 'Data', render: (i) => i.data?.slice(0, 10) ?? '' },
    { chave: 'responsavelUsuarioNome', rotulo: 'Responsável' },
    {
      chave: 'progresso',
      rotulo: 'Progresso',
      render: (i) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 6, flexWrap: 'wrap' }}>
          <span>
            {i.itensRespondidos}/{i.totalItens}
          </span>
          {i.itensNaoConformes > 0 && <StatusChip tom="alerta">{i.itensNaoConformes} NC</StatusChip>}
        </div>
      ),
    },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (i) => <StatusChip tom={tomPorStatusInspecao[i.status] ?? 'neutro'}>{statusInspecaoLabel[i.status]}</StatusChip>,
    },
  ];

  // Exclusão definitiva, só para Administrador. O servidor recusa quando a inspeção já gerou não
  // conformidade — nesse caso a mensagem dele diz quantas são, e é ela que aparece na tela.
  async function excluir(inspecao: Inspecao) {
    const confirmado = await confirmar({
      titulo: 'Excluir inspeção',
      mensagem:
        'Excluir esta inspeção e todas as respostas do checklist dela?' +
        ' Esta ação não pode ser desfeita.',
      rotuloConfirmar: 'Excluir',
    });
    if (!confirmado) return;
    try {
      setExcluindoId(inspecao.id);
      setErro(null);
      await api.inspecoes.excluir(inspecao.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir a inspeção.');
    } finally {
      setExcluindoId(null);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <PageHeader
        titulo="Inspeções"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-inspecao"
          >
            {painelAberto ? 'Fechar' : 'Nova inspeção'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div id="painel-nova-inspecao">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova inspeção">
          <FormSection titulo="Dados da inspeção" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={12}>
                <Field label="Checklist">
                  <Select
                    value={novaInspecao.checklistModeloId}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, checklistModeloId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {checklists.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.nome} (v{c.versao} — {tipoInspecaoLabel[c.tipoInspecao]})
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Obra">
                  <Select
                    value={novaInspecao.obraId}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, obraId: d.value, atividadeId: null })}
                  >
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Atividade (opcional)">
                  <Select
                    value={novaInspecao.atividadeId ?? ''}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, atividadeId: d.value || null })}
                    disabled={!novaInspecao.obraId}
                  >
                    <option value="">Nenhuma</option>
                    {atividadesDaObra.map((atividade) => (
                      <option key={atividade.id} value={atividade.id}>
                        {atividade.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Data">
                  <CampoData
                    value={novaInspecao.data}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, data: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Responsável">
                  <Select
                    value={novaInspecao.responsavelUsuarioId}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, responsavelUsuarioId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {usuarios.map((usuario) => (
                      <option key={usuario.id} value={usuario.id}>
                        {usuario.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando}>
                Iniciar inspeção
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card>
        <DataTable
          aria-label="Inspeções"
          colunas={colunas}
          linhas={inspecoes}
          chaveLinha={(i) => i.id}
          carregando={carregandoLista}
          aoClicarLinha={(i) => navigate(`/prevencao/inspecoes/${i.id}`)}
          acoesLinha={(i) => (
            <>
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais) — ver PoliticasAutorizacao. */}
              {souAdministrador && (
                <Button
                  appearance="subtle"
                  size="small"
                  icon={<Delete24Regular />}
                  disabled={excluindoId === i.id}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    excluir(i);
                  }}
                  aria-label="Excluir inspeção"
                  title="Excluir esta inspeção (somente administrador)"
                />
              )}
            </>
          )}
          vazio={{
            titulo: 'Nenhuma inspeção iniciada ainda.',
            descricao: 'Inicie a primeira inspeção para começar.',
            acao: { rotulo: 'Nova inspeção', aoClicar: () => setPainelAberto(true) },
          }}
        />
      </Card>
    </div>
  );
}
