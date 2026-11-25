import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  BarraProgresso,
  CampoData,
  CelulaDupla,
  FiltroRapido,
  ListaSelecaoMultipla,
  PageHeader,
  PainelCriacaoInline,
  nivelVencimento,
  tomDeVencimento,
  tokensUi,
  DataTable,
  Field,
  FeedbackInline,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  Select,
  StatusChip,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import { Add24Regular, Delete24Regular, DocumentPdf24Regular, Open24Regular, PeopleTeamAdd24Regular, Play24Regular, Search20Regular, Warning20Regular } from '@fluentui/react-icons';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { FotoTrabalhador } from '../../components/FotoTrabalhador';
import { api, StatusApr, statusAprLabel, type Apr, type Atividade, type Equipe, type NovaApr, type Trabalhador } from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { dataBr, hojeIso } from '../../lib/datas';
import { useRegistrosTravados } from '../../hooks/useRegistrosTravados';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

function aprVazia(): NovaApr {
  return {
    atividadeId: '',
    local: '',
    maquinasEquipamentos: '',
    pgrReferencia: '',
    equipeId: null,
    data: hojeIso(),
    validade: null,
    responsaveisIds: [],
  };
}

// Mapeamento 1:1 pelo nome semântico do Fluent (Guia de conversão item 5), preservando as mesmas
// cores da versão anterior (Badge color=): informative→info, warning→atencao, success→ok,
// danger→alerta, subtle→neutro. Reutilizado em AprDetalhePage.tsx para lista e detalhe ficarem
// visualmente coerentes.
const tomPorStatusApr: Record<number, Tom> = {
  [StatusApr.EmElaboracao]: 'neutro',
  [StatusApr.AguardandoAprovacao]: 'atencao',
  [StatusApr.Aprovada]: 'ok',
  [StatusApr.Reprovada]: 'alerta',
  [StatusApr.Encerrada]: 'info',
};

// Onda 2 Task 11 (camada ui/): lista + formulário de criação de APR. Nada de Fluent cru nem de
// pageStyles aqui — lista em DataTable, seleção de responsáveis em ChipCheckboxGroup.
// Visual e utilidade aprovados em 25/11/2026 (docs/mockups/dds-apr-pt-visual-2026-11-25.html).
type FiltroApr = 'todas' | 'elaboracao' | 'aguardando' | 'aprovadas' | 'vencendo' | 'fechadas';
const MS_DIA = 24 * 60 * 60 * 1000;

function diasAteVencer(validade?: string | null): number | null {
  if (!validade) return null;
  const fim = new Date(validade.slice(0, 10) + 'T00:00:00').getTime();
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  return Math.round((fim - hoje.getTime()) / MS_DIA);
}

// "Vencem em breve": aprovada e com validade até 7 dias à frente.
function aprVencendo(a: Apr): boolean {
  const dias = diasAteVencer(a.validade);
  return a.status === StatusApr.Aprovada && dias !== null && dias >= 0 && dias <= 7;
}

function passaNoFiltroApr(a: Apr, f: FiltroApr): boolean {
  switch (f) {
    case 'elaboracao': return a.status === StatusApr.EmElaboracao;
    case 'aguardando': return a.status === StatusApr.AguardandoAprovacao;
    case 'aprovadas': return a.status === StatusApr.Aprovada;
    case 'vencendo': return aprVencendo(a);
    case 'fechadas': return a.status === StatusApr.Reprovada || a.status === StatusApr.Encerrada;
    default: return true;
  }
}

function tomDaLinhaApr(a: Apr): Tom {
  if (a.status === StatusApr.Reprovada) return 'alerta';
  if (a.status === StatusApr.AguardandoAprovacao || aprVencendo(a)) return 'atencao';
  if (a.status === StatusApr.Aprovada) return 'ok';
  return 'neutro';
}

// Fração do prazo de validade já consumida (data da APR até a validade).
function fracaoValidade(a: Apr): number {
  if (!a.validade) return 0;
  const ini = new Date(a.data.slice(0, 10) + 'T00:00:00').getTime();
  const fim = new Date(a.validade.slice(0, 10) + 'T00:00:00').getTime();
  if (!(fim > ini)) return 1;
  return Math.max(0, Math.min(1, (Date.now() - ini) / (fim - ini)));
}

