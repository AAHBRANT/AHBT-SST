import { useEffect, useMemo, useRef, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useQueryClient } from '@tanstack/react-query';
import {
  BotaoAcao,
  Button,
  Carregando,
  DataTable,
  EstadoVazio,
  FeedbackInline,
  PainelCriacaoInline,
  StatusChip,
  useConfirmar,
  useTipografia,
  type Coluna,
} from '@ui';
import {
  Add24Regular,
  Camera24Regular,
  Delete24Regular,
  Edit24Regular,
  Eye24Regular,
  Folder24Regular,
  FullScreenMaximize24Regular,
  FullScreenMinimize24Regular,
  Grid24Regular,
  List24Regular,
  VideoOff24Regular,
} from '@fluentui/react-icons';
import { api, tipoCameraLabel, type CameraResumo } from '../../lib/api';
import { useCameras, useObras } from '../../lib/consultas';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import { useSucessoToast } from '../../hooks/useSucessoToast';
import { escolherObraInicial } from '../../hooks/useObraLembrada';
import { ehCameraIp, estadoDaCamera, estadoExibido, formatarDataHora, formatarHora, situacaoDaCameraIp } from './rotulos';
import { mergeClasses, useEstilosMosaico } from './estilosMosaico';
import { FormularioCamera } from './FormularioCamera';
import { useEstilosCameraIp } from './estilosCameraIp';
import { MarcaEstado } from './PecasCameraIp';

type Visao = 'mosaico' | 'lista';
const CHAVE_VISAO = 'sst:camerasVisao';

function lerVisao(): Visao {
  try {
    return sessionStorage.getItem(CHAVE_VISAO) === 'lista' ? 'lista' : 'mosaico';
  } catch {
    return 'mosaico';
  }
}

function gravarVisao(visao: Visao) {
  try {
    sessionStorage.setItem(CHAVE_VISAO, visao);
  } catch {
    // Sem armazenamento (modo privado): só não lembra.
  }
}

const plural = (n: number, singular: string, pluralTexto: string) => `${n} ${n === 1 ? singular : pluralTexto}`;
const rotuloCameras = (n: number) => plural(n, 'câmera', 'câmeras');
const rotuloIntervalo = (minutos: number) => (minutos === 0 ? 'Sem limite' : `${minutos} minutos`);

interface PainelAberto {
  chave: number;
  camera?: CameraResumo;
  // Cadastro aberto pela aba "Equipamentos e agentes" (lente livre): obra e equipamento já escolhidos.
  obraInicial?: string;
  ipInicial?: { equipamentoId: string; canal: number };
}

