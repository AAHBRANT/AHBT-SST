import { useMemo, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  Avatar,
  Button,
  Carregando,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  EstadoVazio,
  FeedbackInline,
  Field,
  Select,
  SeletorPesquisavel,
  StatusChip,
  useConfirmar,
  useTipografia,
  type OpcaoSeletor,
  type Tom,
} from '@ui';
import { Checkmark16Regular, Person16Regular, Warning16Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  api,
  SituacaoOcorrenciaEpi,
  situacaoOcorrenciaEpiLabel,
  type FiltroOcorrenciasEpi,
  type OcorrenciaEpi,
} from '../../lib/api';
import { useCameras, useObras, useOcorrenciasEpi, useTrabalhadores } from '../../lib/consultas';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { lerLembrada } from '../../hooks/useObraLembrada';
import { rotuloAlertaTelegram } from './aoVivoRegras';
import { descreverIrregularidade, formatarDataHora, resumoCena } from './rotulos';
import { FotoOcorrenciaEpi } from './FotoOcorrenciaEpi';
import { mergeClasses, useEstilosAlertas } from './estilosAlertas';

const LIMITE_API = 200;
const FUSO_BRASILIA = 'America/Sao_Paulo';

type Periodo = 'hoje' | '7dias' | '30dias';

const TOM_SITUACAO: Record<number, Tom> = { 1: 'info', 2: 'ok', 3: 'neutro' };

// "Hoje" = desde a meia-noite em Brasília (UTC-3 fixo: o Brasil não tem horário de verão desde 2019).
function inicioDoPeriodo(periodo: Periodo): string {
  if (periodo === 'hoje') {
    const dia = new Date().toLocaleDateString('en-CA', { timeZone: FUSO_BRASILIA });
    return new Date(`${dia}T00:00:00-03:00`).toISOString();
  }
  const dias = periodo === '7dias' ? 7 : 30;
  return new Date(Date.now() - dias * 24 * 60 * 60 * 1000).toISOString();
}

const plural = (n: number, singular: string, pluralTexto: string) => `${n} ${n === 1 ? singular : pluralTexto}`;

interface DialogoVincularProps {
  ocorrencia: OcorrenciaEpi;
  erro: string | null;
  aoFechar: () => void;
  aoConfirmar: (ocorrencia: OcorrenciaEpi, trabalhadorId: string) => Promise<void>;
}

function DialogoVincular({ ocorrencia, erro, aoFechar, aoConfirmar }: DialogoVincularProps) {
  const estilos = useEstilosAlertas();
  const consulta = useTrabalhadores();
  const trabalhadores = consulta.lista;
  const [trabalhadorId, setTrabalhadorId] = useState('');
  const [salvando, setSalvando] = useState(false);
  const obraId = ocorrencia.obraId;

  // Os trabalhadores da obra da ocorrência vêm primeiro; os demais seguem disponíveis (decisão de
  // produto pendente sobre restringir à obra).
  const opcoes = useMemo<OpcaoSeletor[]>(() => {
    const ordenados = [...trabalhadores].sort((a, b) => {
      const da = a.obraId === obraId ? 0 : 1;
      const db = b.obraId === obraId ? 0 : 1;
      return da - db || a.nome.localeCompare(b.nome, 'pt-BR');
    });
    return ordenados.map((t) => ({ id: t.id, rotulo: t.nome, descricao: t.matricula ?? undefined }));
  }, [trabalhadores, obraId]);

  async function confirmar() {
    if (!trabalhadorId || salvando) return;
    setSalvando(true);
    try {
      await aoConfirmar(ocorrencia, trabalhadorId);
    } finally {
      setSalvando(false);
    }
  }

  return (
    <Dialog
      open
      onOpenChange={(_, d) => {
        if (!d.open && !salvando) aoFechar();
      }}
    >
      <DialogSurface>
        <DialogBody>
          <DialogTitle>{ocorrencia.trabalhadorId ? 'Trocar trabalhador' : 'Vincular trabalhador'}</DialogTitle>
          <DialogContent className={estilos.dialogoCorpo}>
            {erro && <FeedbackInline tom="erro">{erro}</FeedbackInline>}
            {consulta.isError && (
              <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void consulta.refetch() }}>
                Não foi possível carregar os trabalhadores.
              </FeedbackInline>
            )}
            <Field label="Trabalhador da ocorrência" required>
              <SeletorPesquisavel
                placeholder={`Buscar entre ${trabalhadores.length} trabalhadores`}
                opcoes={opcoes}
                valor={trabalhadorId}
                aoMudar={setTrabalhadorId}
              />
            </Field>
          </DialogContent>
          <DialogActions>
            <Button
              className={estilos.botao}
              disabled={salvando}
              onClick={aoFechar}
            >
              Cancelar
            </Button>
            <Button
              appearance="primary"
              className={estilos.botao}
              disabled={!trabalhadorId || salvando}
              onClick={() => void confirmar()}
            >
              {salvando ? 'Salvando...' : 'Vincular'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}

