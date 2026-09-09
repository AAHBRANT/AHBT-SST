import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Field,
  Input,
  CampoData,
  Card,
  DataTable,
  PainelLateral,
  FormGrid,
  Campo,
  FeedbackInline,
  SeletorPesquisavel,
  StatusChip,
  nivelVencimento,
  tomDeVencimento,
  rotuloDeVencimento,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Add24Regular, ArrowDownload24Regular, Delete24Regular, Signature24Regular } from '@fluentui/react-icons';
import { api, type CursoTreinamento, type NovoTreinamento, type Treinamento } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { AssinaturaCertificadoTreinamentoDialog } from '../../components/assinatura/AssinaturaCertificadoTreinamentoDialog';

function treinamentoVazio(trabalhadorId: string): NovoTreinamento {
  return {
    trabalhadorId,
    cursoTreinamentoId: '',
    dataRealizacao: '',
    dataValidade: '',
    cargaHorariaRealizada: 0,
    instituicaoInstrutor: '',
    numeroCertificado: '',
    local: '',
    instrutorRegistroProfissional: '',
  };
}

// Sub-aba de TrabalhadorDetalhePage (aba "Treinamentos & DDS"). Camada ui/ (Onda 2, Task 1): Card com
// título de seção + botão "Adicionar treinamento" no `acoes` do Card (não PageHeader — conteúdo
// aninhado); formulário de criação foi para PainelLateral. A situação de vencimento (antes calculada
// à mão em situacaoTreinamento(), mesma regra de 30 dias) passa a usar os helpers
// nivelVencimento/tomDeVencimento/rotuloDeVencimento de @ui (guia de conversão, "Badge→StatusChip"),
// mantendo a data crua ao lado do chip (aprendizado do piloto 1: chip nunca substitui sozinho um
// valor de auditoria). obraId propaga pro AssinaturaCertificadoTreinamentoDialog (AssinaturaQuiosque).
export function TreinamentosTab({ trabalhadorId, obraId }: { trabalhadorId: string; obraId: string }) {
  const navigate = useNavigate();
  const [treinamentos, setTreinamentos] = useState<Treinamento[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [novoTreinamento, setNovoTreinamento] = useState<NovoTreinamento>(() => treinamentoVazio(trabalhadorId));
  const [erro, setErro] = useState<string | null>(null);
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
  const [baixandoId, setBaixandoId] = useState<string | null>(null);
  const [assinaturaAberta, setAssinaturaAberta] = useState<{
    treinamentoId: string;
    cursoNome: string;
    dataRealizacao: string;
    cargaHorariaRealizada: number;
  } | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [lista, listaCursos] = await Promise.all([
        api.treinamentos.listar(trabalhadorId),
        api.cursosTreinamento.listar(),
      ]);
      setTreinamentos(lista);
      setCursos(listaCursos);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar treinamentos.');
    } finally {
      setCarregandoLista(false);
    }
  }

  function nomeCurso(id: string) {
    return cursos.find((c) => c.id === id)?.nome ?? id;
  }

  async function baixarCertificado(id: string) {
    try {
      setBaixandoId(id);
      const blob = await api.treinamentos.baixarCertificado(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `certificado-treinamento-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar o certificado em PDF.');
    } finally {
      setBaixandoId(null);
    }
  }

  useEffect(() => {
    carregar();
    setNovoTreinamento(treinamentoVazio(trabalhadorId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadorId]);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
  }

  async function criar() {
    try {
      setCarregando(true);
      setErroPainel(null);
      const { id } = await api.treinamentos.criar(novoTreinamento);
      setAssinaturaAberta({
        treinamentoId: id,
        cursoNome: nomeCurso(novoTreinamento.cursoTreinamentoId),
        dataRealizacao: novoTreinamento.dataRealizacao,
        cargaHorariaRealizada: novoTreinamento.cargaHorariaRealizada,
      });
      setNovoTreinamento(treinamentoVazio(trabalhadorId));
      await carregar();
      sucessoToast('Treinamento criado com sucesso.');
      fecharPainel();
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar treinamento.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir este treinamento? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.treinamentos.excluir(id);
      await carregar();
      sucessoToast('Treinamento excluído com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir treinamento.');
    }
  }

  const opcoesCursos = cursos.map((c) => ({ id: c.id, rotulo: c.nome }));

  const colunas: Coluna<Treinamento>[] = [
    { chave: 'curso', rotulo: 'Curso', render: (t) => nomeCurso(t.cursoTreinamentoId) },
    { chave: 'realizacao', rotulo: 'Realização', render: (t) => t.dataRealizacao?.slice(0, 10) },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (t) => {
        const nivel = nivelVencimento(t.dataValidade);
        return (
          <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <span>{t.dataValidade?.slice(0, 10) ?? '—'}</span>
            {nivel && <StatusChip tom={tomDeVencimento(nivel)}>{rotuloDeVencimento(nivel)}</StatusChip>}
          </div>
        );
      },
    },
    { chave: 'certificado', rotulo: 'Certificado', render: (t) => t.numeroCertificado },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      <Card
        titulo="Treinamentos do funcionário"
        acoes={
          <Button appearance="primary" icon={<Add24Regular />} onClick={() => setPainelAberto(true)}>
            Adicionar treinamento
          </Button>
        }
      >
        {erro && (
          <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
            {erro}
          </FeedbackInline>
        )}
        <DataTable
          aria-label="Treinamentos do funcionário"
          colunas={colunas}
          linhas={treinamentos}
          chaveLinha={(t) => t.id}
          carregando={carregandoLista}
          vazio={{
            titulo: 'Nenhum treinamento cadastrado ainda',
            acao: { rotulo: 'Adicionar treinamento', aoClicar: () => setPainelAberto(true) },
          }}
          acoesLinha={(t) => (
            <>
              <Button
                appearance="subtle"
                size="small"
                icon={<Signature24Regular />}
                onClick={() => navigate(`/treinamentos/${t.id}/assinar`)}
                aria-label="Assinar certificado"
                title="Assinar certificado"
              />
              <Button
                appearance="subtle"
                size="small"
                icon={<ArrowDownload24Regular />}
                onClick={() => baixarCertificado(t.id)}
                disabled={baixandoId === t.id}
                aria-label="Baixar certificado"
                title="Baixar certificado em PDF"
              />
              <Button
                appearance="subtle"
                size="small"
                icon={<Delete24Regular />}
                onClick={() => excluir(t.id)}
                aria-label="Excluir"
              />
            </>
          )}
        />
      </Card>

      <PainelLateral
        aberto={painelAberto}
        aoFechar={fecharPainel}
        titulo="Novo treinamento"
        rodape={
          <>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" onClick={criar} disabled={carregando}>
              Adicionar treinamento
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
            <Field label="Curso">
              <SeletorPesquisavel
                placeholder="Selecione um curso"
                opcaoVazia="Selecione um curso"
                opcoes={opcoesCursos}
                valor={novoTreinamento.cursoTreinamentoId}
                aoMudar={(id) => setNovoTreinamento({ ...novoTreinamento, cursoTreinamentoId: id })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Data de realização">
              <CampoData
                value={novoTreinamento.dataRealizacao}
                onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataRealizacao: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Validade">
              <CampoData
                value={novoTreinamento.dataValidade}
                onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataValidade: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Carga horária realizada (h)">
              <Input
                type="number"
                value={String(novoTreinamento.cargaHorariaRealizada)}
                onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, cargaHorariaRealizada: Number(d.value) })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Número do certificado">
              <Input
                value={novoTreinamento.numeroCertificado ?? ''}
                onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, numeroCertificado: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Técnico de Segurança do Trabalho (Instrutor/Resp. Técnico)">
              <Input
                value={novoTreinamento.instituicaoInstrutor ?? ''}
                onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, instituicaoInstrutor: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={6}>
            <Field label="Registro profissional do instrutor (CREA/MTE)">
              <Input
                value={novoTreinamento.instrutorRegistroProfissional ?? ''}
                onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, instrutorRegistroProfissional: d.value })}
              />
            </Field>
          </Campo>
          <Campo span={12}>
            <Field label="Local / Instalações (opcional — sem preencher, usa a Obra)">
              <Input
                value={novoTreinamento.local ?? ''}
                onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, local: d.value })}
              />
            </Field>
          </Campo>
        </FormGrid>
      </PainelLateral>

      {assinaturaAberta && (
        <AssinaturaCertificadoTreinamentoDialog
          open
          onClose={() => setAssinaturaAberta(null)}
          treinamentoId={assinaturaAberta.treinamentoId}
          cursoNome={assinaturaAberta.cursoNome}
          dataRealizacao={assinaturaAberta.dataRealizacao}
          cargaHorariaRealizada={assinaturaAberta.cargaHorariaRealizada}
          obraId={obraId}
        />
      )}
    </div>
  );
}
