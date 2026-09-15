import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Button,
  Campo,
  CampoData,
  Card,
  Carregando,
  Checkbox,
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
  Textarea,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Delete24Regular, DocumentPdf24Regular } from '@fluentui/react-icons';
import {
  api,
  prioridadeAcaoLabel,
  statusAcaoPlanoLabel,
  statusReuniaoCipaLabel,
  tipoAcaoPlanoLabel,
  tipoReuniaoCipaLabel,
  StatusAcaoPlano,
  StatusReuniaoCipa,
  type AcaoPlano,
  type NovaAcaoPlano,
  type ReuniaoCipaDetalhe,
  type Trabalhador,
  type Usuario,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';

function novaAcaoInicial(): Omit<NovaAcaoPlano, 'origemTipo' | 'origemId'> {
  return { tipo: 1, descricao: '', responsavelUsuarioId: '', prioridade: 3, prazo: '' };
}

// Mesmo mapeamento de NaoConformidadeDetalhePage.tsx (piloto 2) — mesmo tipo AcaoPlano, mesmos tons
// entre os módulos que o reaproveitam (ruling da Onda 2: consistência de StatusChip por tipo de dado,
// não só por tela).
const tomAcaoPlano: Record<number, Tom> = {
  [StatusAcaoPlano.Pendente]: 'atencao',
  [StatusAcaoPlano.EmAndamento]: 'atencao',
  [StatusAcaoPlano.Concluido]: 'ok',
  [StatusAcaoPlano.Vencido]: 'alerta',
};

const tomReuniao: Record<number, Tom> = {
  [StatusReuniaoCipa.Agendada]: 'neutro',
  [StatusReuniaoCipa.Realizada]: 'atencao',
  [StatusReuniaoCipa.AtaRegistrada]: 'ok',
};

// Presença: lista os trabalhadores da obra; marcar "Convocado" inclui na ata, e "Presente" registra
// se compareceu. Plano de Ações da reunião (matriz 5W2H pedida pelo usuário) reaproveita o mecanismo
// genérico api.acoesPlano (origemTipo="ReuniaoCipa") — mesmo padrão de PcmsoDetalhePage.tsx.
export function ReuniaoCipaDetalhePage() {
  const { id } = useParams<{ id: string }>();
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
  const { confirmar, dialogElement } = useConfirmar();
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

  if (!id) return <FeedbackInline tom="erro">Reunião não encontrada.</FeedbackInline>;

  const encerrada = detalhe?.reuniao.status === StatusReuniaoCipa.AtaRegistrada;

  const colunasAcoes: Coluna<AcaoPlano>[] = [
    { chave: 'descricao', rotulo: 'Tema/problema' },
    { chave: 'acao', rotulo: 'Ação & responsável', render: (a) => `${tipoAcaoPlanoLabel[a.tipo]} — ${a.responsavelUsuarioNome ?? '—'}` },
    { chave: 'prazo', rotulo: 'Prazo', render: (a) => a.prazo?.slice(0, 10) ?? '—' },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => <StatusChip tom={tomAcaoPlano[a.status] ?? 'neutro'}>{statusAcaoPlanoLabel[a.status]}</StatusChip>,
    },
  ];

  return (
    <div>
      {dialogElement}
      <PageHeader titulo="Reunião da CIPA" voltarPara="/operacao/cipa" rotuloVoltar="Voltar para CIPA" />

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
                Reunião {tipoReuniaoCipaLabel[detalhe.reuniao.tipo]} — {detalhe.reuniao.dataReuniao?.slice(0, 10)}
                <StatusChip tom={tomReuniao[detalhe.reuniao.status] ?? 'neutro'}>
                  {statusReuniaoCipaLabel[detalhe.reuniao.status]}
                </StatusChip>
              </div>
            }
            subtitulo={detalhe.reuniao.pauta ? `Pauta: ${detalhe.reuniao.pauta}` : undefined}
          >
            <FormRodape>
              <Button appearance="primary" icon={<DocumentPdf24Regular />} onClick={baixarAta} disabled={baixandoPdf}>
                Baixar ata em PDF
              </Button>
            </FormRodape>
          </Card>

          <Card titulo="Lista de presença">
            <DataTable
              aria-label="Lista de presença"
              colunas={[
                {
                  chave: 'nome',
                  rotulo: 'Funcionário',
                  render: (t: Trabalhador) => (t.matricula ? `${t.nome} (${t.matricula})` : t.nome),
                },
                {
                  chave: 'convocado',
                  rotulo: 'Convocado',
                  render: (t: Trabalhador) => (
                    <Checkbox
                      checked={presenca[t.id]?.incluido ?? false}
                      disabled={encerrada}
                      onChange={(_, d) =>
                        setPresenca({ ...presenca, [t.id]: { incluido: !!d.checked, presente: presenca[t.id]?.presente ?? false } })
                      }
                    />
                  ),
                },
                {
                  chave: 'presente',
                  rotulo: 'Presente',
                  render: (t: Trabalhador) => (
                    <Checkbox
                      checked={presenca[t.id]?.presente ?? false}
                      disabled={encerrada || !presenca[t.id]?.incluido}
                      onChange={(_, d) =>
                        setPresenca({ ...presenca, [t.id]: { incluido: presenca[t.id]?.incluido ?? false, presente: !!d.checked } })
                      }
                    />
                  ),
                },
              ]}
              linhas={trabalhadores}
              chaveLinha={(t) => t.id}
              vazio={{ titulo: 'Nenhum funcionário cadastrado nesta obra ainda.' }}
            />
            {!encerrada && (
              <FormRodape>
                <Button appearance="primary" onClick={salvarPresenca} disabled={salvando}>
                  Salvar presença
                </Button>
              </FormRodape>
            )}
          </Card>

          {!encerrada && (
            <Card titulo="Encerrar reunião">
              <Field label="Deliberações" required>
                <Textarea value={deliberacoes} onChange={(_, d) => setDeliberacoes(d.value)} />
              </Field>
              <FormRodape>
                <Button appearance="primary" onClick={encerrar} disabled={salvando}>
                  Registrar ata e encerrar
                </Button>
              </FormRodape>
            </Card>
          )}
          {encerrada && detalhe.reuniao.deliberacoes && (
            <Card titulo="Deliberações">
              <Text>{detalhe.reuniao.deliberacoes}</Text>
            </Card>
          )}

          <Card titulo="Novo item do plano de ações (5W2H)">
            <FormSection titulo="Novo Item do Plano" primeira>
              <FormGrid>
                <Campo span={2}>
                  <Field label="Tipo">
                    <Select value={String(novaAcao.tipo)} onChange={(_, d) => setNovaAcao({ ...novaAcao, tipo: Number(d.value) })}>
                      {Object.entries(tipoAcaoPlanoLabel).map(([valor, rotulo]) => (
                        <option key={valor} value={valor}>
                          {rotulo}
                        </option>
                      ))}
                    </Select>
                  </Field>
                </Campo>
                <Campo span={3}>
                  <Field label="Tema/problema" required>
                    <Input value={novaAcao.descricao} onChange={(_, d) => setNovaAcao({ ...novaAcao, descricao: d.value })} />
                  </Field>
                </Campo>
                <Campo span={3}>
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
                </Campo>
                <Campo span={2}>
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
                </Campo>
                <Campo span={2}>
                  <Field label="Prazo">
                    <CampoData value={novaAcao.prazo ?? ''} onChange={(_, d) => setNovaAcao({ ...novaAcao, prazo: d.value })} />
                  </Field>
                </Campo>
              </FormGrid>
              <FormRodape>
                <Button appearance="primary" onClick={criarAcao} disabled={salvando}>
                  Adicionar ação
                </Button>
              </FormRodape>
            </FormSection>
          </Card>

          <Card
            titulo="Plano de ações e pendências"
            acoes={
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
            }
          >
            <DataTable
              aria-label="Plano de ações da reunião"
              colunas={colunasAcoes}
              linhas={acoesPlano}
              chaveLinha={(a) => a.id}
              vazio={{ titulo: 'Nenhuma ação registrada no plano ainda.' }}
              acoesLinha={(acao) => (
                <div style={{ display: 'flex', gap: 4 }}>
                  {!acao.dataValidacao && (
                    <Button appearance="subtle" onClick={() => validarAcao(acao.id)} disabled={salvando}>
                      Validar
                    </Button>
                  )}
                  <Button appearance="subtle" icon={<Delete24Regular />} onClick={() => excluirAcao(acao.id)} aria-label="Excluir" />
                </div>
              )}
            />
          </Card>
        </div>
      )}
    </div>
  );
}