// Aba "Câmeras": mosaico (blocos agrupados por obra) ou lista, com cadastro inline. Nesta fase os blocos
// não mostram vídeo — a imagem ao vivo só existe na tela Monitorar, do dispositivo que filma.
export function MosaicoCamerasTab() {
  const estilos = useEstilosMosaico();
  const ip = useEstilosCameraIp();
  const tipo = useTipografia();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const souAdministrador = useSouAdministrador();
  const { confirmar, dialogElement } = useConfirmar();
  const sucessoToast = useSucessoToast();
  const cameras = useCameras();
  const { lista: obras } = useObras();

  const [visao, setVisao] = useState<Visao>(lerVisao);
  const [obraFiltro, setObraFiltro] = useState('');
  const [painel, setPainel] = useState<PainelAberto | null>(null);
  const [painelAberto, setPainelAberto] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [avisoSalvo, setAvisoSalvo] = useState<string | null>(null);
  const [telaCheia, setTelaCheia] = useState(false);
  const raizRef = useRef<HTMLDivElement>(null);
  const painelRef = useRef<HTMLDivElement>(null);

  const lista = cameras.data;
  const todas = useMemo(() => lista ?? [], [lista]);

  useEffect(() => {
    const aoMudar = () => {
      setTelaCheia(document.fullscreenElement === raizRef.current);
      setAvisoSalvo(null);
    };
    document.addEventListener('fullscreenchange', aoMudar);
    return () => document.removeEventListener('fullscreenchange', aoMudar);
  }, []);

  useEffect(() => {
    if (painelAberto) {
      try {
        painelRef.current?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      } catch {
        // Navegador sem suporte: o formulário continua visível acima da lista.
      }
    }
  }, [painelAberto, painel?.chave]);

  // Link da lente livre (aba Equipamentos e agentes): abre o cadastro de câmera IP já apontando para o equipamento e limpa a URL.
  useEffect(() => {
    if (params.get('nova') !== '1') return;
    const equipamentoId = params.get('equipamento');
    const canal = Number(params.get('lente'));
    const obra = params.get('obra') ?? undefined;
    setPainel({
      chave: Date.now(),
      obraInicial: obra,
      ipInicial: equipamentoId && (canal === 1 || canal === 2) ? { equipamentoId, canal } : undefined,
    });
    setPainelAberto(true);
    setParams(
      (atual) => {
        const novo = new URLSearchParams(atual);
        ['nova', 'obra', 'equipamento', 'lente'].forEach((k) => novo.delete(k));
        return novo;
      },
      { replace: true },
    );
  }, [params, setParams]);

  const obrasComCamera = useMemo(() => {
    const mapa = new Map<string, { id: string; nome: string; total: number }>();
    for (const c of todas) {
      const atual = mapa.get(c.obraId);
      if (atual) atual.total += 1;
      else mapa.set(c.obraId, { id: c.obraId, nome: c.obraNome, total: 1 });
    }
    return [...mapa.values()].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
  }, [todas]);

  // Filtro que sumiu (a última câmera da obra foi excluída) volta para "Todas as obras".
  const filtroEfetivo = obrasComCamera.some((o) => o.id === obraFiltro) ? obraFiltro : '';

  const visiveis = useMemo(
    () => (filtroEfetivo ? todas.filter((c) => c.obraId === filtroEfetivo) : todas),
    [todas, filtroEfetivo],
  );

  const grupos = useMemo(
    () =>
      obrasComCamera
        .filter((o) => !filtroEfetivo || o.id === filtroEfetivo)
        .map((o) => ({
          obra: o,
          cameras: visiveis.filter((c) => c.obraId === o.id).sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')),
        })),
    [obrasComCamera, visiveis, filtroEfetivo],
  );

  function mudarVisao(nova: Visao) {
    setVisao(nova);
    gravarVisao(nova);
  }

  async function alternarTelaCheia() {
    try {
      if (document.fullscreenElement) await document.exitFullscreen();
      else await raizRef.current?.requestFullscreen();
    } catch {
      // O navegador pode recusar (ex.: dentro do Teams): a tela segue normal.
    }
  }

  function abrirCadastro() {
    setAvisoSalvo(null);
    setPainel({ chave: Date.now() });
    setPainelAberto(true);
  }

  function abrirEdicao(camera: CameraResumo) {
    setAvisoSalvo(null);
    setPainel({ chave: Date.now(), camera });
    setPainelAberto(true);
  }

  function fecharPainel() {
    setPainelAberto(false);
  }

  // Toast e diálogos renderizam fora do elemento em tela cheia: sai dela antes de avisar ou confirmar.
  async function sairDaTelaCheia() {
    try {
      if (document.fullscreenElement) await document.exitFullscreen();
    } catch {
      // O chamador confere se ainda está em tela cheia.
    }
  }

  async function aoSalvar(obraDaCamera: string) {
    const editou = !!painel?.camera;
    fecharPainel();
    if (!editou && filtroEfetivo && obraDaCamera !== filtroEfetivo) setObraFiltro('');
    await queryClient.invalidateQueries({ queryKey: ['cameras', 'lista'] });
    await sairDaTelaCheia();
    const mensagem = editou ? 'Câmera atualizada com sucesso.' : 'Câmera cadastrada com sucesso.';
    if (document.fullscreenElement) setAvisoSalvo(mensagem);
    else sucessoToast(mensagem);
  }

  async function excluir(camera: CameraResumo) {
    await sairDaTelaCheia();
    if (document.fullscreenElement) {
      setErro('Saia da tela cheia para excluir a câmera.');
      return;
    }
    if (!(await confirmar(`Excluir a câmera ${camera.nome}? As ocorrências já registradas continuam guardadas.`))) return;
    try {
      setErro(null);
      await api.cameras.excluir(camera.id);
      if (painel?.camera?.id === camera.id) fecharPainel();
      await queryClient.invalidateQueries({ queryKey: ['cameras', 'lista'] });
      sucessoToast('Câmera excluída com sucesso.');
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível excluir a câmera.');
    }
  }

  function EstadoMarca({ camera }: { camera: CameraResumo }) {
    if (ehCameraIp(camera)) {
      const situacao = situacaoDaCameraIp(camera);
      return <MarcaEstado tom={situacao.tom}>{situacao.rotulo}</MarcaEstado>;
    }
    const estado = estadoDaCamera(camera);
    const forma = { ok: estilos.formaOk, atencao: estilos.formaAtencao, neutro: estilos.formaNeutro }[
      estado.tom as 'ok' | 'atencao' | 'neutro'
    ];
    const cor = { ok: estilos.tomOk, atencao: estilos.tomAtencao, neutro: estilos.tomNeutro }[
      estado.tom as 'ok' | 'atencao' | 'neutro'
    ];
    return (
      <span className={mergeClasses(estilos.estado, tipo.legenda, cor)}>
        <span aria-hidden="true" className={mergeClasses(estilos.forma, forma)} />
        {estado.rotulo}
      </span>
    );
  }

  const colunas: Coluna<CameraResumo>[] = [
    {
      chave: 'nome',
      rotulo: 'Câmera',
      render: (c) => (
        <div className={estilos.celulaDupla}>
          <strong>{c.nome}</strong>
          <span className={mergeClasses(tipo.legenda, estilos.apoio)}>{c.obraNome}</span>
          {ehCameraIp(c) && (
            <span className={mergeClasses(tipo.legenda, estilos.apoio)}>
              Câmera IP · {c.equipamentoNome ?? 'equipamento'} · lente {c.canal ?? '—'}
            </span>
          )}
        </div>
      ),
    },
    { chave: 'area', rotulo: 'Área', render: (c) => c.areaNome ?? 'Sem área definida' },
    { chave: 'tipo', rotulo: 'Tipo', render: (c) => tipoCameraLabel[c.tipo] ?? '' },
    {
      chave: 'estado',
      rotulo: 'Monitoramento',
      render: (c) => {
        const estado = estadoExibido(c);
        if (!ehCameraIp(c) || !c.habilitada) return <StatusChip tom={estado.tom}>{estado.rotulo}</StatusChip>;
        return (
          <div className={ip.celulaSituacao}>
            <StatusChip tom={estado.tom}>{estado.rotulo}</StatusChip>
            <span className={mergeClasses(tipo.legenda, estilos.apoio)}>
              {c.ultimoQuadroRecebidoEmUtc ? `Última foto ${formatarHora(c.ultimoQuadroRecebidoEmUtc)}` : 'Ainda sem fotos'}
            </span>
          </div>
        );
      },
    },
    { chave: 'intervalo', rotulo: 'Intervalo entre alertas', render: (c) => rotuloIntervalo(c.intervaloMinimoAlertasMinutos) },
    {
      chave: 'ultima',
      rotulo: 'Última análise',
      render: (c) => (c.ultimaAnaliseEmUtc ? formatarDataHora(c.ultimaAnaliseEmUtc) : 'Ainda sem análise'),
    },
    { chave: 'ocorrencias', rotulo: 'Ocorrências 24 h', alinhar: 'direita', render: (c) => String(c.ocorrenciasHoje) },
    { chave: 'analises', rotulo: 'Análises', alinhar: 'direita', render: (c) => String(c.totalAnalises) },
  ];

  const botoesAcao = (c: CameraResumo, aoMonitorar: boolean) => (
    <div className={estilos.botoesAcao}>
      {aoMonitorar && ehCameraIp(c) && <span aria-hidden="true" className={ip.reservaBotao} />}
      {aoMonitorar && !ehCameraIp(c) && (
        <BotaoAcao
          tom="ver"
          icon={<Eye24Regular />}
          className={estilos.botaoToque}
          aria-label={`Monitorar a câmera ${c.nome}`}
          onClick={() => navigate(`/cameras/${c.id}/monitorar`)}
        />
      )}
      <BotaoAcao
        tom="ver"
        icon={<Edit24Regular />}
        className={estilos.botaoToque}
        aria-label={`Editar a câmera ${c.nome}`}
        onClick={() => abrirEdicao(c)}
      />
      {souAdministrador && (
        <BotaoAcao
          tom="excluir"
          icon={<Delete24Regular />}
          className={estilos.botaoToque}
          aria-label={`Excluir a câmera ${c.nome}`}
          onClick={() => void excluir(c)}
        />
      )}
    </div>
  );

  function bloco(c: CameraResumo) {
    const desligada = !c.habilitada;
    const estado = estadoDaCamera(c);
    const camIp = ehCameraIp(c);
    const situacaoIp = camIp ? situacaoDaCameraIp(c) : null;
    const ultimaFoto = c.ultimoQuadroRecebidoEmUtc ? formatarHora(c.ultimoQuadroRecebidoEmUtc) : null;
    const selo = c.ocorrenciasHoje > 0 && (
      <span className={mergeClasses(estilos.selo, tipo.legenda)}>
        {plural(c.ocorrenciasHoje, 'ocorrência', 'ocorrências')} em 24 h
      </span>
    );
    return (
      <div key={c.id} className={estilos.bloco}>
        {camIp && situacaoIp ? (
          // Câmera IP: o bloco não leva para Monitorar (a imagem vem do agente, não do aparelho). Mostra a situação e a hora
          // da última foto; a listagem ainda não devolve a imagem, então o lugar da foto é um bloco neutro.
          <div className={mergeClasses(ip.areaIp, tipo.corpo)}>
            {desligada ? <VideoOff24Regular /> : <Camera24Regular />}
            <span>{desligada ? 'Monitoramento desligado' : situacaoIp.rotulo}</span>
            {!desligada && situacaoIp.apoio && <span className={tipo.legenda}>{situacaoIp.apoio}</span>}
            {!desligada && ultimaFoto && <span className={tipo.legenda}>Última foto às {ultimaFoto}</span>}
            {selo}
          </div>
        ) : (
          <Link
            to={`/cameras/${c.id}/monitorar`}
            aria-label={`Abrir monitoramento da câmera ${c.nome}`}
            className={mergeClasses(estilos.area, tipo.corpo)}
          >
            {desligada ? <VideoOff24Regular /> : <Camera24Regular />}
            <span>{desligada ? 'Monitoramento desligado' : estado.rotulo}</span>
            {selo}
          </Link>
        )}
        <div className={estilos.rodape}>
          <div className={estilos.textos}>
            <div className={mergeClasses(tipo.subtitulo, estilos.nome)} title={c.nome}>
              {c.nome}
            </div>
            <div className={mergeClasses(tipo.legenda, estilos.apoio)}>
              {camIp
                ? `${tipoCameraLabel[c.tipo] ?? ''} · Câmera IP · lente ${c.canal ?? '—'} · ${ultimaFoto ? `última foto ${ultimaFoto}` : 'sem fotos ainda'}`
                : `${tipoCameraLabel[c.tipo] ?? ''} · ${c.ultimaAnaliseEmUtc ? `última análise ${formatarHora(c.ultimaAnaliseEmUtc)}` : 'ainda sem análise'}`}
            </div>
          </div>
          <EstadoMarca camera={c} />
        </div>
        <div className={estilos.acoes}>{botoesAcao(c, false)}</div>
      </div>
    );
  }

  const obraInicialForm = filtroEfetivo || escolherObraInicial(obras);

  return (
    <div ref={raizRef} className={estilos.raiz}>
      {dialogElement}

      <div className={estilos.barra}>
        <div role="group" aria-label="Modo de exibição" className={estilos.grupoVisao}>
          <button
            type="button"
            aria-label="Ver em mosaico"
            aria-pressed={visao === 'mosaico'}
            className={mergeClasses(estilos.botaoVisao, visao === 'mosaico' && estilos.botaoVisaoAtivo)}
            onClick={() => mudarVisao('mosaico')}
          >
            <Grid24Regular />
          </button>
          <button
            type="button"
            aria-label="Ver em lista"
            aria-pressed={visao === 'lista'}
            className={mergeClasses(estilos.botaoVisao, visao === 'lista' && estilos.botaoVisaoAtivo)}
            onClick={() => mudarVisao('lista')}
          >
            <List24Regular />
          </button>
        </div>
        {document.fullscreenEnabled && (
          <Button
            className={estilos.botao}
            icon={telaCheia ? <FullScreenMinimize24Regular /> : <FullScreenMaximize24Regular />}
            onClick={() => void alternarTelaCheia()}
          >
            {telaCheia ? 'Sair da tela cheia' : 'Tela cheia'}
          </Button>
        )}
        <Button
          appearance="primary"
          className={estilos.botao}
          icon={<Add24Regular />}
          onClick={painelAberto && !painel?.camera ? fecharPainel : abrirCadastro}
        >
          {painelAberto && !painel?.camera ? 'Fechar' : 'Nova câmera'}
        </Button>
      </div>

      {avisoSalvo && <FeedbackInline tom="sucesso" aoFechar={() => setAvisoSalvo(null)}>{avisoSalvo}</FeedbackInline>}
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div ref={painelRef}>
        <PainelCriacaoInline aberto={painelAberto} titulo={painel?.camera ? 'Editar câmera' : 'Cadastrar câmera'}>
          {painel && (
            <FormularioCamera
              key={painel.chave}
              camera={painel.camera}
              obras={obras}
              obraInicial={painel.obraInicial ?? obraInicialForm}
              ipInicial={painel.ipInicial}
              aoSalvar={(obraDaCamera) => void aoSalvar(obraDaCamera)}
              aoCancelar={fecharPainel}
            />
          )}
        </PainelCriacaoInline>
      </div>

      {cameras.isLoading && <Carregando variante="card" linhas={3} />}

      {cameras.isError && (
        <FeedbackInline
          tom="erro"
          acao={{ rotulo: 'Tentar de novo', aoClicar: () => void cameras.refetch() }}
        >
          {cameras.error instanceof Error ? cameras.error.message : 'Não foi possível carregar as câmeras.'}
        </FeedbackInline>
      )}

      {!cameras.isLoading && !cameras.isError && todas.length === 0 && (
        <EstadoVazio
          icone={<Camera24Regular />}
          titulo="Nenhuma câmera cadastrada"
          descricao="Cadastre a primeira câmera para começar a monitorar capacete e colete."
          acao={{ rotulo: 'Nova câmera', aoClicar: abrirCadastro }}
        />
      )}

      {todas.length > 0 && (
        <>
          <div className={estilos.filtros} role="group" aria-label="Filtrar por obra">
            <button
              type="button"
              aria-pressed={filtroEfetivo === ''}
              className={mergeClasses(estilos.cartaoFiltro, tipo.corpo, filtroEfetivo === '' && estilos.cartaoFiltroAtivo)}
              onClick={() => setObraFiltro('')}
            >
              Todas as obras
              <span className={mergeClasses(tipo.legenda, estilos.apoio)}>{rotuloCameras(todas.length)}</span>
            </button>
            {obrasComCamera.map((o) => (
              <button
                key={o.id}
                type="button"
                aria-pressed={filtroEfetivo === o.id}
                className={mergeClasses(estilos.cartaoFiltro, tipo.corpo, filtroEfetivo === o.id && estilos.cartaoFiltroAtivo)}
                onClick={() => setObraFiltro(o.id)}
              >
                {o.nome}
                <span className={mergeClasses(tipo.legenda, estilos.apoio)}>{rotuloCameras(o.total)}</span>
              </button>
            ))}
          </div>

          {visao === 'mosaico' ? (
            grupos.map(({ obra, cameras: daObra }) => (
              <section key={obra.id} className={estilos.grupo} aria-label={`Câmeras de ${obra.nome}`}>
                <div className={estilos.cabecalhoGrupo}>
                  <Folder24Regular aria-hidden="true" />
                  <h2 className={mergeClasses(tipo.titulo, estilos.tituloGrupo)}>
                    {obra.nome} <span className={estilos.apoio}>({rotuloCameras(daObra.length)})</span>
                  </h2>
                </div>
                <div className={estilos.grade}>{daObra.map(bloco)}</div>
              </section>
            ))
          ) : (
            <div className={estilos.tabela}>
              <DataTable
                aria-label="Lista de câmeras"
                colunas={colunas}
                linhas={visiveis}
                chaveLinha={(c) => c.id}
                acoesLinha={(c) => botoesAcao(c, true)}
              />
            </div>
          )}
        </>
      )}

      <FeedbackInline tom="info">
        Nesta fase só o dispositivo que está monitorando mostra a imagem ao vivo, da própria câmera. O mosaico com
        várias imagens ao vivo depende das câmeras fixas da obra.
        <br />
        <strong>Câmeras IP:</strong> o bloco mostra a situação da conexão e a hora da última foto recebida. As fotos seguem
        quando há movimento na cena; não é vídeo ao vivo.
      </FeedbackInline>
    </div>
  );
}
