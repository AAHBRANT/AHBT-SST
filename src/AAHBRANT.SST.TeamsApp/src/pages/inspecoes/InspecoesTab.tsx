import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  BotaoAcao,
  Button,
  Campo,
  Card,
  CampoData,
  CelulaDupla,
  DataTable,
  Field,
  FeedbackInline,
  FiltroRapido,
  FormGrid,
  FormRodape,
  FormSection,
  Input,
  PageHeader,
  PainelCriacaoInline,
  Select,
  StatusChip,
  tokensUi,
  useConfirmar,
  type Coluna,
  type Tom,
} from '@ui';
import {
  Add24Regular,
  CheckmarkCircle16Regular,
  ClipboardTaskListLtr20Regular,
  Delete24Regular,
  DocumentPdf24Regular,
  Home20Regular,
  Open24Regular,
  Play24Regular,
  Search20Regular,
  VehicleTruckProfile20Regular,
} from '@fluentui/react-icons';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { dataBr, hojeIso } from '../../lib/datas';
import { useRegistrosTravados } from '../../hooks/useRegistrosTravados';
import { assinarMudancasSync } from '../../lib/offline/syncEngine';
import { criarInspecaoCampo, mesclarInspecoesLocais, obterPreparacaoInspecoes, prepararInspecoesOffline, type InspecaoCampo } from '../../lib/offline/inspecoes';
import { conferirPendencias } from '../../lib/avisoErro';
import { useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { FotoTrabalhador } from '../../components/FotoTrabalhador';
import {
  api,
  StatusInspecao,
  statusInspecaoLabel,
  tipoInspecaoLabel,
  TipoInspecao,
  type Atividade,
  type ChecklistModelo,
  type Inspecao,
  type NovaInspecao,
  type Obra,
  type Usuario,
} from '../../lib/api';

function criarInspecaoVazia(): NovaInspecao {
  return {
    checklistModeloId: '',
    obraId: '',
    atividadeId: null,
    data: hojeIso(),
    responsavelUsuarioId: '',
  };
}

// EmAndamento/Concluida (StatusInspecao) — mesmo julgamento do guia item 5: em andamento ainda não
// terminou (info), concluída é o estado positivo (ok).
const tomPorStatusInspecao: Record<number, Tom> = {
  1: 'info',
  2: 'ok',
  3: 'atencao',
};

// Visual e utilidade da lista aprovados em 25/11/2026 (docs/mockups/inspecoes-visual-2026-11-25.html).
// Em andamento há mais de DIAS_PARADA dias = inspeção parada (faixa âmbar e "há N dias" em destaque).
const DIAS_PARADA = 30;
const MS_DIA = 24 * 60 * 60 * 1000;

type FiltroInspecao = 'todas' | 'andamento' | 'assinatura' | 'nc' | 'concluidas';

function diasDesde(dataIso: string): number {
  const d = new Date(dataIso.slice(0, 10) + 'T00:00:00');
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  return Math.max(0, Math.round((hoje.getTime() - d.getTime()) / MS_DIA));
}

function estaParada(i: Inspecao): boolean {
  return i.status === StatusInspecao.EmAndamento && diasDesde(i.data) > DIAS_PARADA;
}

function passaNoFiltro(i: Inspecao, filtro: FiltroInspecao): boolean {
  switch (filtro) {
    case 'andamento': return i.status === StatusInspecao.EmAndamento;
    case 'assinatura': return i.status === StatusInspecao.AguardandoAssinatura;
    case 'nc': return i.itensNaoConformes > 0;
    case 'concluidas': return i.status === StatusInspecao.Concluida;
    default: return true;
  }
}

function tomDaLinha(i: Inspecao): Tom {
  if (i.itensNaoConformes > 0) return 'alerta';
  if (i.status === StatusInspecao.Concluida) return 'ok';
  if (estaParada(i)) return 'atencao';
  return 'info';
}

// Ícone do tipo num quadrado marsala claro, à esquerda do nome do checklist.
function IconeTipo({ tipo }: { tipo: number }) {
  const icone = tipo === TipoInspecao.Alojamento ? <Home20Regular /> : tipo === TipoInspecao.Veiculo ? <VehicleTruckProfile20Regular /> : <ClipboardTaskListLtr20Regular />;
  return (
    <span
      aria-hidden="true"
      style={{
        flexShrink: 0, width: 34, height: 34, borderRadius: 10, display: 'grid', placeItems: 'center',
        color: 'var(--sst-acao-marca-tinta)', backgroundColor: 'var(--sst-acao-marca-wash)',
      }}
    >
      {icone}
    </span>
  );
}

// Progresso em dois trechos: respondidos conformes (verde) e não conformes (vermelho).
function BarraRespostas({ respondidos, naoConformes, total }: { respondidos: number; naoConformes: number; total: number }) {
  const conformes = total ? Math.max(0, respondidos - naoConformes) / total : 0;
  const nc = total ? naoConformes / total : 0;
  return (
    <div
      role="progressbar"
      aria-valuemin={0}
      aria-valuemax={total}
      aria-valuenow={respondidos}
      aria-label={`${respondidos} de ${total} itens respondidos`}
      style={{ display: 'flex', width: 120, height: 5, borderRadius: 5, overflow: 'hidden', backgroundColor: tokensUi.status.neutro.fundo }}
    >
      <span style={{ width: `${conformes * 100}%`, backgroundColor: tokensUi.status.ok.tinta }} />
      <span style={{ width: `${nc * 100}%`, backgroundColor: tokensUi.status.alerta.tinta }} />
    </div>
  );
}

// Onda D do spec de formulário inline (2026-09-11): o PainelLateral (drawer) saiu — mesmo padrão
// da Onda A/AtividadesTab.tsx. Agora é um PainelCriacaoInline, que cresce acima da lista só até a
// altura do próprio formulário, em vez de cobrir a tela com uma gaveta. O botão "Ver inspeção" que
// só repetia a navegação da linha (guia item 1) sai — a linha inteira já navega via aoClicarLinha.
export function InspecoesTab({ aoCarregar }: { aoCarregar?: (inspecoes: InspecaoCampo[]) => void } = {}) {
  const navigate = useNavigate();
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const [inspecoes, setInspecoes] = useState<InspecaoCampo[]>([]);
  // Já assinados não podem ser excluídos (o servidor recusa): o botão some deles.
  const travados = useRegistrosTravados('Inspecao', inspecoes.map((i) => i.id));
  const [obras, setObras] = useState<Obra[]>([]);
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [checklists, setChecklists] = useState<ChecklistModelo[]>([]);
  const [usuarios, setUsuarios] = useState<Usuario[]>([]);
  const [novaInspecao, setNovaInspecao] = useState<NovaInspecao>(() => criarInspecaoVazia());
  const [erro, setErro] = useState<string | null>(null);
  const [excluindoId, setExcluindoId] = useState<string | null>(null);
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
  const [erroPainel, setErroPainel] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [listaCarregada, setListaCarregada] = useState(false);
  const [apoioIndisponivel, setApoioIndisponivel] = useState(false);
  const [painelAberto, setPainelAberto] = useState(false);
  const [preparando, setPreparando] = useState(false);
  const [modelosPreparados, setModelosPreparados] = useState(0);
  const [totalModelos, setTotalModelos] = useState(0);

  async function carregar() {
    try {
      setErro(null);
      setPreparando(navigator.onLine);
      const [lista, preparada] = await Promise.allSettled([
        api.inspecoes.listar(), navigator.onLine ? prepararInspecoesOffline().then(r => r.preparacao) : obterPreparacaoInspecoes(),
      ]);
      const mescladas = await mesclarInspecoesLocais(lista.status === 'fulfilled' ? lista.value : []);
      if (lista.status === 'rejected' && !mescladas.length) throw lista.reason;
      setInspecoes(mescladas);
      setListaCarregada(true);
      const preparo = preparada.status === 'fulfilled' ? preparada.value : await obterPreparacaoInspecoes();
      setApoioIndisponivel(!preparo || preparada.status === 'rejected');
      if (preparo) {
        setObras(preparo.obras); setAtividades(preparo.atividades); setUsuarios(preparo.usuarios);
        setChecklists(Object.values(preparo.modelos).map(m => m.checklistModelo));
        setModelosPreparados(Object.keys(preparo.modelos).length);
        setTotalModelos(preparo.totalModelos ?? Object.keys(preparo.modelos).length);
      }
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar inspeções.');
    } finally {
      setCarregandoLista(false);
      setPreparando(false);
    }
  }

  useEffect(() => {
    void carregar();
    return assinarMudancasSync(() => {
      void api.inspecoes.listar().then(mesclarInspecoesLocais).then(setInspecoes).catch(() => undefined);
    });
  }, []);

  // Os cards de Alojamento e Veículos (CartoesInspecoes) mostram a situação a partir desta mesma lista.
  useEffect(() => {
    aoCarregar?.(inspecoes);
  }, [inspecoes, aoCarregar]);

  const [filtro, setFiltro] = useState<FiltroInspecao>('todas');
  const [busca, setBusca] = useState('');
  const contagem = useMemo(() => {
    const c: Record<FiltroInspecao, number> = { todas: inspecoes.length, andamento: 0, assinatura: 0, nc: 0, concluidas: 0 };
    for (const i of inspecoes) {
      for (const f of ['andamento', 'assinatura', 'nc', 'concluidas'] as const) if (passaNoFiltro(i, f)) c[f] += 1;
    }
    return c;
  }, [inspecoes]);
  const inspecoesVisiveis = useMemo(() => {
    const termo = busca.trim().toLocaleLowerCase('pt-BR');
    return inspecoes.filter((i) => {
      if (!passaNoFiltro(i, filtro)) return false;
      if (!termo) return true;
      return [i.obraNome, i.checklistModeloNome, i.responsavelUsuarioNome, tipoInspecaoLabel[i.tipoInspecao], i.veiculoPlacaPrefixo, i.itemInspecionavelIdentificacao, i.atividadeNome]
        .some((v) => v?.toLocaleLowerCase('pt-BR').includes(termo));
    });
  }, [inspecoes, filtro, busca]);

  function abrirPdf(i: InspecaoCampo) {
    void visualizar({ titulo: 'PDF da inspeção', nomeArquivo: `inspecao-${i.id}.pdf`, obter: () => api.inspecoes.baixarPdf(i.id) });
  }

  const atividadesDaObra = atividades.filter((a) => a.obraId === novaInspecao.obraId);

  function fecharPainel() {
    setPainelAberto(false);
    setErroPainel(null);
    setNovaInspecao(criarInspecaoVazia());
  }

  async function criar() {
    if (
      !conferirPendencias([
        { nome: 'Checklist', dica: 'Escolha o modelo de checklist', ok: !!novaInspecao.checklistModeloId, alvo: 'insp-campo-checklist' },
        { nome: 'Obra', dica: 'Selecione a obra da inspeção', ok: !!novaInspecao.obraId, alvo: 'insp-campo-obra' },
        { nome: 'Data', dica: 'Informe a data da inspeção', ok: !!novaInspecao.data, alvo: 'insp-campo-data' },
        { nome: 'Responsável', dica: 'Quem acompanha a inspeção', ok: !!novaInspecao.responsavelUsuarioId, alvo: 'insp-campo-responsavel' },
      ])
    ) {
      return;
    }
    try {
      setCarregando(true);
      setErroPainel(null);
      const criada = await criarInspecaoCampo({
        ...novaInspecao,
        atividadeId: novaInspecao.atividadeId || null,
      });
      fecharPainel();
      navigate(`/prevencao/inspecoes/${criada.id}`);
    } catch (e) {
      setErroPainel(e instanceof Error ? e.message : 'Falha ao criar inspeção.');
    } finally {
      setCarregando(false);
    }
  }

  // A versão do checklist ("v1") sai da lista e continua no detalhe da inspeção.
  const colunas: Coluna<InspecaoCampo>[] = [
    {
      chave: 'checklist',
      rotulo: 'Inspeção',
      render: (i) => {
        const alvo = i.veiculoPlacaPrefixo ?? i.itemInspecionavelIdentificacao ?? i.atividadeNome;
        const detalhe = [i.temaInspecaoNome ?? tipoInspecaoLabel[i.tipoInspecao], alvo].filter(Boolean).join(' · ');
        return <CelulaDupla inicio={<IconeTipo tipo={i.tipoInspecao} />} titulo={i.checklistModeloNome} detalhe={detalhe} />;
      },
    },
    { chave: 'obraNome', rotulo: 'Obra', render: (i) => <CelulaDupla tituloLeve titulo={i.obraNome} /> },
    {
      chave: 'data',
      rotulo: 'Data',
      render: (i) => {
        if (i.status === StatusInspecao.Concluida) return dataBr(i.data);
        const dias = diasDesde(i.data);
        const texto = dias === 0 ? 'hoje' : `há ${dias} ${dias === 1 ? 'dia' : 'dias'}`;
        return (
          <CelulaDupla
            tituloLeve
            titulo={dataBr(i.data)}
            detalhe={estaParada(i) ? <span style={{ color: tokensUi.status.atencao.tinta, fontWeight: 600 }}>{texto}</span> : texto}
          />
        );
      },
    },
    {
      chave: 'responsavelUsuarioNome',
      rotulo: 'Responsável',
      render: (i) => (
        <CelulaDupla
          tituloLeve
          inicio={<FotoTrabalhador nome={i.responsavelUsuarioNome} trabalhadorId={i.responsavelTrabalhadorId ?? ''} temFoto={i.responsavelTemFoto} tamanho={36} />}
          titulo={i.responsavelUsuarioNome}
        />
      ),
    },
    {
      chave: 'progresso',
      rotulo: 'Progresso',
      render: (i) => {
        const aviso = i.totalItens === 0
          ? 'Checklist sem itens'
          : i.status === StatusInspecao.EmAndamento && i.itensRespondidos === i.totalItens
            ? 'Tudo respondido, falta concluir'
            : null;
        return (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 5 }}>
            <span style={{ fontSize: 12, fontVariantNumeric: 'tabular-nums' }}>
              <strong>{i.itensRespondidos}</strong> de {i.totalItens}
              {i.itensNaoConformes > 0 && (
                <strong style={{ color: tokensUi.status.alerta.tinta }}> · {i.itensNaoConformes} NC</strong>
              )}
            </span>
            {i.totalItens > 0 && <BarraRespostas respondidos={i.itensRespondidos} naoConformes={i.itensNaoConformes} total={i.totalItens} />}
            {aviso && <span style={{ fontSize: 11.5, fontWeight: 600, color: tokensUi.status.atencao.tinta }}>{aviso}</span>}
          </div>
        );
      },
    },
    {
      chave: 'status',
      rotulo: 'Status',
      render: (i) => <StatusChip tom={tomPorStatusInspecao[i.status] ?? 'neutro'}>{i.criacaoPendente ? 'Criação pendente' : statusInspecaoLabel[i.status]}</StatusChip>,
    },
  ];

  // Exclusão definitiva, só para Administrador. O servidor recusa quando a inspeção já gerou não
  // conformidade — nesse caso a mensagem dele diz quantas são, e é ela que aparece na tela.
  async function excluir(inspecao: Inspecao) {
    const confirmado = await confirmar({
      titulo: 'Excluir inspeção',
      mensagem:
        'Excluir esta inspeção e todas as respostas do checklist dela?' +
        ' Esta ação não pode ser desfeita.',
      rotuloConfirmar: 'Excluir',
    });
    if (!confirmado) return;
    try {
      setExcluindoId(inspecao.id);
      setErro(null);
      await api.inspecoes.excluir(inspecao.id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao excluir a inspeção.');
    } finally {
      setExcluindoId(null);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {dialogElement}
      {dialogoVisualizador}
      <PageHeader
        titulo="Inspeções"
        acoes={
          <Button
            appearance="primary"
            icon={<Add24Regular />}
            onClick={() => (painelAberto ? fecharPainel() : setPainelAberto(true))}
            aria-expanded={painelAberto}
            aria-controls="painel-nova-inspecao"
          >
            {painelAberto ? 'Fechar' : 'Nova inspeção'}
          </Button>
        }
      />

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      {/* Tudo preparado: um selo pequeno basta (o texto completo fica no title). Faltando modelo, o
          aviso continua grande, porque aí há o que fazer. */}
      {!preparando && modelosPreparados < totalModelos ? (
        <FeedbackInline tom="aviso">
          {`${modelosPreparados} de ${totalModelos} modelo(s) preparado(s) neste dispositivo para criar inspeções sem internet.`}
          {' Alguns modelos não puderam ser baixados. Reabra esta tela com internet para tentar novamente.'}
        </FeedbackInline>
      ) : (
        <div>
          <span
            title={preparando ? undefined : `${modelosPreparados} de ${totalModelos} modelo(s) preparado(s) neste dispositivo para criar inspeções sem internet.`}
            style={{
              display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: 12, padding: '4px 10px', borderRadius: 999,
              color: preparando ? tokensUi.status.info.tinta : tokensUi.status.ok.tinta,
              backgroundColor: preparando ? tokensUi.status.info.fundo : tokensUi.status.ok.fundo,
            }}
          >
            {!preparando && <CheckmarkCircle16Regular aria-hidden="true" />}
            {preparando ? 'Preparando para uso sem internet…' : 'Pronto para usar sem internet'}
          </span>
        </div>
      )}

      <div id="painel-nova-inspecao">
        <PainelCriacaoInline aberto={painelAberto} titulo="Nova inspeção">
          {apoioIndisponivel && (
            <FeedbackInline tom="aviso">Algumas opções do formulário não estão disponíveis neste dispositivo. Conecte-se à internet e reabra esta tela para carregá-las.</FeedbackInline>
          )}
          <FormSection titulo="Dados da inspeção" numero={1} primeira>
            {erroPainel && (
              <FeedbackInline tom="erro" aoFechar={() => setErroPainel(null)}>
                {erroPainel}
              </FeedbackInline>
            )}
            <FormGrid>
              <Campo span={12} id="insp-campo-checklist">
                <Field label="Checklist">
                  <Select
                    value={novaInspecao.checklistModeloId}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, checklistModeloId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {checklists.filter((c) => c.tipoInspecao !== TipoInspecao.Veiculo && c.tipoInspecao !== TipoInspecao.Alojamento && c.tipoInspecao !== TipoInspecao.Tema).map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.nome} (v{c.versao} — {tipoInspecaoLabel[c.tipoInspecao]})
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6} id="insp-campo-obra">
                <Field label="Obra">
                  <Select
                    value={novaInspecao.obraId}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, obraId: d.value, atividadeId: null })}
                  >
                    <option value="">Selecione</option>
                    {obras.map((obra) => (
                      <option key={obra.id} value={obra.id}>
                        {obra.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6}>
                <Field label="Atividade (opcional)">
                  <Select
                    value={novaInspecao.atividadeId ?? ''}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, atividadeId: d.value || null })}
                    disabled={!novaInspecao.obraId}
                  >
                    <option value="">Nenhuma</option>
                    {atividadesDaObra.map((atividade) => (
                      <option key={atividade.id} value={atividade.id}>
                        {atividade.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
              <Campo span={6} id="insp-campo-data">
                <Field label="Data">
                  <CampoData
                    value={novaInspecao.data}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, data: d.value })}
                  />
                </Field>
              </Campo>
              <Campo span={6} id="insp-campo-responsavel">
                <Field label="Responsável">
                  <Select
                    value={novaInspecao.responsavelUsuarioId}
                    onChange={(_, d) => setNovaInspecao({ ...novaInspecao, responsavelUsuarioId: d.value })}
                  >
                    <option value="">Selecione</option>
                    {usuarios.map((usuario) => (
                      <option key={usuario.id} value={usuario.id}>
                        {usuario.nome}
                      </option>
                    ))}
                  </Select>
                </Field>
              </Campo>
            </FormGrid>
            <FormRodape>
              <Button onClick={fecharPainel}>Cancelar</Button>
              <Button appearance="primary" onClick={criar} disabled={carregando || preparando}>
                Iniciar inspeção
              </Button>
            </FormRodape>
          </FormSection>
        </PainelCriacaoInline>
      </div>

      <Card>
        {inspecoes.length > 0 && (
          <div style={{ display: 'flex', flexWrap: 'wrap', gap: 10, alignItems: 'center', justifyContent: 'space-between', marginBottom: 12 }}>
            <FiltroRapido<FiltroInspecao>
              aria-label="Filtrar inspeções pela situação"
              valor={filtro}
              aoMudar={setFiltro}
              opcoes={[
                { valor: 'todas', rotulo: 'Todas', contagem: contagem.todas },
                { valor: 'andamento', rotulo: 'Em andamento', contagem: contagem.andamento },
                { valor: 'assinatura', rotulo: 'Aguardando assinatura', contagem: contagem.assinatura },
                { valor: 'nc', rotulo: 'Com não conformidade', contagem: contagem.nc },
                { valor: 'concluidas', rotulo: 'Concluídas', contagem: contagem.concluidas },
              ]}
            />
            <Input
              aria-label="Buscar obra, checklist ou responsável"
              placeholder="Buscar obra, checklist ou responsável"
              contentBefore={<Search20Regular />}
              value={busca}
              onChange={(_, d) => setBusca(d.value)}
              style={{ minWidth: 260 }}
            />
          </div>
        )}
        <DataTable
          aria-label="Inspeções"
          colunas={colunas}
          linhas={inspecoesVisiveis}
          chaveLinha={(i) => i.id}
          carregando={carregandoLista}
          tomLinha={tomDaLinha}
          // 18/11/2026: inspeção concluída abre o PDF; em andamento, aguardando assinatura, ainda não
          // enviada (offline) ou sem internet continua indo para o detalhe, que também fica no "Abrir".
          aoClicarLinha={(i) =>
            i.status === StatusInspecao.Concluida && !i.criacaoPendente && navigator.onLine
              ? abrirPdf(i)
              : navigate(`/prevencao/inspecoes/${i.id}`)
          }
          // Ação principal conforme a situação (25/11/2026): em andamento → Continuar (é o próprio
          // detalhe, então o "Abrir" não se repete); aguardando assinatura → Assinar; concluída → PDF.
          acoesLinha={(i) => (
            <>
              {i.status === StatusInspecao.EmAndamento || i.criacaoPendente ? (
                <BotaoAcao
                  tom="ver"
                  icon={<Play24Regular />}
                  aria-label="Continuar a inspeção"
                  onClick={(evento) => {
                    evento.stopPropagation();
                    navigate(`/prevencao/inspecoes/${i.id}`);
                  }}
                >
                  Continuar
                </BotaoAcao>
              ) : (
                <>
                  {i.status === StatusInspecao.AguardandoAssinatura && (
                    <BotaoAcao
                      tom="assinar"
                      aria-label="Assinar inspeção"
                      onClick={(evento) => {
                        evento.stopPropagation();
                        navigate(`/prevencao/inspecoes/${i.id}/assinar`);
                      }}
                    >
                      Assinar
                    </BotaoAcao>
                  )}
                  {i.status === StatusInspecao.Concluida && navigator.onLine && (
                    <BotaoAcao
                      tom="baixar"
                      icon={<DocumentPdf24Regular />}
                      aria-label="Abrir o PDF da inspeção"
                      onClick={(evento) => {
                        evento.stopPropagation();
                        abrirPdf(i);
                      }}
                    >
                      PDF
                    </BotaoAcao>
                  )}
                  <BotaoAcao
                    tom="ver"
                    icon={<Open24Regular />}
                    aria-label="Abrir detalhe da inspeção"
                    title="Abrir detalhe"
                    onClick={(evento) => {
                      evento.stopPropagation();
                      navigate(`/prevencao/inspecoes/${i.id}`);
                    }}
                  />
                </>
              )}
              {/* Exclusão é privilégio de Administrador (o servidor recusa os demais) — ver PoliticasAutorizacao. */}
              {souAdministrador && !travados.has(i.id) && (
                <BotaoAcao
                  tom="excluir"
                  icon={<Delete24Regular />}
                  disabled={excluindoId === i.id || i.criacaoPendente}
                  onClick={(evento) => {
                    evento.stopPropagation();
                    excluir(i);
                  }}
                  aria-label="Excluir inspeção"
                  title="Excluir esta inspeção (somente administrador)"
                />
              )}
            </>
          )}
          vazio={inspecoes.length > 0 ? {
            titulo: 'Nenhuma inspeção nesta seleção',
            descricao: 'Troque o filtro ou a busca para ver outras inspeções.',
          } : listaCarregada ? {
            titulo: 'Nenhuma inspeção iniciada ainda.',
            descricao: 'Inicie a primeira inspeção para começar.',
            acao: { rotulo: 'Nova inspeção', aoClicar: () => setPainelAberto(true) },
          } : {
            titulo: 'Lista indisponível neste dispositivo.',
            descricao: 'Conecte-se à internet e carregue as inspeções antes de usá-las offline.',
            acao: { rotulo: 'Tentar novamente', aoClicar: () => void carregar() },
          }}
        />
      </Card>
    </div>
  );
}