export function AprsTab() {
  const souAdministrador = useSouAdministrador();
  const navigate = useNavigate();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [aprs, setAprs] = useState<Apr[]>([]);
  // Finalizados não podem ser excluídos (o servidor recusa): o botão some deles.
  const travados = useRegistrosTravados('Apr', aprs.map((a) => a.id));
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [novaApr, setNovaApr] = useState<NovaApr>(aprVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [montandoEquipe, setMontandoEquipe] = useState(false);
  const [nomeNovaEquipe, setNomeNovaEquipe] = useState('');
  const [encarregadoNovaEquipe, setEncarregadoNovaEquipe] = useState('');
  const [salvandoEquipe, setSalvandoEquipe] = useState(false);
  const [painelAberto, setPainelAberto] = useState(false);
  const [filtro, setFiltro] = useState<FiltroApr>('todas');
  const [busca, setBusca] = useState('');
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, ativs, trabs, equips] = await Promise.all([
        api.aprs.listar(),
        api.atividades.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
      ]);
      setAprs(lista);
      setAtividades(ativs);
      setTrabalhadores(trabs);
      setEquipes(equips);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar APRs.');
    } finally {
      setCarregandoLista(false);
    }
  }

  const obraIdAtividadeSelecionada = atividades.find((a) => a.id === novaApr.atividadeId)?.obraId;
  const equipesDaObra = obraIdAtividadeSelecionada
    ? equipes.filter((e) => e.obraId === obraIdAtividadeSelecionada)
    : equipes;

  useEffect(() => {
    carregar();
  }, []);

  // Equipe na APR (24/09/2026): escolher a equipe marca os membros dela como responsáveis — dá
  // para desmarcar ou incluir alguém depois. "Nenhuma" não mexe na seleção.
  function escolherEquipe(equipeId: string) {
    const equipe = equipes.find((e) => e.id === equipeId);
    setNovaApr((atual) => ({
      ...atual,
      equipeId: equipeId || null,
      responsaveisIds: equipe ? [...equipe.trabalhadorIds] : atual.responsaveisIds,
    }));
  }

  const responsaveisSelecionados = trabalhadores.filter((t) => novaApr.responsaveisIds.includes(t.id));
  // Trabalhador pertence a uma equipe só: quem já está em outra será movido para a nova.
  const seraoMovidos = responsaveisSelecionados.filter((t) => t.equipeId);

  function abrirMontagemEquipe() {
    setNomeNovaEquipe('');
    setEncarregadoNovaEquipe('');
    setMontandoEquipe(true);
  }

  async function salvarEquipe() {
    if (!obraIdAtividadeSelecionada) return;
    try {
      setSalvandoEquipe(true);
      setErro(null);
      const { id } = await api.equipes.criarComMembros({
        obraId: obraIdAtividadeSelecionada,
        nome: nomeNovaEquipe.trim(),
        encarregadoId: encarregadoNovaEquipe || null,
        trabalhadorIds: novaApr.responsaveisIds,
      });
      const [equips, trabs] = await Promise.all([api.equipes.listar(), api.trabalhadores.listar()]);
      setEquipes(equips);
      setTrabalhadores(trabs);
      setNovaApr((atual) => ({ ...atual, equipeId: id }));
      setMontandoEquipe(false);
      sucessoToast(`Equipe "${nomeNovaEquipe.trim()}" criada.`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar a equipe.');
    } finally {
      setSalvandoEquipe(false);
    }
  }

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.aprs.criar({
        ...novaApr,
        validade: novaApr.validade || null,
      });
      setNovaApr(aprVazia());
      setPainelAberto(false);
      await carregar();
      sucessoToast('APR criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar APR.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta APR? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.aprs.excluir(id);
      await carregar();
      sucessoToast('APR excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir APR.');
    }
  }

  function fecharPainel() {
    setPainelAberto(false);
    setNovaApr(aprVazia());
  }

  function abrirPdf(a: Apr) {
    void visualizar({ titulo: 'PDF da APR', nomeArquivo: `apr-${a.id}.pdf`, obter: () => api.aprs.exportarPdf(a.id) });
  }

  const contagem = useMemo(() => {
    const c: Record<FiltroApr, number> = { todas: aprs.length, elaboracao: 0, aguardando: 0, aprovadas: 0, vencendo: 0, fechadas: 0 };
    for (const a of aprs) for (const f of ['elaboracao', 'aguardando', 'aprovadas', 'vencendo', 'fechadas'] as const) if (passaNoFiltroApr(a, f)) c[f] += 1;
    return c;
  }, [aprs]);
  const termo = busca.trim().toLocaleLowerCase('pt-BR');
  const aprsVisiveis = aprs.filter(
    (a) =>
      passaNoFiltroApr(a, filtro) &&
      (!termo || [a.numeroApr, a.atividadeNome, a.local, a.obraNome, a.elaboradorNome].some((v) => v?.toLocaleLowerCase('pt-BR').includes(termo))),
  );

  const aviso = (texto: string) => <span style={{ color: tokensUi.status.atencao.tinta, fontWeight: 600 }}>{texto}</span>;
  const fonteDoc = { fontFamily: "'Roboto Mono', ui-monospace, monospace" };
  const colunas: Coluna<Apr>[] = [
    {
      chave: 'atividadeNome',
      rotulo: 'Atividade',
      render: (a) => {
        const semPessoas = a.status === StatusApr.EmElaboracao && !a.elaboradorTrabalhadorId && !a.supervisorTrabalhadorId;
        return (
          <CelulaDupla
            inicio={
              <span aria-hidden="true" style={{ flexShrink: 0, width: 34, height: 34, borderRadius: 10, display: 'grid', placeItems: 'center', color: 'var(--sst-acao-marca-tinta)', backgroundColor: 'var(--sst-acao-marca-wash)' }}>
                <Warning20Regular />
              </span>
            }
            titulo={a.atividadeNome}
            detalhe={
              <>
                {a.numeroApr && <span style={fonteDoc}>{a.numeroApr}</span>}
                {semPessoas && <> · {aviso('sem elaborador e sem supervisor')}</>}
                {a.status === StatusApr.Reprovada && a.motivoReprovacao && <> · {a.motivoReprovacao}</>}
              </>
            }
          />
        );
      },
    },
    {
      chave: 'local',
      rotulo: 'Obra / local',
      render: (a) => <CelulaDupla tituloLeve titulo={a.obraNome ?? '—'} detalhe={a.local || undefined} />,
    },
    {
      chave: 'elaborador',
      rotulo: 'Elaboração',
      render: (a) =>
        a.elaboradorNome ? (
          <CelulaDupla
            tituloLeve
            inicio={<FotoTrabalhador nome={a.elaboradorNome} trabalhadorId={a.elaboradorTrabalhadorId ?? ''} temFoto={a.elaboradorTemFoto} tamanho={36} />}
            titulo={a.elaboradorNome}
            detalhe={a.supervisorNome ? `Supervisão: ${a.supervisorNome}` : undefined}
          />
        ) : (
          <span style={{ fontSize: 12, color: tokensUi.status.neutro.tinta }}>Não definida</span>
        ),
    },
    { chave: 'data', rotulo: 'Data', render: (a) => dataBr(a.data) },
    {
      chave: 'validade',
      rotulo: 'Validade',
      render: (a) => {
        if (!a.validade) return <span style={{ fontSize: 12 }}>{aviso('Validade não definida')}</span>;
        const dias = diasAteVencer(a.validade);
        const nivel = a.status === StatusApr.Aprovada ? nivelVencimento(a.validade) : null;
        return (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4, width: 110 }}>
            <span>{dataBr(a.validade)}</span>
            {a.status === StatusApr.Aprovada && (
              <BarraProgresso percentual={fracaoValidade(a) * 100} tom={nivel ? tomDeVencimento(nivel) : 'ok'} aria-label="Prazo de validade já consumido" />
            )}
            {aprVencendo(a) && dias !== null && <span style={{ fontSize: 11.5 }}>{aviso(dias === 0 ? 'vence hoje' : `vence em ${dias} ${dias === 1 ? 'dia' : 'dias'}`)}</span>}
          </div>
        );
      },
    },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (a) => <StatusChip tom={tomPorStatusApr[a.status] ?? 'neutro'}>{statusAprLabel[a.status]}</StatusChip>,
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {/* 25/11/2026 (docs/mockups/dds-apr-pt-visual-2026-11-25.html): o formulário ficava sempre aberto,
          com a grade de todas as pessoas, e a lista só aparecia no fim da página. Agora ele abre pelo
          botão "Nova APR" (PainelCriacaoInline, mesmo padrão de Inspeções e DDS). Nenhum campo mudou. */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="APRs"
        subtitulo="Análise Preliminar de Risco"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-apr"
          >
            {painelAberto ? 'Fechar' : 'Nova APR'}
          </Button>
        }
      />
      <div id="painel-nova-apr">
      <PainelCriacaoInline aberto={painelAberto} titulo="Nova APR">
        <FormSection titulo="Dados da APR" numero={1} primeira>
          <FormGrid>
            <Campo span={4}>
              <Field label="Atividade">
                <Select
                  value={novaApr.atividadeId}
                  onChange={(_, d) => setNovaApr({ ...novaApr, atividadeId: d.value })}
                >
                  <option value="">Selecione</option>
                  {atividades.map((atividade) => (
                    <option key={atividade.id} value={atividade.id}>
                      {atividade.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Local / Frente">
                <Input value={novaApr.local} onChange={(_, d) => setNovaApr({ ...novaApr, local: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Máquinas / Equip.">
                <Input
                  value={novaApr.maquinasEquipamentos ?? ''}
                  onChange={(_, d) => setNovaApr({ ...novaApr, maquinasEquipamentos: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="PGR / Procedimento ref.">
                <Input
                  value={novaApr.pgrReferencia ?? ''}
                  onChange={(_, d) => setNovaApr({ ...novaApr, pgrReferencia: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Equipe">
                <Select
                  value={novaApr.equipeId ?? ''}
                  onChange={(_, d) => escolherEquipe(d.value)}
                >
                  <option value="">Nenhuma</option>
                  {equipesDaObra.map((equipe) => (
                    <option key={equipe.id} value={equipe.id}>
                      {equipe.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Data">
                <CampoData value={novaApr.data} onChange={(_, d) => setNovaApr({ ...novaApr, data: d.value })} />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Validade">
                <CampoData
                  value={novaApr.validade ?? ''}
                  onChange={(_, d) => setNovaApr({ ...novaApr, validade: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={12}>
              {!montandoEquipe ? (
                <div style={{ display: 'flex', alignItems: 'center', gap: 12, flexWrap: 'wrap' }}>
                  <Button
                    icon={<PeopleTeamAdd24Regular />}
                    onClick={abrirMontagemEquipe}
                    disabled={!obraIdAtividadeSelecionada || novaApr.responsaveisIds.length === 0}
                  >
                    Salvar seleção como equipe
                  </Button>
                  <span style={{ fontSize: 12, opacity: 0.75 }}>
                    {!obraIdAtividadeSelecionada
                      ? 'Escolha a atividade para saber de qual obra é a equipe.'
                      : novaApr.responsaveisIds.length === 0
                        ? 'Marque os responsáveis que formam a equipe.'
                        : `${novaApr.responsaveisIds.length} responsável(is) marcado(s).`}
                  </span>
                </div>
              ) : (
                <FormGrid>
                  <Campo span={4}>
                    <Field label="Nome da equipe" required>
                      <Input
                        value={nomeNovaEquipe}
                        onChange={(_, d) => setNomeNovaEquipe(d.value)}
                        placeholder="Ex.: Armação – Frente 2"
                      />
                    </Field>
                  </Campo>
                  <Campo span={4}>
                    <Field label="Encarregado">
                      <Select value={encarregadoNovaEquipe} onChange={(_, d) => setEncarregadoNovaEquipe(d.value)}>
                        <option value="">Sem encarregado definido</option>
                        {responsaveisSelecionados.map((t) => (
                          <option key={t.id} value={t.id}>
                            {t.nome}
                          </option>
                        ))}
                      </Select>
                    </Field>
                  </Campo>
                  <Campo span={12}>
                    <span style={{ fontSize: 12 }}>
                      {responsaveisSelecionados.length} membro(s): {responsaveisSelecionados.map((t) => t.nome).join(', ')}.
                      {seraoMovidos.length > 0 &&
                        ` ${seraoMovidos.length} já está(ão) em outra equipe e será(ão) movido(s) para esta.`}
                    </span>
                  </Campo>
                  <Campo span={12}>
                    <div style={{ display: 'flex', gap: 8 }}>
                      <Button
                        appearance="primary"
                        onClick={salvarEquipe}
                        disabled={salvandoEquipe || nomeNovaEquipe.trim() === ''}
                      >
                        Criar equipe
                      </Button>
                      <Button onClick={() => setMontandoEquipe(false)} disabled={salvandoEquipe}>
                        Cancelar
                      </Button>
                    </div>
                  </Campo>
                </FormGrid>
              )}
            </Campo>
            {/* 05/11: quem elabora e quem supervisiona — assinam por biometria, como os envolvidos. */}
            <Campo span={6}>
              <Field label="Elaboração (técnico / encarregado)">
                <Select
                  value={novaApr.elaboradorTrabalhadorId ?? ''}
                  onChange={(_, d) => setNovaApr((atual) => ({ ...atual, elaboradorTrabalhadorId: d.value || null }))}
                >
                  <option value="">Selecione</option>
                  {trabalhadores.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={6}>
              <Field label="Supervisão">
                <Select
                  value={novaApr.supervisorTrabalhadorId ?? ''}
                  onChange={(_, d) => setNovaApr((atual) => ({ ...atual, supervisorTrabalhadorId: d.value || null }))}
                >
                  <option value="">Selecione</option>
                  {trabalhadores.map((t) => (
                    <option key={t.id} value={t.id}>
                      {t.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Responsáveis">
                <ListaSelecaoMultipla
                  aria-label="Responsáveis"
                  opcoes={trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome }))}
                  selecionados={novaApr.responsaveisIds}
                  aoMudar={(atualizar) => setNovaApr((atual) => ({ ...atual, responsaveisIds: atualizar(atual.responsaveisIds) }))}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button appearance="primary" icon={<Add24Regular />} onClick={criar} disabled={carregando}>
              Adicionar APR
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
      </div>

      <Card densidade="compacta">
        {aprs.length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <FiltroRapido<FiltroApr>
              aria-label="Filtrar APRs pela situação"
              valor={filtro}
              aoMudar={setFiltro}
              opcoes={[
                { valor: 'todas', rotulo: 'Todas', contagem: contagem.todas },
                { valor: 'elaboracao', rotulo: 'Em elaboração', contagem: contagem.elaboracao },
                { valor: 'aguardando', rotulo: 'Aguardando aprovação', contagem: contagem.aguardando },
                { valor: 'aprovadas', rotulo: 'Aprovadas', contagem: contagem.aprovadas },
                { valor: 'vencendo', rotulo: 'Vencem em breve', contagem: contagem.vencendo },
                { valor: 'fechadas', rotulo: 'Reprovadas e encerradas', contagem: contagem.fechadas },
              ]}
            />
            <Input
              aria-label="Buscar número, atividade, local ou obra"
              placeholder="Buscar nº, atividade, local ou obra"
              contentBefore={<Search20Regular />}
              value={busca}
              onChange={(_, d) => setBusca(d.value)}
              style={{ minWidth: 260 }}
            />
          </div>
        )}
        <DataTable
          aria-label="APRs cadastradas"
          colunas={colunas}
          linhas={aprsVisiveis}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          tomLinha={tomDaLinhaApr}
          vazio={aprs.length > 0
            ? { titulo: 'Nenhuma APR nesta seleção', descricao: 'Troque o filtro ou a busca para ver outras APRs.' }
            : { titulo: 'Nenhuma APR cadastrada ainda.', acao: { rotulo: 'Nova APR', aoClicar: () => setPainelAberto(true) } }}
          // 18/11/2026: APR aprovada ou encerrada abre o PDF; as demais seguem para o preenchimento.
          aoClicarLinha={(a) =>
            a.status === StatusApr.Aprovada || a.status === StatusApr.Encerrada
              ? abrirPdf(a)
              : navigate(`/operacao/apr/${a.id}`)
          }
          // Ação principal (25/11/2026): em elaboração → Continuar; aguardando aprovação → Aprovar (no
          // detalhe, onde a decisão já acontece); aprovada/encerrada → PDF. Reprovada → Abrir.
          acoesLinha={(a) => (
            <>
              {a.status === StatusApr.EmElaboracao && (
                <BotaoAcao tom="ver" icon={<Play24Regular />} aria-label="Continuar a APR" onClick={(e) => { e.stopPropagation(); navigate(`/operacao/apr/${a.id}`); }}>
                  Continuar
                </BotaoAcao>
              )}
              {a.status === StatusApr.AguardandoAprovacao && (
                <BotaoAcao tom="baixar" aria-label="Aprovar ou reprovar a APR" onClick={(e) => { e.stopPropagation(); navigate(`/operacao/apr/${a.id}`); }}>
                  Aprovar
                </BotaoAcao>
              )}
              {(a.status === StatusApr.Aprovada || a.status === StatusApr.Encerrada) && navigator.onLine && (
                <BotaoAcao tom="baixar" icon={<DocumentPdf24Regular />} aria-label="Abrir o PDF da APR" onClick={(e) => { e.stopPropagation(); abrirPdf(a); }}>
                  PDF
                </BotaoAcao>
              )}
              {a.status !== StatusApr.EmElaboracao && (
                <BotaoAcao tom="ver" icon={<Open24Regular />} aria-label="Abrir detalhe da APR" title="Abrir detalhe" onClick={(e) => { e.stopPropagation(); navigate(`/operacao/apr/${a.id}`); }} />
              )}
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais) — ver PoliticasAutorizacao. */}
              {souAdministrador && !travados.has(a.id) && (
                <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={(e) => { e.stopPropagation(); void excluir(a.id); }} aria-label="Excluir" />
              )}
            </>
          )}
        />
      </Card>
      </div>
      {dialogElement}
      {dialogoVisualizador}
    </>
  );
}
