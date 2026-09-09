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
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusProcessoEleitoralCipaLabel,
  type NovoProcessoEleitoralCipa,
  type Obra,
  type ProcessoEleitoralCipa,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function vazio(): NovoProcessoEleitoralCipa {
  return { obraId: '', dataConvocacao: '', dataInicioInscricoes: '', dataFimInscricoes: '', dataVotacao: '' };
}

// Status do processo (Convocado → InscricoesAbertas → InscricoesEncerradas → VotacaoRealizada →
// Apurado → Encerrado, guia de conversão da Onda 2, seção "Badge→StatusChip"): não é escala de
// severidade, é progressão de workflow — mapeado por julgamento como estágio (neutro/info) até
// concluído (ok), não por posição mecânica.
const tomPorStatus: Record<number, Tom> = {
  1: 'neutro',
  2: 'info',
  3: 'info',
  4: 'info',
  5: 'atencao',
  6: 'ok',
};

// Camada ui/ (Onda 2, Task 4): formulário de convocação saiu para PainelLateral (Guia §2); a linha
// inteira já navega para o detalhe, então o botão "ver" redundante saiu (Guia §1).
export function ProcessoEleitoralCipaTab() {
  const navigate = useNavigate();
  const [lista, setLista] = useState<ProcessoEleitoralCipa[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novo, setNovo] = useState<NovoProcessoEleitoralCipa>(vazio());
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
      const [listaProcessos, listaObras] = await Promise.all([api.cipa.processosEleitorais.listar(), api.obras.listar()]);
      setLista(listaProcessos);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar processos eleitorais.');
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
    if (!novo.obraId || !novo.dataConvocacao || !novo.dataInicioInscricoes || !novo.dataFimInscricoes || !novo.dataVotacao) {
      setErroPainel('Preencha obra e todas as datas do processo.');
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      await api.cipa.processosEleitorais.criar(novo);
      setNovo(vazio());
      await carregar();
      sucessoToast('Processo eleitoral criado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar processo eleitoral.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este processo eleitoral? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.cipa.processosEleitorais.excluir(id);
      await carregar();
      sucessoToast('Processo eleitoral excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir processo eleitoral.');
    }
  }

  const colunas: Coluna<ProcessoEleitoralCipa>[] = [
    { chave: 'obra', rotulo: 'Obra', render: (p) => nomeObra(p.obraId) },
    { chave: 'numeroDocumento', rotulo: 'Nº edital', render: (p) => p.numeroDocumento ?? '—' },
    { chave: 'dataVotacao', rotulo: 'Votação', render: (p) => p.dataVotacao?.slice(0, 10) ?? '' },
    { chave: 'totalCandidatos', rotulo: 'Candidatos' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (p) => <StatusChip tom={tomPorStatus[p.status] ?? 'neutro'}>{statusProcessoEleitoralCipaLabel[p.status]}</StatusChip>,
    },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader
        titulo="Processo Eleitoral CIPA"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Convocar eleição
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
          aria-label="Processos eleitorais"
          colunas={colunas}
          linhas={lista}
          chaveLinha={(p) => p.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum processo eleitoral cadastrado ainda',
            acao: { rotulo: 'Convocar eleição', aoClicar: () => setPainelAberto(true) },
          }}
          aoClicarLinha={(p) => navigate(`/operacao/cipa/eleicao/${p.id}`)}
          acoesLinha={(p) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(p.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Nova convocação de eleição"
        subtitulo="Inscrição de candidatos, avaliação, apuração (manual, sem urna digital) e geração da ata em PDF são feitas na tela de detalhe do processo."
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Convocar eleição
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
            <Field label="Data da convocação" required>
              <CampoData value={novo.dataConvocacao} onChange={(_, d) => setNovo({ ...novo, dataConvocacao: d.value })} />
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Início das inscrições" required>
              <CampoData
                value={novo.dataInicioInscricoes}
                onChange={(_, d) => setNovo({ ...novo, dataInicioInscricoes: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Fim das inscrições" required>
              <CampoData
                value={novo.dataFimInscricoes}
                onChange={(_, d) => setNovo({ ...novo, dataFimInscricoes: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={2}>
            <Field label="Data da votação" required>
              <CampoData value={novo.dataVotacao} onChange={(_, d) => setNovo({ ...novo, dataVotacao: d.value })} />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>
    </div>
  );
}
