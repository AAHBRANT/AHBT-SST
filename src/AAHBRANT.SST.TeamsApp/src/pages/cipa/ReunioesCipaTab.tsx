import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Campo,
  CampoData,
  Card,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  PageHeader,
  PainelLateral,
  Select,
  StatusChip,
  Textarea,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusReuniaoCipaLabel,
  tipoReuniaoCipaLabel,
  TipoReuniaoCipa,
  type NovaReuniaoCipa,
  type Obra,
  type ReuniaoCipa,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function vazio(): NovaReuniaoCipa {
  return { obraId: '', tipo: TipoReuniaoCipa.Ordinaria, dataReuniao: '', pauta: '' };
}

// Status da reunião (Agendada → Realizada → AtaRegistrada, guia de conversão da Onda 2, seção
// "Badge→StatusChip"): progressão de workflow, não severidade — mapeada por julgamento (agendada
// como próximo passo, realizada como pendência de ata, ata registrada como concluído).
const tomPorStatus: Record<number, Tom> = { 1: 'info', 2: 'atencao', 3: 'ok' };

// Camada ui/ (Onda 2, Task 4): formulário de agendamento saiu para PainelLateral (Guia §2); a linha
// inteira já navega para o detalhe, então o botão "ver" redundante saiu (Guia §1).
export function ReunioesCipaTab() {
  const navigate = useNavigate();
  const [lista, setLista] = useState<ReuniaoCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovaReuniaoCipa>(vazio());
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
      const [listaReunioes, listaObras] = await Promise.all([api.cipa.reunioes.listar(), api.obras.listar()]);
      setLista(listaReunioes);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar reuniões da CIPA.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    if (!novo.obraId || !novo.dataReuniao) {
      setErroPainel('Preencha obra e data da reunião.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.cipa.reunioes.criar({ ...novo, pauta: novo.pauta || null });
      setNovo(vazio());
      await carregar();
      sucessoToast('Reunião agendada com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar reunião.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta reunião da CIPA? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.reunioes.excluir(id);
      await carregar();
      sucessoToast('Reunião excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir reunião.');
    }
  }

  const colunas: Coluna<ReuniaoCipa>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (r) => nomeObra(r.obraId) },
    { chave: 'tipo', rotulo: 'Tipo', render: (r) => tipoReuniaoCipaLabel[r.tipo] },
    { chave: 'dataReuniao', rotulo: 'Data', render: (r) => r.dataReuniao?.slice(0, 10) ?? '' },
    { chave: 'presentes', rotulo: 'Presentes', render: (r) => `${r.totalPresentes}/${r.totalParticipantes}` },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (r) => <StatusChip tom={tomPorStatus[r.status] ?? 'neutro'}>{statusReuniaoCipaLabel[r.status]}</StatusChip>,
    },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Reuniões CIPA"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Agendar reunião
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
          aria-label="Reuniões"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(r) => r.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhuma reunião da CIPA cadastrada ainda',
            acao: { rotulo: 'Agendar reunião', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(r) => navigate(`/operacao/cipa/reuniao/${r.id}`)}
          acoesLinha={(r) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(r.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Agendar reunião"
        subtitulo="Lista de presença, deliberações e o plano de ações (matriz 5W2H) da reunião são registrados na tela de detalhe."
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Agendar reunião
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
          <Campo span={4}>
            <Field label="Obra" required>
              <Select value={novo.obraId} onChange={(_, d) => setNovo({ ...novo, obraId: d.value })}>
                <option value="">Selecione</option>
                {obras.map((obra) => (
                  <option key={obra.id} value={obra.id}>
                    {obra.nome}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Tipo">
              <Select value={String(novo.tipo)} onChange={(_, d) => setNovo({ ...novo, tipo: Number(d.value) })}>
                {Object.entries(tipoReuniaoCipaLabel).map(([valor, rotulo]) => (
                  <option key={valor} value={valor}>
                    {rotulo}
                  </option>
                ))}
              </Select>
            </Field>
          </Campo>
          <Campo span={3}>
            <Field label="Data da reunião" required>
              <CampoData value={novo.dataReuniao} onChange={(_, d) => setNovo({ ...novo, dataReuniao: d.value })} />
            </Field>
          </Campo>
          <Campo span={12}>
            <Field label="Pauta">
              <Textarea value={novo.pauta ?? ''} onChange={(_, d) => setNovo({ ...novo, pauta: d.value })} />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
