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
  Input,
  Select,
  StatusChip,
  nivelVencimento,
  rotuloDeVencimento,
  tomDeVencimento,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  statusPcmsoDocumentoLabel,
  type NovoPcmso,
  type Obra,
  type Pcmso,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function pcmsoVazio(): NovoPcmso {
  return {
    nome: '',
    versao: '',
    validade: '',
    dataEmissao: '',
    responsavelUsuarioId: '',
    obraId: '',
    setorId: '',
    arquivo: '',
    medicoResponsavelNome: '',
    medicoResponsavelCrm: '',
    funcoesContempladas: '',
    riscosConsiderados: '',
    examesPrevistos: '',
    periodicidades: '',
    unidadesObrasAbrangidas: '',
  };
}

// StatusPcmsoDocumento: Rascunho=1, EmAprovacao=2, Vigente=3, Obsoleto=4, Cancelado=5.
const tomPorStatusPcmso: Record<number, Tom> = { 1: 'neutro', 2: 'atencao', 3: 'ok', 4: 'neutro', 5: 'alerta' };

function chipVencimento(data?: string | null) {
  const nivel = nivelVencimento(data);
  return nivel ? <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip> : null;
}

// Onda 2 Task 12 (camada ui/): edição completa dos campos clínicos e o Plano de Ação vinculado ficam
// em PcmsoDetalhePage.tsx (mesmo padrão de navegação lista→detalhe usado por PgrsTab.tsx e
// NaoConformidadesTab.tsx).
export function PcmsoTab() {
  const navigate = useNavigate();
  const [pcmsos, setPcmsos] = useState<Pcmso[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [novoPcmso, setNovoPcmso] = useState<NovoPcmso>(pcmsoVazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaObras] = await Promise.all([api.pcmsos.listar(), api.obras.listar()]);
      setPcmsos(lista);
      setObras(listaObras);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar PCMSOs.');
    } finally {
      setCarregandoLista(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  function nomeObra(id?: string | null) {
    if (!id) return '—';
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  async function criar() {
    if (!novoPcmso.nome.trim() || !novoPcmso.dataEmissao) {
      setErro('Preencha nome e data de emissão.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      await api.pcmsos.criar({
        ...novoPcmso,
        versao: novoPcmso.versao || null,
        validade: novoPcmso.validade || null,
        responsavelUsuarioId: novoPcmso.responsavelUsuarioId || null,
        obraId: novoPcmso.obraId || null,
        setorId: novoPcmso.setorId || null,
      });
      setNovoPcmso(pcmsoVazio());
      await carregar();
      sucessoToast('PCMSO criado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar PCMSO.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este PCMSO? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.pcmsos.excluir(id);
      await carregar();
      sucessoToast('PCMSO excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir PCMSO.');
    }
  }

  const colunas: Coluna<Pcmso>[] = [
    { chave: 'numeroDocumento', rotulo: 'Nº do documento', render: (p) => p.numeroDocumento ?? '-' },
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'obra', rotulo: 'Obra', render: (p) => nomeObra(p.obraId) },
    { chave: 'emissao', rotulo: 'Emissão', render: (p) => p.dataEmissao?.slice(0, 10) ?? '' },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (p) => (
        <>
          {p.validade?.slice(0, 10) ?? '—'} {chipVencimento(p.validade)}
        </>
      ),
    },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (p) => (
        <StatusChip tom={tomPorStatusPcmso[p.status] ?? 'neutro'}>{statusPcmsoDocumentoLabel[p.status]}</StatusChip>
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

      <Card titulo="PCMSOs">
        <FormSection titulo="Novo PCMSO" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Nome do Documento" required>
                <Input value={novoPcmso.nome} onChange={(_, d) => setNovoPcmso({ ...novoPcmso, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Versão">
                <Input
                  value={novoPcmso.versao ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, versao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Obra">
                <Select
                  value={novoPcmso.obraId ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, obraId: d.value, setorId: '' })}
                >
                  <option value="">Nenhuma</option>
                  {obras.map((obra) => (
                    <option key={obra.id} value={obra.id}>
                      {obra.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data de emissão" required>
                <CampoData
                  value={novoPcmso.dataEmissao}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, dataEmissao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade">
                <CampoData
                  value={novoPcmso.validade ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, validade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={5}>
              <Field label="Médico responsável">
                <Input
                  value={novoPcmso.medicoResponsavelNome ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, medicoResponsavelNome: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape info="Os demais campos (CRM, funções/riscos/exames contemplados, periodicidades, unidades abrangidas, status e Plano de Ação) são preenchidos na tela de detalhe, após criar o registro.">
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar PCMSO
            </Button>
          </FormRodape>
        </FormSection>

        <DataTable
          aria-label="PCMSOs cadastrados"
          colunas={colunas}
          linhas={pcmsos}
          chaveLinha={(p) => p.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum PCMSO cadastrado ainda.' }}
          aoClicarLinha={(p) => navigate(`/saude-ocupacional/pcmso/${p.id}`)}
          acoesLinha={(p) => (
            <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluir(p.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      {dialogElement}
    </>
  );
}
