import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
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
  Text,
  type Coluna,
  type Tom,
} from '@ui';
import { DocumentPdf24Regular } from '@fluentui/react-icons';
import {
  api,
  statusCandidatoCipaLabel,
  statusProcessoEleitoralCipaLabel,
  StatusCandidatoCipa,
  StatusProcessoEleitoralCipa,
  type CandidatoCipa,
  type ProcessoEleitoralCipaDetalhe,
  type Trabalhador,
  type VotoApuradoCipa,
} from '../../lib/api';

const tomPorStatusProcesso: Record<number, Tom> = {
  [StatusProcessoEleitoralCipa.Convocado]: 'neutro',
  [StatusProcessoEleitoralCipa.InscricoesAbertas]: 'info',
  [StatusProcessoEleitoralCipa.InscricoesEncerradas]: 'atencao',
  [StatusProcessoEleitoralCipa.VotacaoRealizada]: 'atencao',
  [StatusProcessoEleitoralCipa.Apurado]: 'ok',
  [StatusProcessoEleitoralCipa.Encerrado]: 'neutro',
};

const tomPorStatusCandidato: Record<number, Tom> = {
  [StatusCandidatoCipa.Inscrito]: 'neutro',
  [StatusCandidatoCipa.Deferido]: 'info',
  [StatusCandidatoCipa.Indeferido]: 'alerta',
  [StatusCandidatoCipa.Eleito]: 'ok',
  [StatusCandidatoCipa.Suplente]: 'ok',
  [StatusCandidatoCipa.NaoEleito]: 'neutro',
};

