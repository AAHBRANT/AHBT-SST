import { useEffect, useState, type CSSProperties, type ReactElement } from 'react';
import { useLocation, useNavigate, useParams } from 'react-router-dom';
import {
  Button,
  Card,
  Abas,
  FeedbackInline,
  Carregando,
  Text,
  designTokens,
  tokensUi,
  useTipografia,
  type AbaItem,
} from '@ui';
import {
  ArrowLeft16Regular,
  Building24Regular,
  Flag24Regular,
  HatGraduation24Regular,
  Home24Regular,
  ShieldTask24Regular,
  Signature24Regular,
  Warning24Regular,
} from '@fluentui/react-icons';
import { api, TipoVinculo, type PerfilCompletoTrabalhador } from '../../lib/api';
import { salvarBlob, useVisualizadorPdf } from '../../components/useVisualizadorPdf';
import { PerfilGeralTab } from './PerfilGeralTab';
import { TamanhosUniformeSecao } from './TamanhosUniformeSecao';
import { TreinamentosTab } from './TreinamentosTab';
import { RiscosTab } from './RiscosTab';
import { ExamesDaFuncaoCard, GheDaFuncaoCard } from '../../components/estrutura-sst/CardsEstruturaFuncao';
import { OcorrenciasTab } from './OcorrenciasTab';
import { CofreAssinaturasTab } from './CofreAssinaturasTab';
import { TerceirizadoTab } from './TerceirizadoTab';
import { TermoCompromissoEpiCartao } from './TermoCompromissoEpiCartao';
import { FichaTrabalhadorLateral } from './FichaTrabalhadorLateral';
import { BiometriaDigitalCartao } from './BiometriaDigitalCartao';
import { EpiPerfilSecao } from './EpiPerfilSecao';
import { AnelProgresso } from './AnelProgresso';

type AbaPerfil = 'geral' | 'epi' | 'treinamentos' | 'riscos' | 'ocorrencias' | 'cofre' | 'terceirizado';

// Rótulos curtos (Modelo A, 21/10): o "& ASO", "& Matriz" etc. saiu do nome e foi para a descrição,
// que aparece como título da seção logo abaixo das abas.
const ABAS_PERFIL: ReadonlyArray<{ valor: AbaPerfil; rotulo: string; descricao: string; icone: ReactElement }> = [
  { valor: 'geral', rotulo: 'Geral', descricao: 'Situação atual, ASO, exames da função e uniforme', icone: <Home24Regular /> },
  { valor: 'epi', rotulo: 'EPI', descricao: 'Termo de compromisso e frequência de trocas', icone: <ShieldTask24Regular /> },
  { valor: 'treinamentos', rotulo: 'Treinamentos', descricao: 'NRs, certificados e presença em DDS', icone: <HatGraduation24Regular /> },
  { valor: 'riscos', rotulo: 'Riscos', descricao: 'GHE da função, riscos expostos e Ordem de Serviço', icone: <Warning24Regular /> },
  { valor: 'ocorrencias', rotulo: 'Ocorrências', descricao: 'Histórico de eventos do trabalhador', icone: <Flag24Regular /> },
  { valor: 'cofre', rotulo: 'Assinaturas', descricao: 'Cofre de documentos assinados', icone: <Signature24Regular /> },
  { valor: 'terceirizado', rotulo: 'Terceirizado', descricao: 'Empresa contratada, contrato e documentos', icone: <Building24Regular /> },
];

// Abaixo dessa largura a lateral deixa de ser fixa e vai para cima das abas (painel do Teams estreito, celular).
const CONSULTA_TELA_ESTREITA = '(max-width: 900px)';

const estiloPilha: CSSProperties = { display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.lg };

