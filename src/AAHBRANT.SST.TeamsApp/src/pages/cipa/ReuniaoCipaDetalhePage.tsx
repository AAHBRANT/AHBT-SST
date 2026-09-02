import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Checkbox,
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
import { ArrowLeft24Regular, Delete24Regular, DocumentPdf24Regular } from '@fluentui/react-icons';
import {
  api,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusReuniaoCipaLabel,
  tipoAcaoPlanoLabel,
  tipoReuniaoCipaLabel,
  StatusReuniaoCipa,
  type AcaoPlano,
  type NovaAcaoPlano,
  type ReuniaoCipaDetalhe,
  type Trabalhador,
  type Usuario,
} from '../../lib/api';
import { usePageStyles } from '../pageStyles';
import { useConfirmarExclusao } from '../../hooks/useConfirmarExclusao';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { EstadoVazio } from '../../components/EstadoVazio';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

// Presença: lista os trabalhadores da obra; marcar "Convocado" inclui na ata, e "Presente" registra
// se compareceu. Plano de Ações da reunião (matriz 5W2H pedida pelo usuário) reaproveita o mecanismo
// genérico api.acoesPlano (origemTipo="ReuniaoCipa") — mesmo padrão de PcmsoDetalhePage.tsx.
export function ReuniaoCipaDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [detalhe, setDetalhe] = useState<ReuniaoCipaDetalhe | null>(null);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [acoesPlano, setAcoesPlano] = useState<AcaoPlano[]>([]);
  const [presenca, setPresenca] = useState<Record<string, { incluido: boolean; presente: boolean }>>({});
  const [deliberacoes, setDeliberacoes] = useState('');
  const [novaAcao, setNovaAcao] = useState(novaAcaoInicial());
  const [usuarioValidador, setUsuarioValidador] = useState('');
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);
  const [baixandoPdf, setBaixandoPdf] = useState(false);
  const { confirmar, dialogElement } = useConfirmarExclusao();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      const dados = await api.cipa.reunioes.obterDetalhe(id);
      setDetalhe(dados);
      setDeliberacoes(dados.reuniao.deliberacoes ?? '');
      const [listaTrabalhadores, listaUsuarios, listaAcoes] = await Promise.all([
        api.trabalhadores.listar(dados.reuniao.obraId),
        api.usuarios.listar(),
        api.acoesPlano.listar('ReuniaoCipa', id),
      ]);
      setTrabalhadores(listaTrabalhadores);
      setUsuarios(listaUsuarios);
      setAcoesPlano(listaAcoes);

      const mapa: Record<string, { incluido: boolean; presente: boolean }> = {};
      for (const t of listaTrabalhadores) mapa[t.id] = { incluido: false, presente: false };
      for (const p of dados.participantes) mapa[p.trabalhadorId] = { incluido: true, presente: p.presente };
      setPresenca(mapa);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar reunião da CIPA.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  async function salvarPresenca() {
    if (!id) return;
    const participantes = Object.entries(presenca)
      .filter(([, v]) => v.incluido)
      .map(([trabalhadorId, v]) => ({ trabalhadorId, presente: v.presente }));
    try {
      setSalvando(true);
      setErro(null);
      await api.cipa.reunioes.registrarParticipantes(id, participantes);
      await carregar();
      sucessoToast('Presença salva com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao salvar presença.');
    } finally {
      setSalvando(false);
    }
  }

  async function encerrar() {
    if (!id) return;
    if (!deliberacoes.trim()) {
      setErro('Registre as deliberações antes de encerrar a reunião.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.cipa.reunioes.encerrar(id, deliberacoes);
      await carregar();
      sucessoToast('Reunião encerrada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao encerrar reunião.');
    } finally {
      setSalvando(false);
    }
  }

  async function baixarAta() {
    if (!id) return;
    try {
      setBaixandoPdf(true);
      setErro(null);
      const blob = await api.cipa.reunioes.baixarAtaPdf(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `ata-reuniao-cipa-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar a ata em PDF.');
    } finally {
      setBaixandoPdf(false);
    }
  }

  async function criarAcao() {
    if (!id) return;
    if (!novaAcao.descricao.trim()) {
      setErro('Informe a descrição (tema/problema) da ação.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.acoesPlano.criar({
        origemTipo: 'ReuniaoCipa',
        origemId: id,
        ...novaAcao,
        responsavelUsuarioId: novaAcao.responsavelUsuarioId || null,
        prazo: novaAcao.prazo || null,
      });
      setNovaAcao(novaAcaoInicial());
      await carregar();
      sucessoToast('Ação adicionada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar ação do plano.');
    } finally {
      setSalvando(false);
    }
  }

  async function validarAcao(acaoId: string) {
    if (!usuarioValidador) {
      setErro('Selecione o usuário responsável pela validação.');
      return;
    }
    try {
      setSalvando(true);
      setErro(null);
      await api.acoesPlano.validar(acaoId, usuarioValidador);
      await carregar();
      sucessoToast('Ação validada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao validar ação.');
    } finally {
      setSalvando(false);
    }
  }

  async function excluirAcao(acaoId: string) {
    if (!(await confirmar('Excluir esta ação do plano? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.acoesPlano.excluir(acaoId);
      await carregar();
      sucessoToast('Ação excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir ação.');
    }
  }

  if (!id) return <Text>Reunião não encontrada.</Text>;

  const encerrada = detalhe?.reuniao.status === StatusReuniaoCipa.AtaRegistrada;

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
                Reunião {tipoReuniaoCipaLabel[detalhe.reuniao.tipo]} — {detalhe.reuniao.dataReuniao?.slice(0, 10)}
              </Text>
              <Badge appearance="tint">{statusReuniaoCipaLabel[detalhe.reuniao.status]}</Badge>
            </div>
            {detalhe.reuniao.pauta && <Text size={200}>Pauta: {detalhe.reuniao.pauta}</Text>}
            <div className={estilos.formActions}>
              <Button appearance="primary" icon={<DocumentPdf24Regular />} onClick={baixarAta} disabled={baixandoPdf}>
                Baixar ata em PDF
              </Button>
            </div>
          </div>

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Lista de presença</Text>
            </div>
            {trabalhadores.length === 0 ? (
              <EstadoVazio mensagem="Nenhum trabalhador cadastrado nesta obra ainda." />
            ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Trabalhador</TableHeaderCell>
                  <TableHeaderCell>Convocado</TableHeaderCell>
                  <TableHeaderCell>Presente</TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {trabalhadores.map((t) => (
                  <TableRow key={t.id}>
                    <TableCell>
                      {t.nome} ({t.matricula})
                    </TableCell>
                    <TableCell>
                      <Checkbox
                        checked={presenca[t.id]?.incluido ?? false}
                        disabled={encerrada}
                        onChange={(_, d) =>
                          setPresenca({ ...presenca, [t.id]: { incluido: !!d.checked, presente: presenca[t.id]?.presente ?? false } })
                        }
                      />
                    </TableCell>
                    <TableCell>
                      <Checkbox
                        checked={presenca[t.id]?.presente ?? false}
                        disabled={encerrada || !presenca[t.id]?.incluido}
                        onChange={(_, d) =>
                          setPresenca({ ...presenca, [t.id]: { incluido: presenca[t.id]?.incluido ?? false, presente: !!d.checked } })
                        }
                      />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
            )}
            {!encerrada && (
              <div className={estilos.formActions}>
                <Button appearance="primary" onClick={salvarPresenca} disabled={salvando}>
                  Salvar presença
                </Button>
              </div>
            )}
          </div>

          {!encerrada && (
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <div className={estilos.toolbar}>
                <Text weight="semibold">Encerrar reunião</Text>
              </div>
              <Field label="Deliberações" required>
                <Textarea value={deliberacoes} onChange={(_, d) => setDeliberacoes(d.value)} />
              </Field>
              <div className={estilos.formActions}>
                <Button appearance="primary" onClick={encerrar} disabled={salvando}>
                  Registrar ata e encerrar
                </Button>
              </div>
            </div>
          )}
          {encerrada && detalhe.reuniao.deliberacoes && (
            <div className={estilos.card} style={{ marginBottom: 16 }}>
              <div className={estilos.toolbar}>
                <Text weight="semibold">Deliberações</Text>
              </div>
              <Text>{detalhe.reuniao.deliberacoes}</Text>
            </div>
          )}

          <div className={estilos.card} style={{ marginBottom: 16 }}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Novo item do plano de ações (5W2H)</Text>
            </div>
            <div className={estilos.form}>
              <Field label="Tipo">
                <Select value={String(novaAcao.tipo)} onChange={(_, d) => setNovaAcao({ ...novaAcao, tipo: Number(d.value) })}>
                  {Object.entries(tipoAcaoPlanoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Tema/problema" required>
                <Input value={novaAcao.descricao} onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })} />
              </Field>
              <Field label="Responsável">
                <Select
                  value={novaAcao.responsavelUsuarioId ?? ''}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, responsavelUsuarioId: d.value })}
                >
                  <option value="">Nenhum</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Prioridade">
                <Select
                  value={String(novaAcao.prioridade)}
                  onChange={(_, d) => setNovaAcao({ ...novaAcao, prioridade: Number(d.value) })}
                >
                  {Object.entries(prioridadeAcaoLabel).map(([valor, rotulo]) => (
                    <option key={valor} value={valor}>
                      {rotulo}
                    </option>
                  ))}
                </Select>
              </Field>
              <Field label="Prazo">
                <CampoData value={novaAcao.prazo ?? ''} onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })} />
              </Field>
            </div>
            <div className={estilos.formActions}>
              <Button appearance="primary" onClick={criarAcao} disabled={salvando}>
                Adicionar ação
              </Button>
            </div>
          </div>

          <div className={estilos.card}>
            <div className={estilos.toolbar}>
              <Text weight="semibold">Plano de ações e pendências</Text>
              <Field label="Validar como">
                <Select value={usuarioValidador} onChange={(_, d) => setUsuarioValidador(d.value)}>
                  <option value="">Selecione um usuário</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </div>
            {acoesPlano.length === 0 ? (
              <EstadoVazio mensagem="Nenhuma ação registrada no plano ainda." />
            ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHeaderCell>Tema/problema</TableHeaderCell>
                  <TableHeaderCell>Ação & responsável</TableHeaderCell>
                  <TableHeaderCell>Prazo</TableHeaderCell>
                  <TableHeaderCell>Status</TableHeaderCell>
                  <TableHeaderCell></TableHeaderCell>
                </TableRow>
              </TableHeader>
              <TableBody>
                {acoesPlano.map((acao) => (
                  <TableRow key={acao.id}>
                    <TableCell>{acao.descricao}</TableCell>
                    <TableCell>
                      {tipoAcaoPlanoLabel[acao.tipo]} — {acao.responsavelUsuarioNome ?? '—'}
                    </TableCell>
                    <TableCell>{acao.prazo?.slice(0, 10) ?? '—'}</TableCell>
                    <TableCell>
                      <Badge appearance="tint">{statusAcaoPlanoLabel[acao.status]}</Badge>
                    </TableCell>
                    <TableCell>
                      <div style={{ display: 'flex', gap: 4 }}>
                        {!acao.dataValidacao && (
                          <Button appearance="subtle" onClick={() => validarAcao(acao.id)} disabled={salvando}>
                            Validar
                          </Button>
                        )}
                        <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluirAcao(acao.id)} aria-label="Excluir" />
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
