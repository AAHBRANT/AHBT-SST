import { FluentProvider } from '@fluentui/react-components';
import { HashRouter, Navigate, Outlet, Route, Routes, useLocation, useParams } from 'react-router-dom';
import { aahbrantTheme, aahbrantLightTheme } from './theme';
import { ThemeModeProvider, useThemeMode } from './theme/ThemeModeContext';
import { AppShell } from './layout/AppShell';
import { DashboardPage } from './pages/DashboardPage';
import { GestaoSstPage } from './pages/gestao-sst/GestaoSstPage';
import { OperacaoPage } from './pages/operacao/OperacaoPage';
import { OcorrenciasPage } from './pages/ocorrencias/OcorrenciasPage';
import { PessoasPage } from './pages/pessoas/PessoasPage';
import { TrabalhadorDetalhePage } from './pages/pessoas/TrabalhadorDetalhePage';
import { PgrDetalhePage } from './pages/pgr/PgrDetalhePage';
import { AprDetalhePage } from './pages/apr/AprDetalhePage';
import { PermissaoTrabalhoDetalhePage } from './pages/pt/PermissaoTrabalhoDetalhePage';
import { AssinarPtPage } from './pages/pt/AssinarPtPage';
import { InspecaoDetalhePage } from './pages/inspecoes/InspecaoDetalhePage';
import { AssinarInspecaoPage } from './pages/inspecoes/AssinarInspecaoPage';
import { IdentificacaoPublicaPage } from './pages/identificacao/IdentificacaoPublicaPage';
import { ValidarDocumentoPage } from './pages/validacao/ValidarDocumentoPage';
import { AdministracaoPage } from './pages/administracao/AdministracaoPage';
import { NaoConformidadeDetalhePage } from './pages/naoconformidades/NaoConformidadeDetalhePage';
import { AlertasPage } from './pages/alertas/AlertasPage';
import { CalendarioPage } from './pages/calendario/CalendarioPage';
import { AcidenteDetalhePage } from './pages/acidentes/AcidenteDetalhePage';
import { DdsSemanalDetalhePage } from './pages/dds/DdsSemanalDetalhePage';
import { DdsDetalhePage } from './pages/dds/DdsDetalhePage';
import { AssinarDdsPage } from './pages/dds/AssinarDdsPage';
import { AssinarEntregaEpiPage } from './pages/epi/AssinarEntregaEpiPage';
import { SaudeOcupacionalPage } from './pages/saude-ocupacional/SaudeOcupacionalPage';
import { PcmsoDetalhePage } from './pages/saude-ocupacional/PcmsoDetalhePage';
import { ProcessoEleitoralCipaDetalhePage } from './pages/cipa/ProcessoEleitoralCipaDetalhePage';
import { MembroCipaDetalhePage } from './pages/cipa/MembroCipaDetalhePage';
import { ReuniaoCipaDetalhePage } from './pages/cipa/ReuniaoCipaDetalhePage';
import { EventoSipatDetalhePage } from './pages/cipa/EventoSipatDetalhePage';
import { AssinarTreinamentoPage } from './pages/treinamentos/AssinarTreinamentoPage';
import { SessaoTreinamentoDetalhePage } from './pages/treinamentos/SessaoTreinamentoDetalhePage';

// Envolve as rotas internas do app com o AppShell (sidebar/header do Teams). As rotas públicas
// /p/:codigoOuUid e /validar/:token ficam de fora dessa camada — ver IdentificacaoPublicaPage/ValidarDocumentoPage.
function LayoutComTeams() {
  return (
    <AppShell>
      <Outlet />
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
    <ThemeModeProvider>
      <AppRoteado />
    </ThemeModeProvider>
  );
}

// Separado de App só pra poder usar useThemeMode (o hook precisa estar dentro do Provider) e
// escolher o tema do Fluent (claro/escuro) que o botão de dark/light mode alterna — ver
// ThemeModeContext.tsx e o botão em AppShell.tsx.
function AppRoteado() {
  const { modo } = useThemeMode();
  return (
    <FluentProvider theme={modo === 'dark' ? aahbrantTheme : aahbrantLightTheme}>
      <HashRouter>
        <Routes>
          {/* Páginas públicas (abertas via QR code, sem sidebar/header do Teams) ficam no tema claro
              original — não fazem parte do app interno e não devem escurecer junto com ele (02/09). */}
          <Route
            path="/p/:codigoOuUid"
            element={
              <FluentProvider theme={aahbrantLightTheme}>
                <IdentificacaoPublicaPage />
              </FluentProvider>
            }
          />
          <Route
            path="/validar/:token"
            element={
              <FluentProvider theme={aahbrantLightTheme}>
                <ValidarDocumentoPage />
              </FluentProvider>
            }
          />
          <Route element={<LayoutComTeams />}>
            <Route path="/" element={<DashboardPage />} />

            {/* Reformulação de navegação (pedido do usuário, 02/09, réplica de mockup): os itens que
                antes abriam cada um a própria tela (PGR/GRO, PCMSO, Treinamentos, EPI/EPC, CIPA,
                DDS, Documentos & Procedimentos, Requisitos Legais) viraram abas de GestaoSstPage —
                a sidebar mostra só o item "Gestão de SST". As rotas antigas continuam existindo
                como redirecionamento (preservam links/favoritos antigos e qualquer querystring que
                já suportassem, ex.: ?aba=riscos), e as rotas de detalhe (:id) não mudam de lugar. */}
            <Route path="/gestao-sst" element={<GestaoSstPage />} />
            <Route path="/prevencao" element={<Navigate to="/gestao-sst?secao=pgr" replace />} />
            <Route path="/prevencao/pgr" element={<RedirecionarParaPilar pilar="/gestao-sst" secao="pgr" />} />
            <Route path="/prevencao/pgr/:id" element={<PgrDetalhePage />} />
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
            <Route path="/calendario" element={<CalendarioPage />} />

            {/* Item "Pessoas" da sidebar: Funcionários/Funções/Dashboard são abas de PessoasPage.
                "ASO & Exames" chegou a ser uma 2ª aba aqui (via PessoasPillarPage, réplica de
                mockup de 02/09), reaproveitando SaudeOcupacionalPage — removida em 03/09 (pedido
                do usuário) por duplicar a mesma tela já acessível em Gestão de SST → PCMSO. */}
            <Route path="/pessoas" element={<PessoasPage />} />
            <Route path="/pessoas/:id" element={<TrabalhadorDetalhePage />} />

            {/* Item "Ocorrências" da sidebar: Acidentes/Incidentes/Quase-acidentes (já eram a mesma
                tela filtrada por tipo) e Não Conformidades viraram abas de OcorrenciasPage. */}
            <Route path="/ocorrencias" element={<OcorrenciasPage />} />
            <Route path="/nao-conformidades" element={<Navigate to="/ocorrencias?secao=nao-conformidades" replace />} />
            <Route path="/nao-conformidades/:id" element={<NaoConformidadeDetalhePage />} />
            <Route path="/acidentes" element={<Navigate to="/ocorrencias?secao=acidentes" replace />} />
            <Route path="/acidentes/:id" element={<AcidenteDetalhePage />} />

            <Route path="/epi/:id/assinar" element={<AssinarEntregaEpiPage />} />
            <Route path="/treinamentos/:id/assinar" element={<AssinarTreinamentoPage />} />
            <Route path="/administracao" element={<AdministracaoPage />} />

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
    </FluentProvider>
  );
}

export default App;
