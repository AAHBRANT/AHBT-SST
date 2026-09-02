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
  Textarea,
} from '@fluentui/react-components';
import { CampoData } from '../../components/CampoData';
import { ArrowLeft24Regular, Delete24Regular } from '@fluentui/react-icons';
import {
  api,
  cargoMembroCipaLabel,
  origemMembroCipaLabel,
  CargoMembroCipa,
  type MembroCipaDetalhe,
} from '../../lib/api';
import { SeletorFotoCamera } from '../../components/SeletorFotoCamera';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';

function treinamentoVazio() {
  return { cargaHoraria: 4, conteudoProgramatico: '', dataRealizacao: '', dataValidade: '', instituicaoInstrutor: '' };
}

export function MembroCipaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<MembroCipaDetalhe | null>(null);
  const [novoTreinamento, setNovoTreinamento] = useState(treinamentoVazio());
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const { confirmar, dialogElement } = useConfirmarExclusao();
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

  if (!id) return <Text>Membro não encontrado.</Text>;

  return (
    <div>
      {dialogElement}
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
                {detalhe.membro.trabalhadorNome}
              </Text>
              {detalhe.membro.mandatoAtivo && (
                <Badge appearance="tint" color="success">
                  Mandato ativo
                </Badge>
              )}
            </div>
            <Text size={200} style={{ display: 'block', marginBottom: 12 }}>
              {detalhe.membro.obraNome} · {origemMembroCipaLabel[detalhe.membro.origemMembro]} · Mandato:{' '}
              {detalhe.membro.dataInicioMandato?.slice(0, 10)} a {detalhe.membro.dataFimMandato?.slice(0, 10)}
            </Text>
            <div className={estilos.form}>
              <Field label="Cargo">
                <Select value={String(detalhe.membro.cargo)} onChange={(_, d) => definirCargo(Number(d.value))} disabled={salvando}>
                  {Object.entries(cargoMembroCipaLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
            {detalhe.membro.cargo !== CargoMembroCipa.Presidente && (
              <div className={estilos.formActions}>
                <Button appearance="secondary" icon={<Delete24Regular />} onClick={encerrarMandato} disabled={salvando}>
                  Encerrar mandato
                </Button>
              </div>
            )}
          </div>

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Novo treinamento</Text>
            </div>
            <div className={estilos.form}>
              <Field label="Carga horária (h)" required>
                <Input
                  type="number"
                  value={String(novoTreinamento.cargaHoraria)}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, cargaHoraria: Number(d.value) })}
                />
              </Field>
              <Field label="Data de realização" required>
                <CampoData
                  value={novoTreinamento.dataRealizacao}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataRealizacao: d.value })}
                />
              </Field>
              <Field label="Validade">
                <CampoData
                  value={novoTreinamento.dataValidade}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, dataValidade: d.value })}
                />
              </Field>
              <Field label="Instituição/instrutor">
                <Input
                  value={novoTreinamento.instituicaoInstrutor}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, instituicaoInstrutor: d.value })}
                />
              </Field>
              <Field label="Conteúdo programático">
                <Textarea
                  value={novoTreinamento.conteudoProgramatico}
                  onChange={(_, d) => setNovoTreinamento({ ...novoTreinamento, conteudoProgramatico: d.value })}
                />
              </Field>
            </div>
            <div className={estilos.formActions}>
              <Button appearance="primary" onClick={criarTreinamento} disabled={salvando}>
                Adicionar treinamento
              </Button>
            </div>
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Treinamentos</Text>
            </div>
            {detalhe.treinamentos.length === 0 ? (
              <EstadoVazio mensagem="Nenhum treinamento registrado ainda." />
            ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Realização</TableHeaderCell>
                  <TableHeaderCell>Validade</TableHeaderCell>
                  <TableHeaderCell>Carga horária</TableHeaderCell>
                  <TableHeaderCell>Instituição/instrutor</TableHeaderCell>
                  <TableHeaderCell>Certificado</TableHeaderCell>
                  <TableHeaderCell>Lista de presença</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {detalhe.treinamentos.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell>{t.dataRealizacao?.slice(0, 10)}</TableCell>
                    <TableCell>{t.dataValidade?.slice(0, 10) ?? '—'}</TableCell>
                    <TableCell>{t.cargaHoraria}h</TableCell>
                    <TableCell>{t.instituicaoInstrutor ?? '—'}</TableCell>
                    <TableCell>
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                        {t.temCertificado ? (
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
                        )}
                      </div>
                    </TableCell>
                    <TableCell>
                      <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                        {t.temListaPresenca ? (
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
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            )}
          </div>
        </>
      )}
    </div>
  );
}