// Apuração é sempre manual (sem urna digital) — quem apura digita os votos recebidos por cada
// candidato deferido; o sistema classifica titulares/suplentes usando o Dimensionamento mais
// recente da obra. Ver disclosure completo em RegistrarApuracaoProcessoEleitoralCipaCommand.cs.
export function ProcessoEleitoralCipaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [detalhe, setDetalhe] = useState<ProcessoEleitoralCipaDetalhe | null>(null);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [trabalhadorSelecionado, setTrabalhadorSelecionado] = useState('');
  const [votos, setVotos] = useState<Record<string, number>>({});
  const [dataInicioMandato, setDataInicioMandato] = useState('');
  const [dataFimMandato, setDataFimMandato] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const dados = await api.cipa.processosEleitorais.obterDetalhe(id);
      setDetalhe(dados);
      setTrabalhadores(await api.trabalhadores.listar(dados.processo.obraId));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar processo eleitoral.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function inscrever() {
    if (!id || !trabalhadorSelecionado) {
      setErro('Selecione um funcionário para inscrever.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.cipa.processosEleitorais.inscreverCandidato(id, trabalhadorSelecionado);
      setTrabalhadorSelecionado('');
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao inscrever candidato.');
    } finally {
      setProcessando(false);
    }
  }

  async function avaliar(candidatoId: string, deferido: boolean) {
    const motivo = deferido ? null : window.prompt('Motivo do indeferimento:') ?? '';
    try {
      setProcessando(true);
      setErro(null);
      await api.cipa.processosEleitorais.avaliarInscricao(candidatoId, deferido, motivo || null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao avaliar inscrição.');
    } finally {
      setProcessando(false);
    }
  }

  async function apurar() {
    if (!id || !detalhe) return;
    if (!dataInicioMandato || !dataFimMandato) {
      setErro('Informe o início e o fim do mandato para apurar.');
      return;
    }
    const deferidos = detalhe.candidatos.filter((c) => c.status === StatusCandidatoCipa.Deferido);
    if (deferidos.length === 0) {
      setErro('Não há candidatos deferidos para apurar.');
      return;
    }
    const votosApurados: VotoApuradoCipa[] = deferidos.map((c) => ({ candidatoId: c.id, votos: votos[c.id] ?? 0 }));
    try {
      setProcessando(true);
      setErro(null);
      await api.cipa.processosEleitorais.apurar(id, votosApurados, dataInicioMandato, dataFimMandato);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao registrar apuração.');
    } finally {
      setProcessando(false);
    }
  }

  async function baixarAta() {
    if (!id) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.cipa.processosEleitorais.baixarAtaPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ata-eleicao-cipa-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar a ata em PDF.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  if (!id) return <FeedbackInline tom="erro">Processo eleitoral não encontrado.</FeedbackInline>;

  const jaApurado =
    detalhe?.processo.status === StatusProcessoEleitoralCipa.Apurado ||
    detalhe?.processo.status === StatusProcessoEleitoralCipa.Encerrado;

  const colunasCandidatos: Coluna<CandidatoCipa>[] = [
    { chave: 'nome', rotulo: 'Nome', render: (c) => c.trabalhadorNome },
    { chave: 'matricula', rotulo: 'Matrícula', render: (c) => c.trabalhadorMatricula },
    { chave: 'inscricao', rotulo: 'Inscrição', render: (c) => c.dataInscricao?.slice(0, 10) ?? '' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (c) => (
        <>
          <StatusChip tom={tomPorStatusCandidato[c.status] ?? 'neutro'}>{statusCandidatoCipaLabel[c.status]}</StatusChip>
          {c.motivoIndeferimento && (
            <Text size={200} style={{ display: 'block' }}>
              {c.motivoIndeferimento}
            </Text>
          )}
        </>
      ),
    },
    jaApurado
      ? { chave: 'votos', rotulo: 'Votos', render: (c) => c.votosRecebidos }
      : {
          chave: 'acoes',
          rotulo: '',
          render: (c) =>
            c.status === StatusCandidatoCipa.Inscrito ? (
              <div style={{ display: 'flex', gap: 4 }}>
                <Button appearance="subtle" onClick={() => avaliar(c.id, true)} disabled={processando}>
                  Deferir
                </Button>
                <Button appearance="subtle" onClick={() => avaliar(c.id, false)} disabled={processando}>
                  Indeferir
                </Button>
              </div>
            ) : null,
        },
  ];

  return (
    <div>
      <PageHeader titulo="Processo eleitoral" voltarPara="/operacao/cipa" rotuloVoltar="Voltar para CIPA" />

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
                Processo eleitoral {detalhe.processo.numeroDocumento ?? ''}
                <StatusChip tom={tomPorStatusProcesso[detalhe.processo.status] ?? 'neutro'}>
                  {statusProcessoEleitoralCipaLabel[detalhe.processo.status]}
                </StatusChip>
              </div>
            }
            subtitulo={
              <>
                Convocação: {detalhe.processo.dataConvocacao?.slice(0, 10)} · Inscrições:{' '}
                {detalhe.processo.dataInicioInscricoes?.slice(0, 10)} a {detalhe.processo.dataFimInscricoes?.slice(0, 10)} · Votação:{' '}
                {detalhe.processo.dataVotacao?.slice(0, 10)}
              </>
            }
          >
            {jaApurado && (
              <FormRodape>
                <Button appearance="primary" icon={<DocumentPdf24Regular />} onClick={baixarAta} disabled={baixandoPdf}>
                  Baixar ata em PDF
                </Button>
              </FormRodape>
            )}
          </Card>

          {!jaApurado && (
            <Card titulo="Inscrever candidato">
              <FormSection titulo="Dados do Candidato" primeira>
                <FormGrid>
                  <Campo span={4}>
                    <Field label="Funcionário">
                      <Select value={trabalhadorSelecionado} onChange={(_, d) => setTrabalhadorSelecionado(d.value)}>
                        <option value="">Selecione</option>
                        {trabalhadores.map((t) => (
                          <option key={t.id} value={t.id}>
                            {t.matricula ? `${t.nome} (${t.matricula})` : t.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                </FormGrid>
                <FormRodape>
                  <Button appearance="primary" onClick={inscrever} disabled={processando}>
                    Inscrever
                  </Button>
                </FormRodape>
              </FormSection>
            </Card>
          )}

          <Card titulo="Candidatos">
            <DataTable
              aria-label="Candidatos do processo eleitoral"
              colunas={colunasCandidatos}
              linhas={detalhe.candidatos}
              chaveLinha={(c) => c.id}
              vazio={{ titulo: 'Nenhum candidato inscrito ainda.' }}
            />
          </Card>

          {!jaApurado && (
            <Card titulo="Apuração (manual)">
              <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
                Informe os votos recebidos por cada candidato deferido e o período do mandato. Ao
                confirmar, o sistema classifica automaticamente titulares/suplentes conforme o
                Dimensionamento cadastrado para a obra e já cria os respectivos membros da CIPA.
              </Text>
              <FormSection titulo="Período do Mandato" primeira>
                <FormGrid>
                  <Campo span={3}>
                    <Field label="Início do mandato" required>
                      <CampoData value={dataInicioMandato} onChange={(_, d) => setDataInicioMandato(d.value)} />
                    </Field>
                  </Campo>
                  <Campo span={3}>
                    <Field label="Fim do mandato" required>
                      <CampoData value={dataFimMandato} onChange={(_, d) => setDataFimMandato(d.value)} />
                    </Field>
                  </Campo>
                </FormGrid>
              </FormSection>
              <DataTable
                aria-label="Votos por candidato deferido"
                colunas={[
                  { chave: 'candidato', rotulo: 'Candidato', render: (c: CandidatoCipa) => c.trabalhadorNome },
                  {
                    chave: 'votos',
                    rotulo: 'Votos',
                    render: (c: CandidatoCipa) => (
                      <Input
                        type="number"
                        value={String(votos[c.id] ?? 0)}
                        onChange={(_, d) => setVotos({ ...votos, [c.id]: Number(d.value) })}
                      />
                    ),
                  },
                ]}
                linhas={detalhe.candidatos.filter((c) => c.status === StatusCandidatoCipa.Deferido)}
                chaveLinha={(c) => c.id}
                vazio={{ titulo: 'Nenhum candidato deferido ainda.' }}
              />
              <FormRodape>
                <Button appearance="primary" onClick={apurar} disabled={processando}>
                  Registrar apuração
                </Button>
              </FormRodape>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}
