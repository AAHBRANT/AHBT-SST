import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
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
import {
  ArrowDownload24Regular,
  ArrowLeft24Regular,
  ArrowUpload24Regular,
  LockClosed24Regular,
  Save24Regular,
} from '@fluentui/react-icons';
import {
  api,
  StatusInspecao,
  statusInspecaoLabel,
  statusItemChecklistLabel,
  tipoInspecaoLabel,
  type InspecaoDetalhe,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';

interface EdicaoResposta {
  statusItem: string;
  observacao: string;
  responsavelUsuarioId: string;
  prazo: string;
}

function edicaoInicial(): EdicaoResposta {
  return { statusItem: '', observacao: '', responsavelUsuarioId: '', prazo: '' };
}

export function InspecaoDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<InspecaoDetalhe | null>(null);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [edicoes, setEdicoes] = useState<Record<string, EdicaoResposta>>({});
  const [fotosSelecionadas, setFotosSelecionadas] = useState<Record<string, File | null>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [processando, setProcessando] = useState(false);
  const [enviandoFotoId, setEnviandoFotoId] = useState<string | null>(null);
  const [baixandoFotoId, setBaixandoFotoId] = useState<string | null>(null);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const [det, listaUsuarios] = await Promise.all([api.inspecoes.obterDetalhe(id), api.usuarios.listar()]);
      setDetalhe(det);
      setUsuarios(listaUsuarios);
      setEdicoes((atual) => {
        const novo: Record<string, EdicaoResposta> = {};
        for (const resposta of det.respostas) {
          novo[resposta.id] = atual[resposta.id] ?? {
            statusItem: resposta.statusItem != null ? String(resposta.statusItem) : '',
            observacao: resposta.observacao ?? '',
            responsavelUsuarioId: resposta.responsavelUsuarioId ?? '',
            prazo: resposta.prazo?.slice(0, 10) ?? '',
          };
        }
        return novo;
      });
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeção.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function atualizarEdicao(respostaId: string, campos: Partial<EdicaoResposta>) {
    setEdicoes((atual) => ({
      ...atual,
      [respostaId]: { ...(atual[respostaId] ?? edicaoInicial()), ...campos },
    }));
  }

  async function salvarResposta(respostaId: string) {
    const edicao = edicoes[respostaId];
    if (!edicao?.statusItem) {
      setErro('Selecione o status do item antes de salvar.');
      return;
    }
    try {
      setProcessando(true);
      setErro(null);
      await api.inspecoes.responderItem(
        respostaId,
        Number(edicao.statusItem),
        edicao.observacao || null,
        edicao.responsavelUsuarioId || null,
        edicao.prazo || null,
      );
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar resposta do item.');
    } finally {
      setProcessando(false);
    }
  }

  function selecionarFoto(respostaId: string, arquivo: File | null) {
    setFotosSelecionadas((atual) => ({ ...atual, [respostaId]: arquivo }));
  }

  async function enviarFoto(respostaId: string) {
    const arquivo = fotosSelecionadas[respostaId];
    if (!arquivo) return;
    try {
      setEnviandoFotoId(respostaId);
      setErro(null);
      await api.inspecoes.anexarFoto(respostaId, arquivo);
      selecionarFoto(respostaId, null);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao enviar a foto do item.');
    } finally {
      setEnviandoFotoId(null);
    }
  }

  async function baixarFoto(respostaId: string) {
    try {
      setBaixandoFotoId(respostaId);
      setErro(null);
      const blob = await api.inspecoes.baixarFoto(respostaId);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `inspecao-item-${respostaId}`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao baixar a foto do item.');
    } finally {
      setBaixandoFotoId(null);
    }
  }

  async function encerrar() {
    if (!id) return;
    try {
      setProcessando(true);
      setErro(null);
      await api.inspecoes.encerrar(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar inspeção. Confira se todos os itens foram respondidos.');
    } finally {
      setProcessando(false);
    }
  }

  if (!id) {
    return <Text>Inspeção não encontrada.</Text>;
  }

  const inspecao = detalhe?.inspecao;

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/prevencao/inspecoes')}
        style={{ marginBottom: 12 }}
      >
        Voltar para Inspeções
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {inspecao ? (
          <>
            <Text size={500} weight="semibold">
              {tipoInspecaoLabel[inspecao.tipoInspecao]} — {inspecao.checklistModeloNome} (v
              {inspecao.checklistModeloVersao})
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {inspecao.obraNome}</Text>
              {inspecao.atividadeNome && <Text>Atividade: {inspecao.atividadeNome}</Text>}
              <Text>Data: {inspecao.data?.slice(0, 10)}</Text>
              <Text>Responsável: {inspecao.responsavelUsuarioNome}</Text>
              <Badge appearance="tint">{statusInspecaoLabel[inspecao.status]}</Badge>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, alignItems: 'center' }}>
              <Text>
                Itens respondidos: {inspecao.itensRespondidos}/{inspecao.totalItens}
              </Text>
              {inspecao.itensNaoConformes > 0 && (
                <Badge color="danger" appearance="tint">
                  {inspecao.itensNaoConformes} não conforme(s)
                </Badge>
              )}
            </div>

            {inspecao.status === StatusInspecao.EmAndamento && (
              <div className={estilos.formActions} style={{ marginTop: 16 }}>
                <Button
                  appearance="primary"
                  icon={<LockClosed24Regular />}
                  onClick={encerrar}
                  disabled={processando}
                >
                  Encerrar inspeção
                </Button>
              </div>
            )}
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Itens do checklist</Text>
        </div>

        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>#</TableHeaderCell>
              <TableHeaderCell>Descrição</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Observação</TableHeaderCell>
              <TableHeaderCell>Responsável</TableHeaderCell>
              <TableHeaderCell>Prazo</TableHeaderCell>
              <TableHeaderCell>Foto</TableHeaderCell>
              <TableHeaderCell></TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {detalhe?.respostas.map((resposta) => {
              const edicao = edicoes[resposta.id] ?? edicaoInicial();
              const somenteLeitura = inspecao?.status !== StatusInspecao.EmAndamento;
              return (
                <TableRow key={resposta.id}>
                  <TableCell>{resposta.ordem}</TableCell>
                  <TableCell>
                    {resposta.descricao}
                    {resposta.exigeFotografia && (
                      <Badge appearance="tint" style={{ marginLeft: 6 }}>
                        Foto
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <Select
                      value={edicao.statusItem}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { statusItem: d.value })}
                      disabled={somenteLeitura}
                    >
                      <option value="">Selecione</option>
                      {Object.entries(statusItemChecklistLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </TableCell>
                  <TableCell>
                    <Input
                      value={edicao.observacao}
                      onChange={(_, d) => atualizarEdicao(resposta.id, { observacao: d.value })}
                      disabled={somenteLeitura}
                    />
                  </TableCell>
                  <TableCell>
                    {resposta.exigeResponsavel ? (
                      <Select
                        value={edicao.responsavelUsuarioId}
                        onChange={(_, d) => atualizarEdicao(resposta.id, { responsavelUsuarioId: d.value })}
                        disabled={somenteLeitura}
                      >
                        <option value="">Selecione</option>
                        {usuarios.map((usuario) => (
                          <option key={usuario.id} value={usuario.id}>
                            {usuario.nome}
                          </option>
                        ))}
                      </Select>
                    ) : (
                      '—'
                    )}
                  </TableCell>
                  <TableCell>
                    {resposta.exigePrazo ? (
                      <Input
                        type="date"
                        value={edicao.prazo}
                        onChange={(_, d) => atualizarEdicao(resposta.id, { prazo: d.value })}
                        disabled={somenteLeitura}
                      />
                    ) : (
                      '—'
                    )}
                  </TableCell>
                  <TableCell>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 4, alignItems: 'flex-start' }}>
                      {!somenteLeitura && (
                        <>
                          <input
                            type="file"
                            accept="image/*"
                            capture="environment"
                            onChange={(e) => selecionarFoto(resposta.id, e.target.files?.[0] ?? null)}
                            style={{ maxWidth: 140 }}
                          />
                          <Button
                            appearance="subtle"
                            size="small"
                            icon={<ArrowUpload24Regular />}
                            onClick={() => enviarFoto(resposta.id)}
                            disabled={!fotosSelecionadas[resposta.id] || enviandoFotoId === resposta.id}
                          >
                            Enviar
                          </Button>
                        </>
                      )}
                      {resposta.temFoto && (
                        <Button
                          appearance="subtle"
                          size="small"
                          icon={<ArrowDownload24Regular />}
                          onClick={() => baixarFoto(resposta.id)}
                          disabled={baixandoFotoId === resposta.id}
                        >
                          Ver foto
                        </Button>
                      )}
                    </div>
                  </TableCell>
                  <TableCell>
                    {!somenteLeitura && (
                      <Button
                        appearance="subtle"
                        icon={<Save24Regular />}
                        onClick={() => salvarResposta(resposta.id)}
                        disabled={processando}
                        aria-label="Salvar item"
                      />
                    )}
                  </TableCell>
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
