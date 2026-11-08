import { useEffect, useMemo, useState } from 'react';
import { gravar, lerLembrada } from '../../hooks/useObraLembrada';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
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
import { Add24Regular, Delete24Regular, DocumentPdf24Regular } from '@fluentui/react-icons';
import {
  api,
  statusPcmsoDocumentoLabel,
  StatusObra,
  type NovoPcmso,
  type Obra,
  type Pcmso,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { CartoesVigentesPorObra, type CartaoDocumentoObra } from '../../components/documentos-obra/CartoesVigentesPorObra';
import { BarraObraAcoes } from '../../components/documentos-obra/BarraObraAcoes';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { DICA_IA, EnvioPdfComIa, dataCampo, sugerirObra } from '../../components/leitura-ia/EnvioPdfComIa';

function pcmsoVazio(): NovoPcmso {
  return {
    nome: '',
    versao: '',
    validade: '',
    dataEmissao: '',
    responsavelUsuarioId: '',
    obraId: '',
    setorId: '',
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
  // Obra lembrada entre as telas da Gestão de SST (08/11/2026).
  const [obraFiltro, setObraFiltroBase] = useState(() => lerLembrada());
  const setObraFiltro = (id: string) => {
    setObraFiltroBase(id);
    gravar(id);
  };
  const [mostrarForm, setMostrarForm] = useState(false);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [novoPcmso, setNovoPcmso] = useState<NovoPcmso>(pcmsoVazio());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  // Preenchimento pelo PDF (16/10/2026): campos que vieram da IA ganham a dica "confira".
  const [arquivo, setArquivo] = useState<File | null>(null);
  const [lendoIa, setLendoIa] = useState(false);
  const [camposIa, setCamposIa] = useState<Set<keyof NovoPcmso>>(new Set());
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

  async function lerPdf(f: File) {
    setArquivo(f);
    setCamposIa(new Set());
    try {
      setLendoIa(true);
      setErro(null);
      const c = await api.leiturasIa.cabecalhoPcmso(f);
      const novo: Partial<NovoPcmso> = {};
      const obraId = sugerirObra(`${c.obra ?? ''} ${c.nome ?? ''}`, obras);
      if (obraId) novo.obraId = obraId;
      if (c.nome) novo.nome = c.nome;
      if (c.versao) novo.versao = c.versao;
      if (c.dataEmissao) novo.dataEmissao = dataCampo(c.dataEmissao);
      if (c.validade) novo.validade = dataCampo(c.validade);
      if (c.medicoNome) novo.medicoResponsavelNome = c.medicoNome;
      if (c.medicoCrm) novo.medicoResponsavelCrm = c.medicoCrm;
      setNovoPcmso((atual) => ({ ...atual, ...novo }));
      setCamposIa(new Set(Object.keys(novo) as (keyof NovoPcmso)[]));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao ler o PDF com IA. Preencha os campos manualmente.');
    } finally {
      setLendoIa(false);
    }
  }

  const dica = (campo: keyof NovoPcmso) => (camposIa.has(campo) ? DICA_IA : undefined);

  function limpar() {
    setNovoPcmso(pcmsoVazio());
    setArquivo(null);
    setCamposIa(new Set());
  }

  async function criar() {
    if (!novoPcmso.nome.trim() || !novoPcmso.dataEmissao) {
      setErro('Preencha nome e data de emissão.');
      return;
    }
    try {
      setCarregando(true);
      setErro(null);
      const { id } = await api.pcmsos.criar({
        ...novoPcmso,
        versao: novoPcmso.versao || null,
        validade: novoPcmso.validade || null,
        responsavelUsuarioId: novoPcmso.responsavelUsuarioId || null,
        obraId: novoPcmso.obraId || null,
        setorId: novoPcmso.setorId || null,
      });
      if (arquivo) {
        // O PDF fica no PCMSO; com obra, já entra na fila da leitura completa (quadros de exames).
        await api.pcmsos.enviarDocumento(id, arquivo);
        const temObra = !!novoPcmso.obraId;
        if (temObra) await api.leiturasIa.iniciar('pcmso', id).catch(() => undefined);
        limpar();
        sucessoToast(temObra ? 'PCMSO criado. A IA está lendo o documento completo.' : 'PCMSO criado com o PDF anexado.');
        navigate(`/saude-ocupacional/pcmso/${id}`);
        return;
      }
      limpar();
      setMostrarForm(false);
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

  function abrirPdf(p: Pcmso) {
    void visualizar({
      titulo: p.nome,
      nomeArquivo: `${p.nome}.pdf`,
      obter: async () => {
        const blob = await api.pcmsos.obterDocumento(p.id);
        if (!blob) throw new Error('Este PCMSO não tem PDF anexado.');
        return blob;
      },
    });
  }

  // PCMSO vigente de cada obra (06/11/2026, mesmo padrão do PGR); obra em andamento sem vigente fica laranja.
  const cartoes = useMemo<CartaoDocumentoObra[]>(() => {
    const obraOk = (obraId?: string | null) => !obraFiltro || obraId === obraFiltro;
    const nome = (id?: string | null) => (id ? obras.find((o) => o.id === id)?.nome ?? '' : 'Sem obra vinculada');
    const vigentes: CartaoDocumentoObra[] = pcmsos
      .filter((p) => p.status === 3 && obraOk(p.obraId))
      .map((p) => ({
        chave: p.id,
        titulo: nome(p.obraId),
        obra: p.nome,
        tags: [{ rotulo: 'Vigente', tom: 'ok' as const }],
        vigencia: { inicio: p.dataEmissao, fim: p.validade },
        linhas: [
          {
            rotulo: 'Médico',
            valor: p.medicoResponsavelNome
              ? `${p.medicoResponsavelNome}${p.medicoResponsavelCrm ? ` · CRM ${p.medicoResponsavelCrm}` : ''}`
              : '—',
          },
        ],
        acao: { rotulo: 'Abrir PDF', aoClicar: () => abrirPdf(p) },
        acaoExtra: { rotulo: 'Ver PCMSO completo →', aoClicar: () => navigate(`/saude-ocupacional/pcmso/${p.id}`) },
      }))
      .sort((a, b) => a.obra.localeCompare(b.obra, 'pt-BR'));
    const comVigente = new Set(pcmsos.filter((p) => p.status === 3).map((p) => p.obraId));
    const pendentes: CartaoDocumentoObra[] = obras
      .filter((o) => o.status === StatusObra.EmAndamento && !comVigente.has(o.id) && obraOk(o.id))
      .map((o) => ({
        chave: `sem-${o.id}`,
        titulo: 'Sem PCMSO vigente',
        obra: o.nome,
        pendente: true,
        tags: [{ rotulo: 'Pendente', tom: 'atencao' as const }],
        linhas: [{ rotulo: 'Próximo passo', valor: 'Enviar o escopo ao médico' }],
        acao: {
          rotulo: 'Escopo para o médico',
          secundaria: true,
          aoClicar: () => navigate('/gestao-sst?secao=pcmso&aba=escopo-pcmso'),
        },
      }))
      .sort((a, b) => a.obra.localeCompare(b.obra, 'pt-BR'));
    return [...vigentes, ...pendentes];
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pcmsos, obras, obraFiltro]);

  // O vigente já está no cartão de cima; aqui rascunhos, em aprovação, obsoletos e cancelados.
  const pcmsosFiltrados = pcmsos.filter((p) => p.status !== 3 && (!obraFiltro || p.obraId === obraFiltro));

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

      <BarraObraAcoes obras={obras} obraId={obraFiltro} aoMudarObra={setObraFiltro}>
          <Button appearance="primary" icon={<DocumentPdf24Regular />} onClick={() => setMostrarForm(true)} disabled={mostrarForm}>
            Importar PCMSO em PDF
          </Button>
      </BarraObraAcoes>

      {mostrarForm && (
      <Card titulo="Importar PCMSO existente (PDF)" subtitulo="PCMSO elaborado pelo médico. A IA lê o arquivo e preenche os campos para você conferir.">
        <FormSection titulo="Enviar o PDF do PCMSO" numero={1} primeira>
          <EnvioPdfComIa arquivo={arquivo} lendo={lendoIa} lido={camposIa.size > 0} aoEscolher={lerPdf} aoRemover={() => setArquivo(null)} />
        </FormSection>
        <FormSection titulo="Dados do PCMSO" numero={2}>
          <FormGrid>
            <Campo span={4}>
              <Field label="Nome do Documento" required hint={dica('nome')}>
                <Input value={novoPcmso.nome} onChange={(_, d) => setNovoPcmso({ ...novoPcmso, nome: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Versão" hint={dica('versao')}>
                <Input
                  value={novoPcmso.versao ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, versao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Obra" hint={dica('obraId')}>
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
              <Field label="Data de emissão" required hint={dica('dataEmissao')}>
                <CampoData
                  value={novoPcmso.dataEmissao}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, dataEmissao: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade" hint={dica('validade')}>
                <CampoData
                  value={novoPcmso.validade ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, validade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Médico responsável" hint={dica('medicoResponsavelNome')}>
                <Input
                  value={novoPcmso.medicoResponsavelNome ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, medicoResponsavelNome: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="CRM" hint={dica('medicoResponsavelCrm')}>
                <Input
                  value={novoPcmso.medicoResponsavelCrm ?? ''}
                  onChange={(_, d) => setNovoPcmso({ ...novoPcmso, medicoResponsavelCrm: d.value })}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape info="Os demais campos (funções/riscos/exames contemplados, periodicidades, unidades abrangidas, status e Plano de Ação) são preenchidos na tela de detalhe, após criar o registro.">
            <Button onClick={() => { limpar(); setMostrarForm(false); }} disabled={carregando || lendoIa}>
              Cancelar
            </Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando || lendoIa}>
              Salvar PCMSO
            </Button>
          </FormRodape>
        </FormSection>
      </Card>
      )}

      <CartoesVigentesPorObra
        titulo="PCMSO vigente por obra"
        cartoes={cartoes}
        carregando={carregandoLista}
        vazio="Nenhum PCMSO vigente nem obra em andamento para esta seleção."
      />

      {pcmsosFiltrados.length > 0 && (
      <Card titulo="Outros PCMSOs" subtitulo="Rascunhos, em aprovação e histórico">

        <DataTable
          aria-label="PCMSOs cadastrados"
          colunas={colunas}
          linhas={pcmsosFiltrados}
          chaveLinha={(p) => p.id}
          carregando={carregandoLista}
          vazio={{ titulo: 'Nenhum PCMSO cadastrado ainda.' }}
          aoClicarLinha={(p) => navigate(`/saude-ocupacional/pcmso/${p.id}`)}
          acoesLinha={(p) => (
            <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={() => excluir(p.id)} aria-label="Excluir" />
          )}
        />
      </Card>
      )}
      {dialogElement}
      {dialogoVisualizador}
    </>
  );
}