// Aba "Alertas de EPI": ocorrências de capacete/colete registradas pelas câmeras, com conferência
// (vínculo do trabalhador e marcação de falso alerta).
export function AlertasEpiTab() {
  const estilos = useEstilosAlertas();
  const tipo = useTipografia();
  const queryClient = useQueryClient();
  const sucessoToast = useSucessoToast();
  const { confirmar, dialogElement } = useConfirmar();
  const { lista: obras } = useObras();

  const [obraEscolhida, setObraEscolhida] = useState<string | null>(null);
  const [cameraEscolhida, setCameraEscolhida] = useState('');
  const [periodo, setPeriodo] = useState<Periodo>('7dias');
  const [situacao, setSituacao] = useState(0);
  const [erro, setErro] = useState<string | null>(null);
  const [vinculando, setVinculando] = useState<OcorrenciaEpi | null>(null);
  const [ocupada, setOcupada] = useState<string | null>(null);

  // Enquanto o usuário não escolheu, vale a obra lembrada (se ainda existir) ou "Todas as obras".
  const lembrada = lerLembrada();
  const obraId = obraEscolhida ?? (obras.some((o) => o.id === lembrada) ? lembrada : '');

  const cameras = useCameras(obraId || undefined);
  const listaCameras = cameras.data;
  const cameraId = (listaCameras ?? []).some((c) => c.id === cameraEscolhida) ? cameraEscolhida : '';

  // Memoizado por período: um `de` novo a cada render mudaria a chave da consulta sem parar.
  const de = useMemo(() => inicioDoPeriodo(periodo), [periodo]);
  const filtro = useMemo<FiltroOcorrenciasEpi>(
    () => ({
      obraId: obraId || undefined,
      cameraId: cameraId || undefined,
      de,
      situacao: situacao || undefined,
    }),
    [obraId, cameraId, de, situacao],
  );
  const ocorrencias = useOcorrenciasEpi(filtro);
  const itens = ocorrencias.data;
  const lista = useMemo(() => itens ?? [], [itens]);
  const novas = lista.filter((o) => o.situacao === SituacaoOcorrenciaEpi.Nova).length;

  async function executar(ocorrencia: OcorrenciaEpi, acao: () => Promise<void>, mensagem: string): Promise<boolean> {
    setErro(null);
    setOcupada(ocorrencia.id);
    try {
      await acao();
      await queryClient.invalidateQueries({ queryKey: ['cameras', 'ocorrencias'] });
      sucessoToast(mensagem);
      return true;
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível concluir a ação.');
      return false;
    } finally {
      setOcupada(null);
    }
  }

  async function vincular(ocorrencia: OcorrenciaEpi, trabalhadorId: string) {
    const ok = await executar(
      ocorrencia,
      () => api.cameras.vincularTrabalhador(ocorrencia.id, trabalhadorId),
      'Trabalhador vinculado com sucesso.',
    );
    if (ok) setVinculando(null);
  }

  function abrirVinculo(ocorrencia: OcorrenciaEpi | null) {
    setErro(null);
    setVinculando(ocorrencia);
  }

  async function marcarFalsoAlerta(ocorrencia: OcorrenciaEpi) {
    const ok = await confirmar({
      mensagem: 'Marcar como falso alerta? Use quando a IA errou — a pessoa estava com o EPI.',
      tom: 'neutro',
    });
    if (!ok) return;
    await executar(
      ocorrencia,
      () => api.cameras.definirSituacao(ocorrencia.id, SituacaoOcorrenciaEpi.FalsoAlerta),
      'Ocorrência marcada como falso alerta.',
    );
  }

  async function desfazerFalsoAlerta(ocorrencia: OcorrenciaEpi) {
    const nova = ocorrencia.trabalhadorId ? SituacaoOcorrenciaEpi.Conferida : SituacaoOcorrenciaEpi.Nova;
    await executar(ocorrencia, () => api.cameras.definirSituacao(ocorrencia.id, nova), 'Falso alerta desfeito.');
  }

  // Câmeras sem aviso no Telegram (do cadastro já carregado): a ocorrência delas não é "alerta não enviado".
  const semAviso = new Set((listaCameras ?? []).filter((c) => c.avisarTelegram === false).map((c) => c.id));

  function cartao(o: OcorrenciaEpi) {
    const irregularidade = descreverIrregularidade(o.semCapacete, o.semColete);
    const rotuloTelegram = rotuloAlertaTelegram(o.alertaEnviado, semAviso.has(o.cameraId) ? false : undefined);
    const titulo = `${o.cameraNome} · ${formatarDataHora(o.capturadaEmUtc)}`;
    const falso = o.situacao === SituacaoOcorrenciaEpi.FalsoAlerta;
    const bloqueada = ocupada !== null;
    // Apontado pelo reconhecimento facial e ainda sem conferência: o técnico confirma ou troca.
    const porRosto = !!o.trabalhadorId && o.identificadoPorRosto;
    const nomeTrabalhador = o.trabalhadorNome ?? 'Trabalhador vinculado';
    const confiancaRosto =
      o.confiancaIdentificacao != null ? ` · ${Math.round(o.confiancaIdentificacao)}%` : '';
    return (
      <article key={o.id} className={estilos.cartao} aria-label={`Ocorrência de ${titulo}`}>
        <div className={estilos.areaFoto}>
          <FotoOcorrenciaEpi id={o.id} alt={`Foto da ocorrência: ${irregularidade}, ${titulo}`} />
          <div className={estilos.chipsFoto}>
            <StatusChip tom="alerta" icone={<Warning16Regular />}>
              {irregularidade}
            </StatusChip>
            <StatusChip tom={TOM_SITUACAO[o.situacao] ?? 'neutro'}>
              {situacaoOcorrenciaEpiLabel[o.situacao] ?? 'Situação desconhecida'}
            </StatusChip>
          </div>
        </div>
        <div className={estilos.corpo}>
          <div className={estilos.textos}>
            <div className={tipo.subtitulo}>{titulo}</div>
            <div className={mergeClasses(tipo.corpo, estilos.apoio)}>
              {resumoCena(o.pessoasNaCena, o.semCapacete, o.semColete)}
            </div>
          </div>
          <div
            className={mergeClasses(
              tipo.legenda,
              estilos.telegram,
              rotuloTelegram.tom === 'ok' && estilos.telegramOk,
              rotuloTelegram.tom === 'aviso' && estilos.telegramAviso,
              rotuloTelegram.tom === 'neutro' && estilos.telegramNeutro,
            )}
          >
            {rotuloTelegram.tom === 'ok' && <Checkmark16Regular aria-hidden="true" />}
            {rotuloTelegram.tom === 'aviso' && <Warning16Regular aria-hidden="true" />}
            {rotuloTelegram.texto}
          </div>
          {porRosto && (
            <>
              <div className={mergeClasses(estilos.faixaTrabalhador, estilos.faixaComAvatar, estilos.faixaRosto)}>
                <Avatar name={nomeTrabalhador} color="brand" size={36} aria-hidden="true" />
                <div className={estilos.faixaTextos}>
                  <span className={tipo.corpo}>
                    <strong>{nomeTrabalhador}</strong>
                  </span>
                  <StatusChip tom="ok" icone={<Person16Regular />}>
                    Identificado pelo rosto{confiancaRosto}
                  </StatusChip>
                </div>
              </div>
              <div className={mergeClasses(tipo.legenda, estilos.apoio)}>Confira a foto antes de confirmar.</div>
            </>
          )}
          {o.trabalhadorId && !porRosto && (
            <div className={estilos.faixaTrabalhador}>
              <span className={tipo.corpo}>
                <strong>{nomeTrabalhador}</strong>
              </span>
              <span className={mergeClasses(tipo.legenda, estilos.apoio)}>Trabalhador vinculado</span>
            </div>
          )}
          {!o.trabalhadorId && !falso && (
            <div className={mergeClasses(estilos.faixaTrabalhador, estilos.faixaComAvatar)}>
              <Avatar size={36} aria-hidden="true" />
              <div className={estilos.faixaTextos}>
                <span className={tipo.corpo}>
                  <strong>Trabalhador não identificado</strong>
                </span>
                <span className={mergeClasses(tipo.legenda, estilos.apoio)}>Vincule quem aparece na foto.</span>
              </div>
            </div>
          )}
          <div className={estilos.acoes}>
            {porRosto ? (
              <>
                <Button
                  appearance="primary"
                  className={estilos.botao}
                  disabled={bloqueada}
                  onClick={() =>
                    void executar(
                      o,
                      // Regravar o mesmo trabalhador transforma o vínculo em manual e marca como conferida.
                      () => api.cameras.vincularTrabalhador(o.id, o.trabalhadorId),
                      'Trabalhador confirmado com sucesso.',
                    )
                  }
                >
                  Confirmar trabalhador
                </Button>
                <Button className={estilos.botao} disabled={bloqueada} onClick={() => abrirVinculo(o)}>
                  Trocar
                </Button>
              </>
            ) : !o.trabalhadorId ? (
              <Button
                appearance="primary"
                className={estilos.botao}
                disabled={bloqueada}
                onClick={() => abrirVinculo(o)}
              >
                Vincular trabalhador
              </Button>
            ) : (
              <>
                <Button className={estilos.botao} disabled={bloqueada} onClick={() => abrirVinculo(o)}>
                  Trocar trabalhador
                </Button>
                <Button
                  className={estilos.botao}
                  disabled={bloqueada}
                  onClick={() =>
                    void executar(o, () => api.cameras.vincularTrabalhador(o.id, null), 'Vínculo removido com sucesso.')
                  }
                >
                  Remover vínculo
                </Button>
              </>
            )}
            {falso ? (
              <Button className={estilos.botao} disabled={bloqueada} onClick={() => void desfazerFalsoAlerta(o)}>
                Desfazer falso alerta
              </Button>
            ) : (
              <Button className={estilos.botao} disabled={bloqueada} onClick={() => void marcarFalsoAlerta(o)}>
                Falso alerta
              </Button>
            )}
          </div>
        </div>
      </article>
    );
  }

  return (
    <div className={estilos.raiz}>
      {dialogElement}
      {vinculando && (
        <DialogoVincular
          key={vinculando.id}
          ocorrencia={vinculando}
          erro={erro}
          aoFechar={() => abrirVinculo(null)}
          aoConfirmar={vincular}
        />
      )}

      <div className={estilos.filtros}>
        <div className={estilos.campoFiltro}>
          <Field label="Obra">
            <Select
              value={obraId}
              onChange={(_, d) => {
                setObraEscolhida(d.value);
                setCameraEscolhida('');
              }}
            >
              <option value="">Todas as obras</option>
              {obras.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.nome}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className={estilos.campoFiltro}>
          <Field label="Câmera">
            <Select value={cameraId} onChange={(_, d) => setCameraEscolhida(d.value)}>
              <option value="">Todas</option>
              {(listaCameras ?? []).map((c) => (
                <option key={c.id} value={c.id}>
                  {c.nome}
                </option>
              ))}
            </Select>
          </Field>
        </div>
        <div className={estilos.campoFiltro}>
          <Field label="Período">
            <Select value={periodo} onChange={(_, d) => setPeriodo(d.value as Periodo)}>
              <option value="hoje">Hoje</option>
              <option value="7dias">Últimos 7 dias</option>
              <option value="30dias">Últimos 30 dias</option>
            </Select>
          </Field>
        </div>
        <div className={estilos.campoFiltro}>
          <Field label="Situação">
            <Select value={String(situacao)} onChange={(_, d) => setSituacao(Number(d.value))}>
              <option value="0">Todas</option>
              <option value={SituacaoOcorrenciaEpi.Nova}>Novas</option>
              <option value={SituacaoOcorrenciaEpi.Conferida}>Conferidas</option>
              <option value={SituacaoOcorrenciaEpi.FalsoAlerta}>Falso alerta</option>
            </Select>
          </Field>
        </div>
        {itens && (
          <div className={mergeClasses(tipo.corpo, estilos.resumo)} role="status">
            {plural(lista.length, 'ocorrência', 'ocorrências')} · {plural(novas, 'nova', 'novas')}
          </div>
        )}
      </div>

      {itens && lista.length >= LIMITE_API && (
        <FeedbackInline tom="info">Mostrando as 200 mais recentes; refine o período.</FeedbackInline>
      )}

      {erro && !vinculando && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      {ocorrencias.isLoading && <Carregando variante="card" linhas={3} />}

      {ocorrencias.isError && (
        <FeedbackInline tom="erro" acao={{ rotulo: 'Tentar de novo', aoClicar: () => void ocorrencias.refetch() }}>
          {ocorrencias.error instanceof Error ? ocorrencias.error.message : 'Não foi possível carregar as ocorrências.'}
        </FeedbackInline>
      )}

      {itens && lista.length === 0 && (
        <EstadoVazio
          icone={<Warning24Regular />}
          titulo="Nenhuma ocorrência no período"
          descricao="Quando uma câmera registrar alguém sem capacete ou colete, o alerta aparece aqui."
        />
      )}

      {lista.length > 0 && <div className={estilos.grade}>{lista.map(cartao)}</div>}

      <div className={mergeClasses(tipo.legenda, estilos.rodape)}>
        As fotos mostram trabalhadores. O acesso segue o escopo por obra.
      </div>
    </div>
  );
}
