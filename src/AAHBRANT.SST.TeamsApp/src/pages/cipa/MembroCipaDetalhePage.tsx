import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Campo,
  CampoData,
  Card,
  Carregando,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  Select,
  StatusChip,
  Textarea,
  useConfirmar,
  type Coluna,
} from '@ui';
import { Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  cargoMembroCipaLabel,
  origemMembroCipaLabel,
  CargoMembroCipa,
  type MembroCipaDetalhe,
  type TreinamentoCipa,
} from '../../lib/api';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function treinamentoVazio() {
  return { cargaHoraria: 4, conteudoProgramatico: '', dataRealizacao: '', dataValidade: '', instituicaoInstrutor: '' };
}

export function MembroCipaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [detalhe, setDetalhe] = useState<MembroCipaDetalhe | null>(null);
  const [novoTreinamento, setNovoTreinamento] = useState(treinamentoVazio());
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setDetalhe(await api.cipa.membros.obterDetalhe(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar membro da CIPA.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function definirCargo(cargo: number) {
    if (!id) return;
    try {
      setSalvando(true);
      setErro(null);
      await api.cipa.membros.definirCargo(id, cargo);
      await carregar();
      sucessoToast('Cargo atualizado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao alterar cargo.');
    } finally {
      setSalvando(false);
    }
  }

  async function encerrarMandato() {
    if (!id) return;
    if (!(await confirmar('Encerrar o mandato deste membro? Essa ação não pode ser desfeita.'))) return;
    try {
      setSalvando(true);
      setErro(null);
      await api.cipa.membros.encerrarMandato(id);
      sucessoToast('Mandato encerrado com sucesso.');
      navigate('/operacao/cipa');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar mandato.');
      setSalvando(false);
    }
  }

  async function criarTreinamento() {
    if (!id) return;
    if (!novoTreinamento.dataRealizacao || novoTreinamento.cargaHoraria <= 0) {
      setErro('Preencha data de realização e carga horária.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.cipa.membros.criarTreinamento(
        id,
        novoTreinamento.cargaHoraria,
        novoTreinamento.conteudoProgramatico || null,
        novoTreinamento.dataRealizacao,
        novoTreinamento.dataValidade || null,
        novoTreinamento.instituicaoInstrutor || null,
      );
      setNovoTreinamento(treinamentoVazio());
      await carregar();
      sucessoToast('Treinamento registrado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar treinamento.');
    } finally {
      setSalvando(false);
    }
  }

  async function anexarCertificado(treinamentoId: string, arquivo: File) {
    try {
      setErro(null);
      await api.cipa.membros.anexarCertificado(treinamentoId, arquivo);
      await carregar();
      sucessoToast('Certificado anexado com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao anexar certificado.');
    }
  }

  async function anexarListaPresenca(treinamentoId: string, arquivo: File) {
    try {
      setErro(null);
      await api.cipa.membros.anexarListaPresenca(treinamentoId, arquivo);
      await carregar();
      sucessoToast('Lista de presença anexada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao anexar lista de presença.');
    }
  }

  async function baixarArquivo(treinamentoId: string, tipo: 'certificado' | 'lista-presenca') {
    try {
      setErro(null);
      const blob =
        tipo === 'certificado'
          ? await api.cipa.membros.baixarCertificado(treinamentoId)
          : await api.cipa.membros.baixarListaPresenca(treinamentoId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${tipo}-treinamento-cipa-${treinamentoId}`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar arquivo.');
    }
  }

  if (!id) return <FeedbackInline tom="erro">Membro não encontrado.</FeedbackInline>;

  const colunasTreinamentos: Coluna<TreinamentoCipa>[] = [
    { chave: 'dataRealizacao', rotulo: 'Realização', render: (t) => t.dataRealizacao?.slice(0, 10) ?? '' },
    { chave: 'dataValidade', rotulo: 'Validade', render: (t) => t.dataValidade?.slice(0, 10) ?? '—' },
    { chave: 'cargaHoraria', rotulo: 'Carga horária', render: (t) => `${t.cargaHoraria}h` },
    { chave: 'instituicaoInstrutor', rotulo: 'Instituição/instrutor', render: (t) => t.instituicaoInstrutor ?? '—' },
    {
      chave: 'certificado',
      rotulo: 'Certificado',
      render: (t) =>
        t.temCertificado ? (
          <Button appearance="subtle" onClick={() => baixarArquivo(t.id, 'certificado')}>
            Baixar
          </Button>
        ) : (
          <SeletorFotoCamera
            rotulo="Anexar"
            tamanho="small"
            tiposAceitos="application/pdf,image/*"
            tamanhoMaximoMb={8}
            aoSelecionarArquivo={(arquivo) => anexarCertificado(t.id, arquivo)}
            aoErroValidacao={setErro}
          />
        ),
    },
    {
      chave: 'listaPresenca',
      rotulo: 'Lista de presença',
      render: (t) =>
        t.temListaPresenca ? (
          <Button appearance="subtle" onClick={() => baixarArquivo(t.id, 'lista-presenca')}>
            Baixar
          </Button>
        ) : (
          <SeletorFotoCamera
            rotulo="Anexar"
            tamanho="small"
            tiposAceitos="application/pdf,image/*"
            tamanhoMaximoMb={8}
            aoSelecionarArquivo={(arquivo) => anexarListaPresenca(t.id, arquivo)}
            aoErroValidacao={setErro}
          />
        ),
    },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader titulo="Membro da CIPA" voltarPara="/operacao/cipa" rotuloVoltar="Voltar para CIPA" />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {!detalhe ? (
        <Carregando variante="detalhe" linhas={8} />
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
          <Card
            densidade="compacta"
            titulo={
              <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
                {detalhe.membro.trabalhadorNome}
                {detalhe.membro.mandatoAtivo && <StatusChip tom="ok">Mandato ativo</StatusChip>}
              </div>
            }
            subtitulo={
              <>
                {detalhe.membro.obraNome} · {origemMembroCipaLabel[detalhe.membro.origemMembro]} · Mandato:{' '}
                {detalhe.membro.dataInicioMandato?.slice(0, 10)} a {detalhe.membro.dataFimMandato?.slice(0, 10)}
              </>
            }
          >
            <FormSection titulo="Cargo do Membro" primeira>
              <FormGrid>
                <Campo span={4}>
                  <Field label="Cargo">
                    <Select value={String(detalhe.membro.cargo)} onChange={(_, d) => definirCargo(Number(d.value))} disabled={salvando}>
                      {Object.entries(cargoMembroCipaLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
              </FormGrid>
              {detalhe.membro.cargo !== CargoMembroCipa.Presidente && (
                <FormRodape>
                  <Button appearance="secondary" icon={<Delete24Regular />} onClick={encerrarMandato} disabled={salvando}>
                    Encerrar mandato
                  </Button>
                </FormRodape>
              )}
            </FormSection>
          </Card>

          <Card titulo="Novo treinamento">
            <FormSection titulo="Dados do Treinamento" primeira>
              <FormGrid>
                <Campo span={2}>
                  <Field label="Carga horária (h)" required>
                    <Input
                      type="number"
                      value={String(novoTreinamento.cargaHoraria)}
                      onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, cargaHoraria: Number(d.value) })}
                    />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Data de realização" required>
                    <CampoData
                      value={novoTreinamento.dataRealizacao}
                      onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataRealizacao: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Validade">
                    <CampoData
                      value={novoTreinamento.dataValidade}
                      onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataValidade: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={4}>
                  <Field label="Instituição/instrutor">
                    <Input
                      value={novoTreinamento.instituicaoInstrutor}
                      onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, instituicaoInstrutor: d.value })}
                    />
                  </Field>
                </Campo>
                <Campo span={12}>
                  <Field label="Conteúdo programático">
                    <Textarea
                      value={novoTreinamento.conteudoProgramatico}
                      onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, conteudoProgramatico: d.value })}
                    />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button appearance="primary" onClick={criarTreinamento} disabled={salvando}>
                  Adicionar treinamento
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card titulo="Treinamentos">
            <DataTable
              aria-label="Treinamentos do membro"
              colunas={colunasTreinamentos}
              linhas={detalhe.treinamentos}
              chaveLinha={(t) => t.id}
              vazio={{ titulo: 'Nenhum treinamento registrado ainda.' }}
            />
          </Card>
        </div>
      )}
    </div>
  );
}
