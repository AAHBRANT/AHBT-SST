import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  CampoData,
  CelulaDupla,
  FiltroRapido,
  ListaSelecaoMultipla,
  PageHeader,
  PainelCriacaoInline,
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
import { Add24Regular, Delete24Regular, DocumentPdf24Regular, Open24Regular, Play24Regular, Search20Regular, ShieldCheckmark20Regular } from '@fluentui/react-icons';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { FotoTrabalhador } from '../../components/FotoTrabalhador';
import {
  api,
  StatusPt,
  statusPtLabel,
  type Atividade,
  type Equipe,
  type NovaPermissaoTrabalho,
  type PermissaoTrabalho,
  type Trabalhador,
  type Usuario,
} from '../../lib/api';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { dataBr, hojeIso } from '../../lib/datas';
import { useRegistrosTravados } from '../../hooks/useRegistrosTravados';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

function ptVazia(): NovaPermissaoTrabalho {
  return {
    atividadeId: '',
    descricaoAtividade: '',
    local: '',
    empresaExecutante: '',
    equipeId: null,
    data: hojeIso(),
    horarioInicio: null,
    horarioFim: null,
    validade: null,
    responsavelExecucaoUsuarioId: null,
    responsavelAreaUsuarioId: null,
    responsaveisIds: [],
  };
}

// Mapeamento 1:1 pelo nome semântico do Fluent (Guia de conversão item 5), preservando as mesmas
// cores da versão anterior (Badge color=): subtle→neutro, success→ok, warning→atencao, informative→info.
const tomPorStatusPt: Record<number, Tom> = {
  1: 'neutro',
  2: 'ok',
  3: 'atencao',
  4: 'info',
};

// Onda 2 Task 6 (camada ui/): lista + formulário de criação de Permissão de Trabalho. Mesmo padrão
// de AprsTab.tsx (Task 11) — Card + FormSection + DataTable, seleção de responsáveis em ChipCheckboxGroup.
// Visual e utilidade aprovados em 25/11/2026 (docs/mockups/dds-apr-pt-visual-2026-11-25.html).
type FiltroPt = 'todas' | 'vigor' | 'elaboracao' | 'suspensas' | 'encerradas';

function diaLocal(iso: string): number {
  return new Date(iso.slice(0, 10) + 'T00:00:00').getTime();
}

// "Em vigor hoje": autorizada e hoje dentro do período da PT (data até a validade, ou só a data).
function emVigorHoje(pt: PermissaoTrabalho): boolean {
  if (pt.status !== StatusPt.Autorizada) return false;
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  const t = hoje.getTime();
  return diaLocal(pt.data) <= t && t <= diaLocal(pt.validade ?? pt.data);
}

function passaNoFiltroPt(pt: PermissaoTrabalho, f: FiltroPt): boolean {
  switch (f) {
    case 'vigor': return emVigorHoje(pt);
    case 'elaboracao': return pt.status === StatusPt.EmElaboracao;
    case 'suspensas': return pt.status === StatusPt.Suspensa;
    case 'encerradas': return pt.status === StatusPt.Encerrada;
    default: return true;
  }
}

function tomDaLinhaPt(pt: PermissaoTrabalho): Tom {
  if (pt.status === StatusPt.Suspensa) return 'atencao';
  if (emVigorHoje(pt)) return 'ok';
  if (pt.status === StatusPt.Encerrada) return 'info';
  return 'neutro';
}

const hora = (h?: string | null) => (h ? h.slice(0, 5) : null);

