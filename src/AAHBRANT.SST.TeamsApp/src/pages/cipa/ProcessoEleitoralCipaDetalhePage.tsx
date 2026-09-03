import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Field,
  Input,
  Select,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { ArrowLeft24Regular, DocumentPdf24Regular } from '@fluentui/react-icons';
import {
  api,
  statusCandidatoCipaLabel,
  statusProcessoEleitoralCipaLabel,
  StatusCandidatoCipa,
  StatusProcessoEleitoralCipa,
  type ProcessoEleitoralCipaDetalhe,
  type Trabalhador,
  type VotoApuradoCipa,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

// Apuração é sempre manual (sem urna digital) — quem apura digita os votos recebidos por cada
// candidato deferido; o sistema classifica titulares/suplentes usando o Dimensionamento mais
// recente da obra. Ver disclosure completo em RegistrarApuracaoProcessoEleitoralCipaCommand.cs.
export function ProcessoEleitoralCipaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
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
      setErro('Selecione um trabalhador para inscrever.');
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

  if (!id) return <Text>Processo eleitoral não encontrado.</Text>;

  const jaApurado =
    detalhe?.processo.status === StatusProcessoEleitoralCipa.Apurado ||
    detalhe?.processo.status === StatusProcessoEleitoralCipa.Encerrado;

  return (
    <div>
      <Button appearance="subtle" icon={<ArrowLeft24Regular />} onClick={() => navigate('/operacao/cipa')} style={{ marginBottom: 12 }}>
        Voltar para CIPA
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      {!detalhe ? (
        <Text>Carregando...</Text>
      ) : (
        <>
          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div style={{ display: 'flex', gap: 16, alignItems: 'center', marginBottom: 8 }}>
              <Text size={500} weight="semibold">
                Processo eleitoral {detalhe.processo.numeroDocumento ?? ''}
              </Text>
              <Badge appearance="tint">{statusProcessoEleitoralCipaLabel[detalhe.processo.status]}</Badge>
            </div>
            <Text size={200}>
              Convocação: {detalhe.processo.dataConvocacao?.slice(0, 10)} · Inscrições:{' '}
              {detalhe.processo.dataInicioInscricoes?.slice(0, 10)} a {detalhe.processo.dataFimInscricoes?.slice(0, 10)} · Votação:{' '}
              {detalhe.processo.dataVotacao?.slice(0, 10)}
            </Text>
            {jaApurado && (
              <div className={estilos.formActions} style={{ marginTop: 12 }}>
                <Button appearance="primary" icon={<DocumentPdf24Regular />} onClick={baixarAta} disabled={baixandoPdf}>
                  Baixar ata em PDF
                </Button>
              </div>
            )}
          </div>

          {!jaApurado && (
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <div className={estilos.toolbar}>
                <Text weight="semibold">Inscrever candidato</Text>
              </div>
              <div className={estilos.form}>
                <Field label="Trabalhador">
                  <Select value={trabalhadorSelecionado} onChange={(_, d) => setTrabalhadorSelecionado(d.value)}>
                    <option value="">Selecione</option>
                    {trabalhadores.map((t) => (
                      <option key={t.id} value={t.id}>
                        {t.nome} ({t.matricula})
                      </option>
                    ))}
                  </Select>
                </Field>
              </div>
              <div className={estilos.formActions}>
                <Button appearance="primary" onClick={inscrever} disabled={processando}>
                  Inscrever
                </Button>
              </div>
            </div>
          )}

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Candidatos</Text>
            </div>
            <Table noNativeElements>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Nome</TableHeaderCell>
                  <TableHeaderCell>Matrícula</TableHeaderCell>
                  <TableHeaderCell>Inscrição</TableHeaderCell>
                  <TableHeaderCell>Status</TableHeaderCell>
                  {!jaApurado && <TableHeaderCell></TableHeaderCell>}
                  {jaApurado && <TableHeaderCell>Votos</TableHeaderCell>}
                </TableRow>
              </TableHeader>
              <TableBody>
                {detalhe.candidatos.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell>{c.trabalhadorNome}</TableCell>
                    <TableCell>{c.trabalhadorMatricula}</TableCell>
                    <TableCell>{c.dataInscricao?.slice(0, 10)}</TableCell>
                    <TableCell>
                      <Badge appearance="tint">{statusCandidatoCipaLabel[c.status]}</Badge>
                      {c.motivoIndeferimento && (
                        <Text size={200} style={{ display: 'block' }}>
                          {c.motivoIndeferimento}
                        </Text>
                      )}
                    </TableCell>
                    {!jaApurado && c.status === StatusCandidatoCipa.Inscrito && (
                      <TableCell>
                        <div style={{ display: 'flex', gap: 4 }}>
                          <Button appearance="subtle" onClick={() => avaliar(c.id, true)} disabled={processando}>
                            Deferir
                          </Button>
                          <Button appearance="subtle" onClick={() => avaliar(c.id, false)} disabled={processando}>
                            Indeferir
                          </Button>
                        </div>
                      </TableCell>
                    )}
                    {!jaApurado && c.status !== StatusCandidatoCipa.Inscrito && <TableCell></TableCell>}
                    {jaApurado && <TableCell>{c.votosRecebidos}</TableCell>}
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {!jaApurado && (
            <div className={estilos.card}>
              <div className={estilos.toolbar}>
                <Text weight="semibold">Apuração (manual)</Text>
              </div>
              <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
                Informe os votos recebidos por cada candidato deferido e o período do mandato. Ao
                confirmar, o sistema classifica automaticamente titulares/suplentes conforme o
                Dimensionamento cadastrado para a obra e já cria os respectivos membros da CIPA.
              </Text>
              <div className={estilos.form}>
                <Field label="Início do mandato" required>
                  <CampoData value={dataInicioMandato} onChange={(_, d) => setDataInicioMandato(d.value)} />
                </Field>
                <Field label="Fim do mandato" required>
                  <CampoData value={dataFimMandato} onChange={(_, d) => setDataFimMandato(d.value)} />
                </Field>
              </div>
              <Table noNativeElements>
                <TableHeader>
                  <TableRow>
                    <TableHeaderCell>Candidato</TableHeaderCell>
                    <TableHeaderCell>Votos</TableHeaderCell>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {detalhe.candidatos
                    .filter((c) => c.status === StatusCandidatoCipa.Deferido)
                    .map((c) => (
                      <TableRow key={c.id}>
                        <TableCell>{c.trabalhadorNome}</TableCell>
                        <TableCell>
                          <Input
                            type="number"
                            value={String(votos[c.id] ?? 0)}
                            onChange={(_, d) => setVotos({ ...votos, [c.id]: Number(d.value) })}
                          />
                        </TableCell>
                      </TableRow>
                    ))}
                </TableBody>
              </Table>
              <div className={estilos.formActions}>
                <Button appearance="primary" onClick={apurar} disabled={processando}>
                  Registrar apuração
                </Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
