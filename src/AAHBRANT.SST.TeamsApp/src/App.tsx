import { lazy, Suspense, useEffect, useState } from 'react';
import { FluentProvider } from '@fluentui/react-components';
import { QueryClientProvider } from '@tanstack/react-query';
import { ganchosEstiloBotao } from './ui/primitivos/Botao/estiloBotao';
import { MotionConfig } from 'framer-motion';
import { HashRouter, Navigate, Outlet, Route, Routes, useLocation, useParams } from 'react-router-dom';
import { aahbrantTheme, aahbrantLightTheme } from './theme';
import { ThemeModeProvider, useThemeMode } from './theme/ThemeModeContext';
import { UsuarioLogadoProvider } from './lib/UsuarioLogadoContext';
import { ObraAtivaProvider } from './lib/ObraAtivaContext';
import { processarLoginNavegadorPendente } from './lib/browserAuth';
import { queryClient } from './lib/consultas';
import { AppShell } from './layout/AppShell';
import { Carregando } from '@ui';

// Cada tela vira um chunk próprio, baixado só quando a rota é aberta — antes o app inteiro
// (todas as páginas + recharts) saía num único JS de ~2,9 MB, encostando no limite de 3 MiB do
// precache do service worker (vite.config.ts). O shell (AppShell) continua no bundle inicial.
const DashboardPage = lazy(() => import('./pages/DashboardPage').then((m) => ({ default: m.DashboardPage })));
const GestaoSstPage = lazy(() => import('./pages/gestao-sst/GestaoSstPage').then((m) => ({ default: m.GestaoSstPage })));
const OperacaoPage = lazy(() => import('./pages/operacao/OperacaoPage').then((m) => ({ default: m.OperacaoPage })));
const OcorrenciasPage = lazy(() => import('./pages/ocorrencias/OcorrenciasPage').then((m) => ({ default: m.OcorrenciasPage })));
const PessoasPage = lazy(() => import('./pages/pessoas/PessoasPage').then((m) => ({ default: m.PessoasPage })));
const TrabalhadorDetalhePage = lazy(() => import('./pages/pessoas/TrabalhadorDetalhePage').then((m) => ({ default: m.TrabalhadorDetalhePage })));
const PgrDetalhePage = lazy(() => import('./pages/pgr/PgrDetalhePage').then((m) => ({ default: m.PgrDetalhePage })));
const GeracaoPgrEapPage = lazy(() => import('./pages/pgr/GeracaoPgrEapPage').then((m) => ({ default: m.GeracaoPgrEapPage })));
const LeituraIaRevisaoPage = lazy(() => import('./pages/leitura-ia/LeituraIaRevisaoPage').then((m) => ({ default: m.LeituraIaRevisaoPage })));
const AprDetalhePage = lazy(() => import('./pages/apr/AprDetalhePage').then((m) => ({ default: m.AprDetalhePage })));
const PermissaoTrabalhoDetalhePage = lazy(() => import('./pages/pt/PermissaoTrabalhoDetalhePage').then((m) => ({ default: m.PermissaoTrabalhoDetalhePage })));
const AssinarPtPage = lazy(() => import('./pages/pt/AssinarPtPage').then((m) => ({ default: m.AssinarPtPage })));
const AssinarAprPage = lazy(() => import('./pages/apr/AssinarAprPage').then((m) => ({ default: m.AssinarAprPage })));
const InspecaoDetalhePage = lazy(() => import('./pages/inspecoes/InspecaoDetalhePage').then((m) => ({ default: m.InspecaoDetalhePage })));
const AssinarInspecaoPage = lazy(() => import('./pages/inspecoes/AssinarInspecaoPage').then((m) => ({ default: m.AssinarInspecaoPage })));
const IdentificacaoPublicaPage = lazy(() => import('./pages/identificacao/IdentificacaoPublicaPage').then((m) => ({ default: m.IdentificacaoPublicaPage })));
const ValidarDocumentoPage = lazy(() => import('./pages/validacao/ValidarDocumentoPage').then((m) => ({ default: m.ValidarDocumentoPage })));
const AdministracaoPage = lazy(() => import('./pages/administracao/AdministracaoPage').then((m) => ({ default: m.AdministracaoPage })));
const SuporteIaPage = lazy(() => import('./pages/suporte-ia/SuporteIaPage').then((m) => ({ default: m.SuporteIaPage })));
const SuporteIaDetalhePage = lazy(() => import('./pages/suporte-ia/SuporteIaDetalhePage').then((m) => ({ default: m.SuporteIaDetalhePage })));
const NaoConformidadeDetalhePage = lazy(() => import('./pages/naoconformidades/NaoConformidadeDetalhePage').then((m) => ({ default: m.NaoConformidadeDetalhePage })));
const AlertasPage = lazy(() => import('./pages/alertas/AlertasPage').then((m) => ({ default: m.AlertasPage })));
const RelatoriosPage = lazy(() => import('./pages/relatorios/RelatoriosPage').then((m) => ({ default: m.RelatoriosPage })));
const CalendarioPage = lazy(() => import('./pages/calendario/CalendarioPage').then((m) => ({ default: m.CalendarioPage })));
const AcidenteDetalhePage = lazy(() => import('./pages/acidentes/AcidenteDetalhePage').then((m) => ({ default: m.AcidenteDetalhePage })));
const DdsSemanalDetalhePage = lazy(() => import('./pages/dds/DdsSemanalDetalhePage').then((m) => ({ default: m.DdsSemanalDetalhePage })));
const DdsDetalhePage = lazy(() => import('./pages/dds/DdsDetalhePage').then((m) => ({ default: m.DdsDetalhePage })));
const AssinarDdsPage = lazy(() => import('./pages/dds/AssinarDdsPage').then((m) => ({ default: m.AssinarDdsPage })));
const AssinarEntregaEpiPage = lazy(() => import('./pages/epi/AssinarEntregaEpiPage').then((m) => ({ default: m.AssinarEntregaEpiPage })));
const AssinarTermoEpiPage = lazy(() => import('./pages/epi/AssinarTermoEpiPage').then((m) => ({ default: m.AssinarTermoEpiPage })));
const SaudeOcupacionalPage = lazy(() => import('./pages/saude-ocupacional/SaudeOcupacionalPage').then((m) => ({ default: m.SaudeOcupacionalPage })));
const PcmsoDetalhePage = lazy(() => import('./pages/saude-ocupacional/PcmsoDetalhePage').then((m) => ({ default: m.PcmsoDetalhePage })));
const ProcessoEleitoralCipaDetalhePage = lazy(() => import('./pages/cipa/ProcessoEleitoralCipaDetalhePage').then((m) => ({ default: m.ProcessoEleitoralCipaDetalhePage })));
const MembroCipaDetalhePage = lazy(() => import('./pages/cipa/MembroCipaDetalhePage').then((m) => ({ default: m.MembroCipaDetalhePage })));
const ReuniaoCipaDetalhePage = lazy(() => import('./pages/cipa/ReuniaoCipaDetalhePage').then((m) => ({ default: m.ReuniaoCipaDetalhePage })));
const EventoSipatDetalhePage = lazy(() => import('./pages/cipa/EventoSipatDetalhePage').then((m) => ({ default: m.EventoSipatDetalhePage })));
const AssinarTreinamentoPage = lazy(() => import('./pages/treinamentos/AssinarTreinamentoPage').then((m) => ({ default: m.AssinarTreinamentoPage })));
const SessaoTreinamentoDetalhePage = lazy(() => import('./pages/treinamentos/SessaoTreinamentoDetalhePage').then((m) => ({ default: m.SessaoTreinamentoDetalhePage })));
const GaleriaPage = lazy(() => import('./ui/galeria/GaleriaPage').then((m) => ({ default: m.GaleriaPage })));
const TerceirizadoPage = lazy(() => import('./pages/terceirizados/TerceirizadoPage').then((m) => ({ default: m.TerceirizadoPage })));
const EmpresaDetalhePage = lazy(() => import('./pages/terceirizados/EmpresaDetalhePage').then((m) => ({ default: m.EmpresaDetalhePage })));
const ContratoDetalhePage = lazy(() => import('./pages/terceirizados/ContratoDetalhePage').then((m) => ({ default: m.ContratoDetalhePage })));