// Ficha do trabalhador: identificação fixa na lateral (foto, dados, aptidão, relatórios) + seis abas de
// conteúdo à direita. Sem fluxo de estados/workflow (a aptidão é só leitura, calculada a partir do ASO
// mais recente) — por isso layout próprio em vez de DetailPageLayout, que existe para lateral fixa de
// ações de fluxo (ver NaoConformidadeDetalhePage). As abas são navegação interna da página, não da URL — não
// há `useAbaNaUrl` aqui de propósito, ela é reservada aos dois níveis da página-pilar (spec §4.1).
export function TrabalhadorDetalhePage() {
  const { id } = useParams<{ id: string }>();
  // Quem chega pela lista de cadastros faciais para revisar já cai na aba do cofre (onde fica Assinatura).
  const abaInicial = (useLocation().state as { aba?: AbaPerfil } | null)?.aba;
  const [aba, setAba] = useState<AbaPerfil>(abaInicial ?? 'geral');
  const [perfil, setPerfil] = useState<PerfilCompletoTrabalhador | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [baixandoRelatorio, setBaixandoRelatorio] = useState(false);
  const [fotoUrl, setFotoUrl] = useState<string | null>(null);
  const { visualizar, dialogoVisualizador } = useVisualizadorPdf();
  const navigate = useNavigate();
  const tipo = useTipografia();
  const [telaEstreita, setTelaEstreita] = useState(() => window.matchMedia(CONSULTA_TELA_ESTREITA).matches);

  useEffect(() => {
    const media = window.matchMedia(CONSULTA_TELA_ESTREITA);
    const atualizar = () => setTelaEstreita(media.matches);
    media.addEventListener('change', atualizar);
    return () => media.removeEventListener('change', atualizar);
  }, []);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setPerfil(await api.trabalhadores.obterPerfilCompleto(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar perfil do funcionário.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // Foto real do trabalhador — baixada sob demanda quando temFoto=true, mesmo padrão de
  // TrabalhadoresTab.tsx/ObrasPage.tsx. Sem foto, o Avatar cai para as iniciais do nome.
  useEffect(() => {
    if (!perfil?.temFoto) return;
    let cancelado = false;
    let urlCriada: string | null = null;
    (async () => {
      try {
        const blob = await api.trabalhadores.baixarFoto(perfil.id);
        if (cancelado) return;
        urlCriada = URL.createObjectURL(blob);
        setFotoUrl(urlCriada);
      } catch {
        // Falha ao carregar a foto não impede o uso da página; o trabalhador fica com iniciais.
      }
    })();
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [perfil?.id, perfil?.temFoto]);

  async function baixarRelatorio() {
    if (!id) return;
    try {
      setBaixandoRelatorio(true);
      setErro(null);
      salvarBlob(await api.trabalhadores.baixarRelatorioFiscalizacao(id), `relatorio-fiscalizacao-${id}.pdf`);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o relatório de fiscalização.');
    } finally {
      setBaixandoRelatorio(false);
    }
  }

  // Mesmo relatório do botão "Emitir", aberto na janela de visualização.
  function visualizarRelatorio() {
    if (!id) return;
    const trabalhadorId = id;
    void visualizar({
      titulo: `Relatório de fiscalização — ${perfil?.nome ?? 'Funcionário'}`,
      nomeArquivo: `relatorio-fiscalizacao-${trabalhadorId}.pdf`,
      obter: () => api.trabalhadores.baixarRelatorioFiscalizacao(trabalhadorId),
    });
  }

  if (!id) {
    return <FeedbackInline tom="erro">Funcionário não encontrado.</FeedbackInline>;
  }

  const contadorPorAba: Partial<Record<AbaPerfil, number>> = perfil
    ? {
        epi: perfil.episAtivos.length,
        treinamentos: perfil.treinamentos.length,
        riscos: perfil.riscos.length,
        ocorrencias: perfil.ocorrencias.length,
        cofre: perfil.assinaturas.length,
      }
    : {};

  const abasVisiveis: AbaItem<AbaPerfil>[] = ABAS_PERFIL.filter(
    (a) => a.valor !== 'terceirizado' || perfil?.vinculo === TipoVinculo.Terceirizado,
  ).map((a) => ({ valor: a.valor, rotulo: a.rotulo, icone: a.icone, contador: contadorPorAba[a.valor] }));
  const abaAtual = ABAS_PERFIL.find((a) => a.valor === aba) ?? ABAS_PERFIL[0];


  return (
    <div>
      <Button
        appearance="subtle"
        size="small"
        icon={<ArrowLeft16Regular />}
        style={{ marginBottom: tokensUi.espaco.md, color: designTokens.colorNeutralMedium }}
        onClick={() => navigate('/operacao/pessoas')}
      >
        Pessoas
      </Button>

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {dialogoVisualizador}

      {!perfil ? (
        <Carregando variante="detalhe" linhas={6} />
      ) : (
        <div
          style={{
            display: 'grid',
            gridTemplateColumns: telaEstreita ? 'minmax(0, 1fr)' : '300px minmax(0, 1fr)',
            gap: 20,
            alignItems: 'start',
          }}
        >
          {/* A coluna fixa ganhou o cartão de biometria (21/10); com os dois cartões ela pode passar da
              altura da tela, então rola por dentro em vez de cortar o fim. */}
          <aside
            style={
              telaEstreita
                ? estiloPilha
                : {
                    ...estiloPilha,
                    position: 'sticky',
                    top: tokensUi.espaco.lg,
                    maxHeight: `calc(100vh - ${tokensUi.espaco.xxl})`,
                    overflowY: 'auto',
                  }
            }
            aria-label="Identificação do trabalhador"
          >
            <FichaTrabalhadorLateral
              perfil={perfil}
              fotoUrl={fotoUrl}
              baixandoRelatorio={baixandoRelatorio}
              aoVisualizarRelatorio={visualizarRelatorio}
              aoBaixarRelatorio={baixarRelatorio}
            />
            <BiometriaDigitalCartao trabalhadorId={perfil.id} trabalhadorNome={perfil.nome} aoAlterar={carregar} />
          </aside>

          <div style={{ ...estiloPilha, minWidth: 0 }}>
            {/* Abas já traz margem própria (10px em cima, 18px embaixo); aqui o espaçamento vem do gap da pilha. */}
            <div style={{ marginTop: -10, marginBottom: -18 }}>
              <Abas nivel="modulo" aria-label="Seções do perfil" abas={abasVisiveis} valor={aba} aoMudar={setAba} />
            </div>

            <div>
              <h2 className={tipo.subtitulo} style={{ margin: 0 }}>{abaAtual.rotulo}</h2>
              <div className={tipo.legenda} style={{ color: designTokens.colorNeutralMedium, marginTop: 2 }}>
                {abaAtual.descricao}
              </div>
            </div>

            {aba === 'geral' && (
              <div style={estiloPilha}>
                <PerfilGeralTab perfil={perfil} />
                <ExamesDaFuncaoCard obraId={perfil.obraId} funcaoId={perfil.funcaoId} />
                <TamanhosUniformeSecao trabalhadorId={id!} />
              </div>
            )}
            {aba === 'epi' && (
              <div style={estiloPilha}>
                <TermoCompromissoEpiCartao trabalhadorId={id} trabalhadorNome={perfil.nome} />
                <EpiPerfilSecao perfil={perfil} />
              </div>
            )}
            {aba === 'treinamentos' && (
              <div style={estiloPilha}>
                <Card>
                  {perfil.assiduidadeDds.totalRealizados === 0 ? (
                    <Text>Nenhum DDS realizado na obra desde a admissão.</Text>
                  ) : (
                    <div style={{ display: 'flex', alignItems: 'center', gap: tokensUi.espaco.xl, flexWrap: 'wrap' }}>
                      <AnelProgresso
                        fracao={perfil.assiduidadeDds.totalParticipados / perfil.assiduidadeDds.totalRealizados}
                        tom="ok"
                        centro={`${Math.round((perfil.assiduidadeDds.totalParticipados / perfil.assiduidadeDds.totalRealizados) * 100)}%`}
                        legenda="presença"
                        aria-label="Presença em DDS"
                      />
                      <div>
                        <div className={tipo.micro} style={{ color: designTokens.colorNeutralMedium }}>
                          Assiduidade em DDS
                        </div>
                        <div className={tipo.titulo}>
                          {perfil.assiduidadeDds.totalParticipados} de {perfil.assiduidadeDds.totalRealizados} DDS
                        </div>
                        <Text style={{ color: designTokens.colorNeutralMedium }}>Desde a admissão na obra</Text>
                      </div>
                    </div>
                  )}
                </Card>
                <TreinamentosTab trabalhadorId={id} obraId={perfil.obraId} />
              </div>
            )}
            {aba === 'riscos' && (
              <div style={estiloPilha}>
                <GheDaFuncaoCard obraId={perfil.obraId} funcaoId={perfil.funcaoId} />
                <RiscosTab riscos={perfil.riscos} />
              </div>
            )}
            {aba === 'ocorrencias' && <OcorrenciasTab ocorrencias={perfil.ocorrencias} />}
            {aba === 'cofre' && <CofreAssinaturasTab trabalhadorId={id} assinaturas={perfil.assinaturas} />}
            {aba === 'terceirizado' && <TerceirizadoTab trabalhadorId={id} perfil={perfil} />}
          </div>
        </div>
      )}
    </div>
  );
}
