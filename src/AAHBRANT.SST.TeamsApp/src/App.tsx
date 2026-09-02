import { FluentProvider } from '@fluentui/react-components';
import { HashRouter, Navigate, Outlet, Route, Routes, useParams } from 'react-router-dom';
import { aahbrantTheme } from './theme';
import { AppShell } from './layout/AppShell';
import { DashboardPage } from './pages/DashboardPage';
import { PessoasPage } from './pages/pessoas/PessoasPage';
import { TrabalhadorDetalhePage } from './pages/pessoas/TrabalhadorDetalhePage';
import { TreinamentosPage } from './pages/treinamentos/TreinamentosPage';
import { PgrRiscosPage } from './pages/pgr/PgrRiscosPage';
import { PgrDetalhePage } from './pages/pgr/PgrDetalhePage';
import { AprsPage } from './pages/apr/AprsPage';
import { AprDetalhePage } from './pages/apr/AprDetalhePage';
import { PermissoesTrabalhoPage } from './pages/pt/PermissoesTrabalhoPage';
import { PermissaoTrabalhoDetalhePage } from './pages/pt/PermissaoTrabalhoDetalhePage';
import { AssinarPtPage } from './pages/pt/AssinarPtPage';
import { InspecoesPage } from './pages/inspecoes/InspecoesPage';
import { InspecaoDetalhePage } from './pages/inspecoes/InspecaoDetalhePage';
import { AssinarInspecaoPage } from './pages/inspecoes/AssinarInspecaoPage';
import { IdentificacaoPage } from './pages/identificacao/IdentificacaoPage';
import { AreaPublicaPage } from './pages/identificacao/AreaPublicaPage';
import { ValidarDocumentoPage } from './pages/validacao/ValidarDocumentoPage';
import { AdministracaoPage } from './pages/administracao/AdministracaoPage';
import { NaoConformidadesPage } from './pages/naoconformidades/NaoConformidadesPage';
import { RequisitosLegaisPage } from './pages/requisitoslegais/RequisitosLegaisPage';
import { NaoConformidadeDetalhePage } from './pages/naoconformidades/NaoConformidadeDetalhePage';
import { AlertasPage } from './pages/alertas/AlertasPage';
import { CalendarioPage } from './pages/calendario/CalendarioPage';
import { AcidentesPage } from './pages/acidentes/AcidentesPage';
import { AcidenteDetalhePage } from './pages/acidentes/AcidenteDetalhePage';
import { DdsPage } from './pages/dds/DdsPage';
import { DdsSemanalDetalhePage } from './pages/dds/DdsSemanalDetalhePage';
import { DdsDetalhePage } from './pages/dds/DdsDetalhePage';
import { AssinarDdsPage } from './pages/dds/AssinarDdsPage';
import { EpiPage } from './pages/epi/EpiPage';
import { AssinarEntregaEpiPage } from './pages/epi/AssinarEntregaEpiPage';
import { SaudeOcupacionalPage } from './pages/saude-ocupacional/SaudeOcupacionalPage';
import { PcmsoDetalhePage } from './pages/saude-ocupacional/PcmsoDetalhePage';
import { EmConstrucaoPage } from './pages/EmConstrucaoPage';
import { CipaPage } from './pages/cipa/CipaPage';
import { ProcessoEleitoralCipaDetalhePage } from './pages/cipa/ProcessoEleitoralCipaDetalhePage';
import { MembroCipaDetalhePage } from './pages/cipa/MembroCipaDetalhePage';
import { ReuniaoCipaDetalhePage } from './pages/cipa/ReuniaoCipaDetalhePage';
import { EventoSipatDetalhePage } from './pages/cipa/EventoSipatDetalhePage';
import { AssinarTreinamentoPage } from './pages/treinamentos/AssinarTreinamentoPage';

// Envolve as rotas internas do app com o AppShell (sidebar/header do Teams). As rotas públicas
// /p/:codigoOuUid e /validar/:token ficam de fora dessa camada — ver AreaPublicaPage/ValidarDocumentoPage.
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