// Envolve as rotas internas do app com o AppShell (sidebar/header do Teams). As rotas públicas
// /p/:codigoOuUid e /validar/:token ficam de fora dessa camada — ver IdentificacaoPublicaPage/ValidarDocumentoPage.
function LayoutComTeams() {
  return (
    <AppShell>
      {/* Suspense dentro do shell: sidebar/header ficam na tela enquanto o chunk da página chega. */}
      <Suspense fallback={<Carregando variante="detalhe" linhas={6} />}>
        <Outlet />
      </Suspense>
    </AppShell>
  );
}

// Redireciona uma rota antiga com :id para o equivalente novo aninhado sob o módulo-pilar
// (ver consolidação de navegação em 24/08 — memória a ser gravada ao final desta tarefa).
function RedirecionarComId({ para }: { para: (id: string) => string }) {
  const { id } = useParams<{ id: string }>();
  return <Navigate to={id ? para(id) : '/'} replace />;
}

// Redireciona uma rota antiga (item que tinha link próprio na sidebar, antes da reformulação de
// 02/09) para a página-pilar equivalente, preservando qualquer querystring que a rota antiga já
// suportasse (ex.: ?aba=riscos) — só adiciona/sobrescreve o parâmetro "secao".
function RedirecionarParaPilar({ pilar, secao }: { pilar: string; secao: string }) {
  const location = useLocation();
  const params = new URLSearchParams(location.search);
  params.set('secao', secao);
  return <Navigate to={`${pilar}?${params.toString()}`} replace />;
}