export function PermissoesTrabalhoTab() {
  const souAdministrador = useSouAdministrador();
  const navigate = useNavigate();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [permissoes, setPermissoes] = useState<PermissaoTrabalho[]>([]);
  // Finalizados não podem ser excluídos (o servidor recusa): o botão some deles.
  const travados = useRegistrosTravados('PermissaoTrabalho', permissoes.map((pt) => pt.id));
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [equipes, setEquipes] = useState<Equipe[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaPt, setNovaPt] = useState<NovaPermissaoTrabalho>(ptVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [painelAberto, setPainelAberto] = useState(false);
  const [filtro, setFiltro] = useState<FiltroPt>('todas');
  const [busca, setBusca] = useState('');
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();

  async function carregar() {
    try {
      setErro(null);
      const [lista, ativs, trabs, equips, usrs] = await Promise.all([
        api.permissoesTrabalho.listar(),
        api.atividades.listar(),
        api.trabalhadores.listar(),
        api.equipes.listar(),
        api.usuarios.listar().catch(() => [] as Usuario[]),
      ]);
      setPermissoes(lista);
      setAtividades(ativs);
      setTrabalhadores(trabs);
      setEquipes(equips);
      setUsuarios(usrs);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar Permissões de Trabalho.');
    } finally {
      setCarregandoLista(false);
    }
  }

  const obraIdAtividadeSelecionada = atividades.find((a) => a.id === novaPt.atividadeId)?.obraId;
  const equipesDaObra = obraIdAtividadeSelecionada
    ? equipes.filter((e) => e.obraId === obraIdAtividadeSelecionada)
    : equipes;

  useEffect(() => {
    carregar();
  }, []);

  async function criar() {
    try {
      setCarregando(true);
      setErro(null);
      await api.permissoesTrabalho.criar({
        ...novaPt,
        empresaExecutante: novaPt.empresaExecutante || null,
        equipeId: novaPt.equipeId || null,
        horarioInicio: novaPt.horarioInicio ? `${novaPt.horarioInicio}:00` : null,
        horarioFim: novaPt.horarioFim ? `${novaPt.horarioFim}:00` : null,
        validade: novaPt.validade || null,
        responsavelExecucaoUsuarioId: novaPt.responsavelExecucaoUsuarioId || null,
        responsavelAreaUsuarioId: novaPt.responsavelAreaUsuarioId || null,
      });
      setNovaPt(ptVazia());
      setPainelAberto(false);
      await carregar();
      sucessoToast('Permissão de Trabalho criada com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao criar Permissão de Trabalho.');
    } finally {
      setCarregando(false);
    }
  }

  async function excluir(id: string) {
    if (!(await confirmar('Excluir esta Permissão de Trabalho? Essa ação não pode ser desfeita.'))) return;
    try {
      await api.permissoesTrabalho.excluir(id);
      await carregar();
      sucessoToast('Permissão de Trabalho excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir Permissão de Trabalho.');
    }
  }

  function fecharPainel() {
    setPainelAberto(false);
    setNovaPt(ptVazia());
  }

  function abrirPdf(pt: PermissaoTrabalho) {
    void visualizar({ titulo: 'PDF da Permissão de Trabalho', nomeArquivo: `pt-${pt.id}.pdf`, obter: () => api.permissoesTrabalho.exportarPdf(pt.id) });
  }

  const contagem = useMemo(() => {
    const c: Record<FiltroPt, number> = { todas: permissoes.length, vigor: 0, elaboracao: 0, suspensas: 0, encerradas: 0 };
    for (const pt of permissoes) for (const f of ['vigor', 'elaboracao', 'suspensas', 'encerradas'] as const) if (passaNoFiltroPt(pt, f)) c[f] += 1;
    return c;
  }, [permissoes]);
  const termo = busca.trim().toLocaleLowerCase('pt-BR');
  const permissoesVisiveis = permissoes.filter(
    (pt) =>
      passaNoFiltroPt(pt, filtro) &&
      (!termo ||
        [pt.numeroPt, pt.atividadeNome, pt.descricaoAtividade, pt.local, pt.obraNome, pt.empresaExecutante, pt.responsavelExecucaoUsuarioNome]
          .some((v) => v?.toLocaleLowerCase('pt-BR').includes(termo))),
  );

  const aviso = (texto: string) => <span style={{ color: tokensUi.status.atencao.tinta, fontWeight: 600 }}>{texto}</span>;
  const fonteDoc = { fontFamily: "'Roboto Mono', ui-monospace, monospace" };
  const colunas: Coluna<PermissaoTrabalho>[] = [
    {
      chave: 'atividadeNome',
      rotulo: 'Atividade',
      render: (pt) => {
        const faltando = pt.status === StatusPt.EmElaboracao
          ? [!pt.horarioInicio && !pt.horarioFim ? 'horário' : null, !pt.responsavelExecucaoUsuarioId ? 'responsáveis' : null].filter(Boolean)
          : [];
        return (
          <CelulaDupla
            inicio={
              <span aria-hidden="true" style={{ flexShrink: 0, width: 34, height: 34, borderRadius: 10, display: 'grid', placeItems: 'center', color: 'var(--sst-acao-marca-tinta)', backgroundColor: 'var(--sst-acao-marca-wash)' }}>
                <ShieldCheckmark20Regular />
              </span>
            }
            titulo={pt.descricaoAtividade || pt.atividadeNome}
            detalhe={
              <>
                {pt.numeroPt && <span style={fonteDoc}>{pt.numeroPt}</span>}
                {pt.empresaExecutante && <> · {pt.empresaExecutante}</>}
                {faltando.length > 0 && <> · {aviso(`sem ${faltando.join(' e sem ')}`)}</>}
              </>
            }
          />
        );
      },
    },
    {
      chave: 'local',
      rotulo: 'Obra / local',
      render: (pt) => <CelulaDupla tituloLeve titulo={pt.obraNome ?? '—'} detalhe={pt.local || undefined} />,
    },
    {
      chave: 'data',
      rotulo: 'Data e horário',
      render: (pt) => {
        const inicio = hora(pt.horarioInicio);
        const fim = hora(pt.horarioFim);
        const horario = inicio && fim ? `${inicio} às ${fim}` : inicio ? `a partir das ${inicio}` : fim ? `até ${fim}` : 'horário não definido';
        return (
          <CelulaDupla
            tituloLeve
            titulo={<span style={{ fontVariantNumeric: 'tabular-nums' }}>{dataBr(pt.data)}{pt.validade && pt.validade.slice(0, 10) !== pt.data.slice(0, 10) ? ` a ${dataBr(pt.validade)}` : ''}</span>}
            detalhe={
              emVigorHoje(pt)
                ? <><strong style={{ color: tokensUi.status.ok.tinta }}>em vigor</strong> · {horario}</>
                : horario
            }
          />
        );
      },
    },
    {
      chave: 'responsavel',
      rotulo: 'Responsável pela execução',
      render: (pt) =>
        pt.responsavelExecucaoUsuarioNome ? (
          <CelulaDupla
            tituloLeve
            inicio={<FotoTrabalhador nome={pt.responsavelExecucaoUsuarioNome} trabalhadorId={pt.responsavelExecucaoTrabalhadorId ?? ''} temFoto={pt.responsavelExecucaoTemFoto} tamanho={36} />}
            titulo={pt.responsavelExecucaoUsuarioNome}
          />
        ) : (
          <span style={{ fontSize: 12 }}>{aviso('Não definido')}</span>
        ),
    },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (pt) => <StatusChip tom={tomPorStatusPt[pt.status] ?? 'neutro'}>{statusPtLabel[pt.status]}</StatusChip>,
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
          botão "Nova PT" (PainelCriacaoInline, mesmo padrão de Inspeções e DDS). Nenhum campo mudou. */}
      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <PageHeader
        titulo="Permissões de Trabalho"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-pt"
          >
            {painelAberto ? 'Fechar' : 'Nova PT'}
          </Button>
        }
      />
      <div id="painel-nova-pt">
      <PainelCriacaoInline aberto={painelAberto} titulo="Nova Permissão de Trabalho">
        <FormSection titulo="Dados Gerais" numero={1} primeira>
          <FormGrid>
            <Campo span={3}>
              <Field label="Atividade">
                <Select value={novaPt.atividadeId} onChange={(_, d) => setNovaPt({ ...novaPt, atividadeId: d.value })}>
                  <option value="">Selecione</option>
                  {atividades.map((atividade) => (
                    <option key={atividade.id} value={atividade.id}>
                      {atividade.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Descrição da atividade">
                <Input
                  value={novaPt.descricaoAtividade}
                  onChange={(_, d) => setNovaPt({ ...novaPt, descricaoAtividade: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={3}>
              <Field label="Local">
                <Input value={novaPt.local} onChange={(_, d) => setNovaPt({ ...novaPt, local: d.value })} />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Empresa executante">
                <Input
                  value={novaPt.empresaExecutante ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, empresaExecutante: d.value })}
                />
              </Field>
            </Campo>
            <Campo span={4}>
              <Field label="Equipe">
                <Select
                  value={novaPt.equipeId ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, equipeId: d.value || null })}
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
          </FormGrid>
        </FormSection>

        <FormSection titulo="Prazos e Responsáveis" numero={2}>
          <FormGrid>
            <Campo span={2}>
              <Field label="Data">
                <CampoData value={novaPt.data} onChange={(_, d) => setNovaPt({ ...novaPt, data: d.value })} />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Horário início">
                <Input
                  type="time"
                  value={novaPt.horarioInicio ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, horarioInicio: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Horário fim">
                <Input
                  type="time"
                  value={novaPt.horarioFim ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, horarioFim: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Validade">
                <CampoData
                  value={novaPt.validade ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, validade: d.value || null })}
                />
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Responsável pela execução">
                <Select
                  value={novaPt.responsavelExecucaoUsuarioId ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, responsavelExecucaoUsuarioId: d.value || null })}
                >
                  <option value="">Não definido</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={2}>
              <Field label="Responsável pela área">
                <Select
                  value={novaPt.responsavelAreaUsuarioId ?? ''}
                  onChange={(_, d) => setNovaPt({ ...novaPt, responsavelAreaUsuarioId: d.value || null })}
                >
                  <option value="">Não definido</option>
                  {usuarios.map((usuario) => (
                    <option key={usuario.id} value={usuario.id}>
                      {usuario.nome}
                    </option>
                  ))}
                </Select>
              </Field>
            </Campo>
            <Campo span={12}>
              <Field label="Equipe executante (responsáveis)">
                <ListaSelecaoMultipla
                  aria-label="Equipe executante (responsáveis)"
                  opcoes={trabalhadores.map((t) => ({ id: t.id, rotulo: t.nome }))}
                  selecionados={novaPt.responsaveisIds}
                  aoMudar={(atualizar) => setNovaPt((atual) => ({ ...atual, responsaveisIds: atualizar(atual.responsaveisIds) }))}
                />
              </Field>
            </Campo>
          </FormGrid>
          <FormRodape>
            <Button onClick={fecharPainel}>Cancelar</Button>
            <Button
              appearance="primary"
              icon={<Add24Regular />}
              onClick={criar}
              disabled={carregando || !novaPt.atividadeId || !novaPt.descricaoAtividade || !novaPt.local || !novaPt.data}
            >
              Adicionar PT
            </Button>
          </FormRodape>
        </FormSection>
      </PainelCriacaoInline>
      </div>

      <Card densidade="compacta">
        {permissoes.length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <FiltroRapido<FiltroPt>
              aria-label="Filtrar Permissões de Trabalho pela situação"
              valor={filtro}
              aoMudar={setFiltro}
              opcoes={[
                { valor: 'todas', rotulo: 'Todas', contagem: contagem.todas },
                { valor: 'vigor', rotulo: 'Em vigor hoje', contagem: contagem.vigor },
                { valor: 'elaboracao', rotulo: 'Em elaboração', contagem: contagem.elaboracao },
                { valor: 'suspensas', rotulo: 'Suspensas', contagem: contagem.suspensas },
                { valor: 'encerradas', rotulo: 'Encerradas', contagem: contagem.encerradas },
              ]}
            />
            <Input
              aria-label="Buscar número, atividade, local ou empresa"
              placeholder="Buscar nº, atividade, local ou empresa"
              contentBefore={<Search20Regular />}
              value={busca}
              onChange={(_, d) => setBusca(d.value)}
              style={{ minWidth: 260 }}
            />
          </div>
        )}
        <DataTable
          aria-label="Permissões de Trabalho cadastradas"
          colunas={colunas}
          linhas={permissoesVisiveis}
          chaveLinha={(pt) => pt.id}
          carregando={carregandoLista}
          tomLinha={tomDaLinhaPt}
          vazio={permissoes.length > 0
            ? { titulo: 'Nenhuma PT nesta seleção', descricao: 'Troque o filtro ou a busca para ver outras PTs.' }
            : { titulo: 'Nenhuma Permissão de Trabalho cadastrada ainda.', acao: { rotulo: 'Nova PT', aoClicar: () => setPainelAberto(true) } }}
          // 18/11/2026: PT já autorizada (ou suspensa/encerrada) abre o PDF; em elaboração segue para o
          // preenchimento. Assinatura da equipe e demais ações ficam no detalhe, pelo "Abrir".
          aoClicarLinha={(pt) =>
            pt.status !== StatusPt.EmElaboracao
              ? abrirPdf(pt)
              : navigate(`/operacao/pt/${pt.id}`)
          }
          // Ação principal (25/11/2026): em elaboração → Continuar (é o próprio detalhe); autorizada ou
          // encerrada → PDF + Abrir; suspensa → Abrir.
          acoesLinha={(pt) => (
            <>
              {pt.status === StatusPt.EmElaboracao ? (
                <BotaoAcao tom="ver" icon={<Play24Regular />} aria-label="Continuar a PT" onClick={(e) => { e.stopPropagation(); navigate(`/operacao/pt/${pt.id}`); }}>
                  Continuar
                </BotaoAcao>
              ) : (
                <>
                  {(pt.status === StatusPt.Autorizada || pt.status === StatusPt.Encerrada) && navigator.onLine && (
                    <BotaoAcao tom="baixar" icon={<DocumentPdf24Regular />} aria-label="Abrir o PDF da PT" onClick={(e) => { e.stopPropagation(); abrirPdf(pt); }}>
                      PDF
                    </BotaoAcao>
                  )}
                  <BotaoAcao tom="ver" icon={<Open24Regular />} aria-label="Abrir detalhe da PT" title="Abrir detalhe" onClick={(e) => { e.stopPropagation(); navigate(`/operacao/pt/${pt.id}`); }} />
                </>
              )}
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais) — ver PoliticasAutorizacao. */}
              {souAdministrador && !travados.has(pt.id) && (
                <BotaoAcao tom="excluir" icon={<Delete24Regular />} onClick={(e) => { e.stopPropagation(); void excluir(pt.id); }} aria-label="Excluir" />
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