// HashRouter evita depender de configuração de rota no servidor durante o sideload no Teams.
function App() {
  return (
    <FluentProvider theme={aahbrantTheme}>
      <HashRouter>
        <Routes>
          <Route path="/p/:codigoOuUid" element={<AreaPublicaPage />} />
          <Route path="/validar/:token" element={<ValidarDocumentoPage />} />
          <Route element={<LayoutComTeams />}>
            <Route path="/" element={<DashboardPage />} />

            {/* Cada item da sidebar abre só o que é dele (pedido do usuário, 02/09 — reverte os
                módulos-pilar "Procedimentos & Planos"/"Operação" de 24/08, que empilhavam abas de
                itens que já tinham link próprio, ex.: clicar em "CIPA" mostrava CIPA + APR + PT +
                Identificação + Ativos + Saúde Ocupacional juntos). Só ficam agrupados os pares que
                fazem sentido de verdade: PGR+Riscos (ver PgrRiscosPage) e DDS+Temas de DDS (ver
                DdsPage) — nos dois casos, o segundo item nunca teve link próprio na sidebar. */}
            <Route path="/prevencao" element={<Navigate to="/prevencao/pgr" replace />} />
            <Route path="/prevencao/pgr" element={<PgrRiscosPage />} />
            <Route path="/prevencao/pgr/:id" element={<PgrDetalhePage />} />
            <Route path="/prevencao/inspecoes" element={<InspecoesPage />} />
            <Route path="/prevencao/inspecoes/:id" element={<InspecaoDetalhePage />} />
            <Route path="/prevencao/inspecoes/:id/assinar" element={<AssinarInspecaoPage />} />
            <Route path="/prevencao/dds" element={<DdsPage />} />
            <Route path="/prevencao/dds/semana/:id" element={<DdsSemanalDetalhePage />} />
            <Route path="/prevencao/dds/dia/:id" element={<DdsDetalhePage />} />
            <Route path="/prevencao/dds/dia/:id/assinar" element={<AssinarDdsPage />} />
            {/* Legado: Temas de DDS tinha sub-rota própria dentro do pilar (até 02/09) — virou aba
                de DdsPage. */}
            <Route path="/prevencao/temas-dds" element={<Navigate to="/prevencao/dds?aba=temas-dds" replace />} />
            {/* Legado: /prevencao/pcmso apontava pro PCMSO antigo (descontinuado em 28/08 —
                ver ONBOARDING.md) — redireciona pro módulo Saúde Ocupacional atual. */}
            <Route path="/prevencao/pcmso" element={<Navigate to="/operacao/saude-ocupacional" replace />} />
            <Route path="/prevencao/pcmso/:id" element={<RedirecionarComId para={(id) => `/operacao/saude-ocupacional/pcmso/${id}`} />} />

            {/* Ex-pilar Operação — cada rota abaixo é independente agora (Pessoas virou item de 1º
                nível na sidebar; Obras saiu daqui e virou aba de Administração, pedido do usuário,
                01/09; Ativos/Extintores & Equipamentos foi removido do sistema em 02/09, pedido
                explícito do usuário — "não vamos usar"). */}
            <Route path="/operacao" element={<Navigate to="/operacao/apr" replace />} />
            <Route path="/operacao/apr" element={<AprsPage />} />
            <Route path="/operacao/apr/:id" element={<AprDetalhePage />} />
            <Route path="/operacao/pt" element={<PermissoesTrabalhoPage />} />
            <Route path="/operacao/pt/:id" element={<PermissaoTrabalhoDetalhePage />} />
            <Route path="/operacao/pt/:id/assinar" element={<AssinarPtPage />} />
            <Route path="/operacao/identificacao" element={<IdentificacaoPage />} />
            <Route path="/operacao/saude-ocupacional" element={<SaudeOcupacionalPage />} />
            <Route path="/operacao/saude-ocupacional/pcmso/:id" element={<PcmsoDetalhePage />} />
            <Route path="/operacao/cipa" element={<CipaPage />} />
            <Route path="/operacao/cipa/eleicao/:id" element={<ProcessoEleitoralCipaDetalhePage />} />
            <Route path="/operacao/cipa/membro/:id" element={<MembroCipaDetalhePage />} />
            <Route path="/operacao/cipa/reuniao/:id" element={<ReuniaoCipaDetalhePage />} />
            <Route path="/operacao/cipa/sipat/:id" element={<EventoSipatDetalhePage />} />

            <Route path="/alertas" element={<AlertasPage />} />
            <Route path="/calendario" element={<CalendarioPage />} />

            {/* Pessoas, Não Conformidades e Acidentes & Incidentes são itens de 1º nível na sidebar
                (antes eram abas de Prevenção/Operação e de Melhoria Contínua, respectivamente —
                Melhoria Contínua foi removida). Cada página já é autossuficiente (título + abas
                internas próprias), mesmo padrão já usado por EpiPage. Riscos deixou de ser item
                próprio em 02/09 e virou aba de PgrRiscosPage — ver /prevencao/pgr. */}
            <Route path="/pessoas" element={<PessoasPage />} />
            <Route path="/pessoas/:id" element={<TrabalhadorDetalhePage />} />
            <Route path="/treinamentos" element={<TreinamentosPage />} />
            {/* Legado: Riscos era item de 1º nível na sidebar (até 02/09), virou aba de PGR/GRO. */}
            <Route path="/riscos" element={<Navigate to="/prevencao/pgr?aba=riscos" replace />} />
            <Route path="/nao-conformidades" element={<NaoConformidadesPage />} />
            <Route path="/nao-conformidades/:id" element={<NaoConformidadeDetalhePage />} />
            <Route path="/acidentes" element={<AcidentesPage />} />
            <Route path="/acidentes/:id" element={<AcidenteDetalhePage />} />
            <Route path="/requisitos-legais" element={<RequisitosLegaisPage />} />

            <Route path="/epi" element={<EpiPage />} />
            <Route path="/epi/:id/assinar" element={<AssinarEntregaEpiPage />} />
            <Route path="/treinamentos/:id/assinar" element={<AssinarTreinamentoPage />} />
            <Route path="/administracao" element={<AdministracaoPage />} />

            {/* Legado: "Treinamentos" era placeholder (EmConstrucaoPage) na reorganização de
                sidebar de 31/08 — Treinamentos virou módulo próprio em 02/09 (ver TreinamentosPage). */}
            <Route path="/gestao-sst/treinamentos" element={<Navigate to="/treinamentos" replace />} />
            {/* Reorganização de sidebar em grupos (2026-08-31, pedido do usuário) — item que
                ganhou lugar fixo no menu mas ainda não tem tela/dado próprio no sistema. */}
            <Route
              path="/gestao-sst/documentos"
              element={
                <EmConstrucaoPage
                  titulo="Documentos & Procedimentos"
                  descricao="O módulo de Gestão Documental foi removido do sistema em 28/08 (junto com a Matriz Legal antiga). Esse item está reservado no menu, mas precisa ser reconstruído do zero."
                />
              }
            />
            {/* Redirecionamentos legados: caminhos antigos (pré-consolidação de 24/08) apontando
                para as novas sub-rotas dentro dos módulos-pilar — preserva links/favoritos antigos. */}
            <Route path="/prevencao/riscos" element={<Navigate to="/prevencao/pgr?aba=riscos" replace />} />
            <Route path="/pgr" element={<Navigate to="/prevencao/pgr" replace />} />
            <Route path="/pgr/:id" element={<RedirecionarComId para={(id) => `/prevencao/pgr/${id}`} />} />
            <Route path="/pcmso" element={<Navigate to="/operacao/saude-ocupacional" replace />} />
            <Route path="/pcmso/:id" element={<RedirecionarComId para={(id) => `/operacao/saude-ocupacional/pcmso/${id}`} />} />
            <Route path="/inspecoes" element={<Navigate to="/prevencao/inspecoes" replace />} />
            <Route
              path="/inspecoes/:id"
              element={<RedirecionarComId para={(id) => `/prevencao/inspecoes/${id}`} />}
            />
            <Route path="/dds" element={<Navigate to="/prevencao/dds" replace />} />
            <Route path="/dds/:id" element={<RedirecionarComId para={(id) => `/prevencao/dds/dia/${id}`} />} />

            <Route path="/obras" element={<Navigate to="/administracao" replace />} />
            {/* Legado: Obras era aba de Operação (até 01/09), virou aba de Administração. */}
            <Route path="/operacao/obras" element={<Navigate to="/administracao" replace />} />
            {/* Legado: Pessoas era aba de Operação (até 28/08), virou item de 1º nível. */}
            <Route path="/operacao/pessoas" element={<Navigate to="/pessoas" replace />} />
            <Route path="/operacao/pessoas/:id" element={<RedirecionarComId para={(id) => `/pessoas/${id}`} />} />
            <Route path="/apr" element={<Navigate to="/operacao/apr" replace />} />
            <Route path="/apr/:id" element={<RedirecionarComId para={(id) => `/operacao/apr/${id}`} />} />
            <Route path="/pt" element={<Navigate to="/operacao/pt" replace />} />
            <Route path="/pt/:id" element={<RedirecionarComId para={(id) => `/operacao/pt/${id}`} />} />
            <Route path="/identificacao" element={<Navigate to="/operacao/identificacao" replace />} />
            {/* Legado: Saúde Ocupacional era item de 1º nível na sidebar (até 28/08), virou aba
                de Operação. */}
            <Route path="/saude-ocupacional" element={<Navigate to="/operacao/saude-ocupacional" replace />} />
            <Route
              path="/saude-ocupacional/pcmso/:id"
              element={<RedirecionarComId para={(id) => `/operacao/saude-ocupacional/pcmso/${id}`} />}
            />

            <Route path="/naoconformidades" element={<Navigate to="/nao-conformidades" replace />} />
            <Route
              path="/naoconformidades/:id"
              element={<RedirecionarComId para={(id) => `/nao-conformidades/${id}`} />}
            />

            {/* Módulo Melhoria Contínua removido (24/26/08) — Não Conformidades e Acidentes &
                Incidentes viraram itens de 1º nível. Redirecionamentos preservam links antigos. */}
            <Route path="/melhoria" element={<Navigate to="/nao-conformidades" replace />} />
            <Route path="/melhoria/nao-conformidades" element={<Navigate to="/nao-conformidades" replace />} />
            <Route
              path="/melhoria/nao-conformidades/:id"
              element={<RedirecionarComId para={(id) => `/nao-conformidades/${id}`} />}
            />
            <Route path="/melhoria/acidentes" element={<Navigate to="/acidentes" replace />} />
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