// HashRouter evita depender de configuração de rota no servidor durante o sideload no Teams.
function App() {
  return (
    // Cache de leituras compartilhado entre telas — ver lib/consultas.ts.
    <QueryClientProvider client={queryClient}>
      <ThemeModeProvider>
        {/* Quem está logado e o que pode fazer — uma chamada por sessão, no topo da árvore, para
            qualquer tela decidir se mostra uma ação restrita (ver UsuarioLogadoContext). */}
        <UsuarioLogadoProvider>
          {/* Obra escolhida no cabeçalho, valendo para o sistema inteiro (ver ObraAtivaContext). */}
          <ObraAtivaProvider>
            <AppRoteado />
          </ObraAtivaProvider>
        </UsuarioLogadoProvider>
      </ThemeModeProvider>
    </QueryClientProvider>
  );
}

function AppRoteado() {
  const { modo } = useThemeMode();
  const [loginProcessado, setLoginProcessado] = useState(() => !window.location.hash.startsWith('#code='));

  useEffect(() => {
    if (loginProcessado) {
      return;
    }

    processarLoginNavegadorPendente().finally(() => setLoginProcessado(true));
  }, [loginProcessado]);

  if (!loginProcessado) {
    return (
      <FluentProvider theme={modo === 'dark' ? aahbrantTheme : aahbrantLightTheme} customStyleHooks_unstable={ganchosEstiloBotao}>
        <div style={{ padding: 24 }}>Entrando...</div>
      </FluentProvider>
    );
  }

  return (
    <FluentProvider theme={modo === 'dark' ? aahbrantTheme : aahbrantLightTheme} customStyleHooks_unstable={ganchosEstiloBotao}>
      {/* Com 'reduzir movimento' no SO, o framer-motion desliga animações de transform/opacity
          (spec §1.5); o CSS em index.css cuida das keyframes/transições. */}
      <MotionConfig reducedMotion="user">
        <HashRouter>
          <Routes>
            {/* Páginas públicas (abertas via QR code, sem sidebar/header do Teams) ficam no tema claro
                original — não fazem parte do app interno e não devem escurecer junto com ele (02/09). */}
            <Route
              path="/p/:codigoOuUid"
              element={
                <FluentProvider theme={aahbrantLightTheme}>
                  <Suspense fallback={<Carregando variante="detalhe" linhas={6} />}>
                    <IdentificacaoPublicaPage />
                  </Suspense>
                </FluentProvider>
              }
            />
            <Route
              path="/validar/:token"
              element={
                <FluentProvider theme={aahbrantLightTheme}>
                  <Suspense fallback={<Carregando variante="detalhe" linhas={6} />}>
                    <ValidarDocumentoPage />
                  </Suspense>
                </FluentProvider>
              }
            />
            <Route element={<LayoutComTeams />}>
              <Route path="/" element={<DashboardPage />} />

              {/* Galeria da camada ui/ (spec 2026-09-07 §6) — só em desenvolvimento. */}
              {import.meta.env.DEV && <Route path="/ui-galeria" element={<GaleriaPage />} />}

              {/* Reformulação de navegação (pedido do usuário, 02/09, réplica de mockup): os itens que
                  antes abriam cada um a própria tela (PGR/GRO, PCMSO, Treinamentos, EPI/EPC, CIPA,
                  DDS, Documentos & Procedimentos, Requisitos Legais) viraram abas de GestaoSstPage —
                  a sidebar mostra só o item "Gestão de SST". As rotas antigas continuam existindo
                  como redirecionamento (preservam links/favoritos antigos e qualquer querystring que
                  já suportassem, ex.: ?aba=riscos), e as rotas de detalhe (:id) não mudam de lugar. */}
              <Route path="/gestao-sst" element={<GestaoSstPage />} />
              <Route path="/prevencao" element={<Navigate to="/gestao-sst?secao=pgr" replace />} />
              <Route path="/prevencao/pgr" element={<RedirecionarParaPilar pilar="/gestao-sst" secao="pgr" />} />
              <Route path="/prevencao/pgr/gerar-eap" element={<GeracaoPgrEapPage />} />
              <Route path="/prevencao/pgr/gerar-eap/:geracaoId" element={<GeracaoPgrEapPage />} />
              <Route path="/prevencao/pgr/:id" element={<PgrDetalhePage />} />
              <Route path="/leituras-ia/:documento/:id" element={<LeituraIaRevisaoPage />} />
              <Route
                path="/prevencao/inspecoes"
                element={<RedirecionarParaPilar pilar="/operacao" secao="inspecoes" />}
              />
              <Route path="/prevencao/inspecoes/:id" element={<InspecaoDetalhePage />} />
              <Route path="/prevencao/inspecoes/:id/assinar" element={<AssinarInspecaoPage />} />
              <Route path="/prevencao/dds" element={<RedirecionarParaPilar pilar="/operacao" secao="dds" />} />
              <Route path="/prevencao/dds/semana/:id" element={<DdsSemanalDetalhePage />} />
              <Route path="/prevencao/dds/dia/:id" element={<DdsDetalhePage />} />
              <Route path="/prevencao/dds/dia/:id/assinar" element={<AssinarDdsPage />} />
              {/* Legado: Temas de DDS tinha sub-rota própria dentro do pilar (até 02/09) — virou aba
                  de DdsPage, dentro da aba "DDS" de Gestão de SST. */}
              <Route path="/prevencao/temas-dds" element={<Navigate to="/operacao?secao=dds&aba=temas-dds" replace />} />
              {/* Legado: /prevencao/pcmso apontava pro PCMSO antigo (descontinuado em 28/08 —
                  ver ONBOARDING.md) — redireciona pro módulo Saúde Ocupacional atual. Rota
                  /operacao/saude-ocupacional continua existindo sem o wrapper de pilar (link direto,
                  não tem mais item próprio na sidebar). */}
              <Route path="/prevencao/pcmso" element={<Navigate to="/operacao/saude-ocupacional" replace />} />
              <Route path="/prevencao/pcmso/:id" element={<RedirecionarComId para={(id) => `/operacao/saude-ocupacional/pcmso/${id}`} />} />
              <Route path="/treinamentos" element={<RedirecionarParaPilar pilar="/gestao-sst" secao="treinamentos" />} />
              <Route path="/treinamentos/turma/:id" element={<SessaoTreinamentoDetalhePage />} />
              <Route path="/epi" element={<RedirecionarParaPilar pilar="/operacao" secao="epi" />} />
              <Route path="/requisitos-legais" element={<RedirecionarParaPilar pilar="/gestao-sst" secao="requisitos-legais" />} />
              <Route path="/gestao-sst/documentos" element={<Navigate to="/gestao-sst?secao=documentos" replace />} />

              {/* Item "Operação" da sidebar: APR, PT, Inspeções, CIPA, EPI/EPC, DDS e Identificação
                  (rotulada "Outros controles operacionais") viraram abas de OperacaoPage — CIPA/EPI/DDS
                  vieram de Gestão de SST em 03/09 (pedido do usuário). Pessoas virou item de 1º nível
                  próprio (ver PessoasPillarPage); Obras virou aba de Administração (01/09); Ativos foi
                  removido do sistema (02/09, pedido explícito — "não vamos usar"). */}
              <Route path="/operacao" element={<OperacaoPage />} />
              <Route path="/operacao/apr" element={<RedirecionarParaPilar pilar="/operacao" secao="apr" />} />
              <Route path="/operacao/apr/:id" element={<AprDetalhePage />} />
              <Route path="/operacao/apr/:id/assinar" element={<AssinarAprPage />} />
              <Route path="/operacao/pt" element={<RedirecionarParaPilar pilar="/operacao" secao="pt" />} />
              <Route path="/operacao/pt/:id" element={<PermissaoTrabalhoDetalhePage />} />
              <Route path="/operacao/pt/:id/assinar" element={<AssinarPtPage />} />
              <Route
                path="/operacao/identificacao"
                element={<RedirecionarParaPilar pilar="/operacao" secao="identificacao" />}
              />
              <Route path="/operacao/saude-ocupacional" element={<SaudeOcupacionalPage />} />
              <Route path="/operacao/saude-ocupacional/pcmso/:id" element={<PcmsoDetalhePage />} />
              <Route path="/operacao/cipa" element={<RedirecionarParaPilar pilar="/operacao" secao="cipa" />} />
              <Route path="/operacao/cipa/eleicao/:id" element={<ProcessoEleitoralCipaDetalhePage />} />
              <Route path="/operacao/cipa/membro/:id" element={<MembroCipaDetalhePage />} />
              <Route path="/operacao/cipa/reuniao/:id" element={<ReuniaoCipaDetalhePage />} />
              <Route path="/operacao/cipa/sipat/:id" element={<EventoSipatDetalhePage />} />

              <Route path="/alertas" element={<AlertasPage />} />
              <Route path="/relatorios" element={<RelatoriosPage />} />
              <Route path="/calendario" element={<CalendarioPage />} />
              <Route path="/suporte-ia" element={<SuporteIaPage />} />
              <Route path="/suporte-ia/:id" element={<SuporteIaDetalhePage />} />

              {/* Item "Pessoas" da sidebar: Funcionários/Funções/Dashboard são abas de PessoasPage.
                  "ASO & Exames" chegou a ser uma 2ª aba aqui (via PessoasPillarPage, réplica de
                  mockup de 02/09), reaproveitando SaudeOcupacionalPage — removida em 03/09 (pedido
                  do usuário) por duplicar a mesma tela já acessível em Gestão de SST → PCMSO. */}
              <Route path="/pessoas" element={<PessoasPage />} />
              <Route path="/pessoas/:id" element={<TrabalhadorDetalhePage />} />

              <Route path="/terceirizados" element={<TerceirizadoPage />} />
              <Route path="/terceirizados/empresas/:id" element={<EmpresaDetalhePage />} />
              <Route path="/terceirizados/contratos/:id" element={<ContratoDetalhePage />} />

              {/* Item "Ocorrências" da sidebar: Acidentes/Incidentes/Quase-acidentes (já eram a mesma
                  tela filtrada por tipo) e Não Conformidades viraram abas de OcorrenciasPage. */}
              <Route path="/ocorrencias" element={<OcorrenciasPage />} />
              <Route path="/nao-conformidades" element={<Navigate to="/ocorrencias?secao=nao-conformidades" replace />} />
              <Route path="/nao-conformidades/:id" element={<NaoConformidadeDetalhePage />} />
              <Route path="/acidentes" element={<Navigate to="/ocorrencias?secao=acidentes" replace />} />
              <Route path="/acidentes/:id" element={<AcidenteDetalhePage />} />

              <Route path="/epi/:id/assinar" element={<AssinarEntregaEpiPage />} />
              <Route path="/epi/termo/:trabalhadorId/assinar" element={<AssinarTermoEpiPage />} />
              <Route path="/treinamentos/:id/assinar" element={<AssinarTreinamentoPage />} />
              <Route path="/administracao" element={<AdministracaoPage />} />
              <Route path="/administracao/suporte-ia" element={<Navigate to="/suporte-ia" replace />} />

              {/* Redirecionamentos legados: caminhos antigos (pré-consolidação de 24/08 e pré-reforma
                  de 02/09) apontando pras páginas-pilar atuais — preserva links/favoritos antigos. */}
              <Route path="/gestao-sst/treinamentos" element={<Navigate to="/gestao-sst?secao=treinamentos" replace />} />
              <Route path="/prevencao/riscos" element={<Navigate to="/gestao-sst?secao=pgr&aba=riscos" replace />} />
              <Route path="/riscos" element={<Navigate to="/gestao-sst?secao=pgr&aba=riscos" replace />} />
              <Route path="/pgr" element={<Navigate to="/gestao-sst?secao=pgr" replace />} />
              <Route path="/pgr/:id" element={<RedirecionarComId para={(id) => `/prevencao/pgr/${id}`} />} />
              <Route path="/pcmso" element={<Navigate to="/operacao/saude-ocupacional" replace />} />
              <Route path="/pcmso/:id" element={<RedirecionarComId para={(id) => `/operacao/saude-ocupacional/pcmso/${id}`} />} />
              <Route path="/inspecoes" element={<Navigate to="/operacao?secao=inspecoes" replace />} />
              <Route
                path="/inspecoes/:id"
                element={<RedirecionarComId para={(id) => `/prevencao/inspecoes/${id}`} />}
              />
              <Route path="/dds" element={<Navigate to="/operacao?secao=dds" replace />} />
              <Route path="/dds/:id" element={<RedirecionarComId para={(id) => `/prevencao/dds/dia/${id}`} />} />

              <Route path="/obras" element={<Navigate to="/administracao" replace />} />
              {/* Legado: Obras era aba de Operação (até 01/09), virou aba de Administração. */}
              <Route path="/operacao/obras" element={<Navigate to="/administracao" replace />} />
              {/* Legado: Pessoas era aba de Operação (até 28/08), virou item de 1º nível. */}
              <Route path="/operacao/pessoas" element={<Navigate to="/pessoas" replace />} />
              <Route path="/operacao/pessoas/:id" element={<RedirecionarComId para={(id) => `/pessoas/${id}`} />} />
              <Route path="/apr" element={<Navigate to="/operacao?secao=apr" replace />} />
              <Route path="/apr/:id" element={<RedirecionarComId para={(id) => `/operacao/apr/${id}`} />} />
              <Route path="/pt" element={<Navigate to="/operacao?secao=pt" replace />} />
              <Route path="/pt/:id" element={<RedirecionarComId para={(id) => `/operacao/pt/${id}`} />} />
              <Route path="/identificacao" element={<Navigate to="/operacao?secao=identificacao" replace />} />
              {/* Legado: Saúde Ocupacional era item de 1º nível na sidebar (até 28/08), virou aba
                  de Operação. */}
              <Route path="/saude-ocupacional" element={<Navigate to="/operacao/saude-ocupacional" replace />} />
              <Route
                path="/saude-ocupacional/pcmso/:id"
                element={<RedirecionarComId para={(id) => `/operacao/saude-ocupacional/pcmso/${id}`} />}
              />

              <Route path="/naoconformidades" element={<Navigate to="/ocorrencias?secao=nao-conformidades" replace />} />
              <Route
                path="/naoconformidades/:id"
                element={<RedirecionarComId para={(id) => `/nao-conformidades/${id}`} />}
              />

              {/* Módulo Melhoria Contínua removido (24/26/08) — Não Conformidades e Acidentes &
                  Incidentes viraram abas de Ocorrências. Redirecionamentos preservam links antigos. */}
              <Route path="/melhoria" element={<Navigate to="/ocorrencias?secao=nao-conformidades" replace />} />
              <Route path="/melhoria/nao-conformidades" element={<Navigate to="/ocorrencias?secao=nao-conformidades" replace />} />
              <Route
                path="/melhoria/nao-conformidades/:id"
                element={<RedirecionarComId para={(id) => `/nao-conformidades/${id}`} />}
              />
              <Route path="/melhoria/acidentes" element={<Navigate to="/ocorrencias?secao=acidentes" replace />} />
              <Route
                path="/melhoria/acidentes/:id"
                element={<RedirecionarComId para={(id) => `/acidentes/${id}`} />}
              />
            </Route>
          </Routes>
        </HashRouter>
      </MotionConfig>
    </FluentProvider>
  );
}

export default App;
