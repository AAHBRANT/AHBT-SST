import { FluentProvider } from '@fluentui/react-components';
import { HashRouter, Navigate, Outlet, Route, Routes, useParams } from 'react-router-dom';
import { aahbrantTheme } from './theme';
import { AppShell } from './layout/AppShell';
import { PillarLayout } from './layout/PillarLayout';
import { DashboardPage } from './pages/DashboardPage';
import { PessoasPage } from './pages/pessoas/PessoasPage';
import { TrabalhadorDetalhePage } from './pages/pessoas/TrabalhadorDetalhePage';
import { RiscosPage } from './pages/riscos/RiscosPage';
import { PgrsPage } from './pages/pgr/PgrsPage';
import { PgrDetalhePage } from './pages/pgr/PgrDetalhePage';
import { AprsPage } from './pages/apr/AprsPage';
import { AprDetalhePage } from './pages/apr/AprDetalhePage';
import { PermissoesTrabalhoPage } from './pages/pt/PermissoesTrabalhoPage';
import { PermissaoTrabalhoDetalhePage } from './pages/pt/PermissaoTrabalhoDetalhePage';
import { AssinarPtPage } from './pages/pt/AssinarPtPage';
import { InspecoesPage } from './pages/inspecoes/InspecoesPage';
import { InspecaoDetalhePage } from './pages/inspecoes/InspecaoDetalhePage';
import { IdentificacaoPage } from './pages/identificacao/IdentificacaoPage';
import { AreaPublicaPage } from './pages/identificacao/AreaPublicaPage';
import { ValidarDocumentoPage } from './pages/validacao/ValidarDocumentoPage';
import { AtivosPage } from './pages/ativos/AtivosPage';
import { AdministracaoPage } from './pages/administracao/AdministracaoPage';
import { NaoConformidadesPage } from './pages/naoconformidades/NaoConformidadesPage';
import { RequisitosLegaisPage } from './pages/requisitoslegais/RequisitosLegaisPage';
import { NaoConformidadeDetalhePage } from './pages/naoconformidades/NaoConformidadeDetalhePage';
import { AlertasPage } from './pages/alertas/AlertasPage';
import { CalendarioPage } from './pages/calendario/CalendarioPage';
import { AcidentesPage } from './pages/acidentes/AcidentesPage';
import { AcidenteDetalhePage } from './pages/acidentes/AcidenteDetalhePage';
import { DdsSemanalPage } from './pages/dds/DdsSemanalPage';
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

            {/* Módulo Procedimentos & Planos (ex-Prevenção): PGR, Inspeções, DDS (Riscos virou
                item de 1º nível — ver /riscos; PCMSO faz parte de Saúde Ocupacional, aba de
                Operação — ver /operacao/saude-ocupacional) */}
            <Route
              path="/prevencao"
              element={
                <PillarLayout
                  titulo="Procedimentos & Planos"
                  prefixo="prevencao"
                  abas={[
                    { valor: 'pgr', rotulo: 'PGR' },
                    { valor: 'inspecoes', rotulo: 'Inspeções' },
                    { valor: 'dds', rotulo: 'DDS' },
                  ]}
                />
              }
            >
              <Route index element={<Navigate to="pgr" replace />} />
              <Route path="pgr" element={<PgrsPage />} />
              <Route path="pgr/:id" element={<PgrDetalhePage />} />
              <Route path="inspecoes" element={<InspecoesPage />} />
              <Route path="inspecoes/:id" element={<InspecaoDetalhePage />} />
              <Route path="dds" element={<DdsSemanalPage />} />
              <Route path="dds/semana/:id" element={<DdsSemanalDetalhePage />} />
              <Route path="dds/dia/:id" element={<DdsDetalhePage />} />
              <Route path="dds/dia/:id/assinar" element={<AssinarDdsPage />} />
            </Route>
            {/* Legado: /prevencao/pcmso apontava pro PCMSO antigo (descontinuado em 28/08 —
                ver ONBOARDING.md) — redireciona pro módulo Saúde Ocupacional atual. */}
            <Route path="/prevencao/pcmso" element={<Navigate to="/operacao/saude-ocupacional" replace />} />
            <Route path="/prevencao/pcmso/:id" element={<RedirecionarComId para={(id) => `/operacao/saude-ocupacional/pcmso/${id}`} />} />

            {/* Módulo Operação: APR, PT, Identificação & Acesso, Saúde Ocupacional (Pessoas virou
                item de 1º nível na sidebar — ver abaixo; Obras saiu daqui e virou aba de
                Administração — pedido do usuário, 01/09) */}
            <Route
              path="/operacao"
              element={
                <PillarLayout
                  titulo="Operação"
                  prefixo="operacao"
                  abas={[
                    { valor: 'apr', rotulo: 'APR' },
                    { valor: 'pt', rotulo: 'PT (Permissão de Trabalho)' },
                    { valor: 'identificacao', rotulo: 'Identificação & Acesso' },
                    { valor: 'ativos', rotulo: 'Ativos (Extintores & Equipamentos)' },
                    { valor: 'saude-ocupacional', rotulo: 'Saúde Ocupacional' },
                    { valor: 'cipa', rotulo: 'CIPA' },
                  ]}
                />
              }
            >
              <Route index element={<Navigate to="apr" replace />} />
              <Route path="apr" element={<AprsPage />} />
              <Route path="apr/:id" element={<AprDetalhePage />} />
              <Route path="pt" element={<PermissoesTrabalhoPage />} />
              <Route path="pt/:id" element={<PermissaoTrabalhoDetalhePage />} />
              <Route path="pt/:id/assinar" element={<AssinarPtPage />} />
              <Route path="identificacao" element={<IdentificacaoPage />} />
              <Route path="ativos" element={<AtivosPage />} />
              <Route path="saude-ocupacional" element={<SaudeOcupacionalPage />} />
              <Route path="saude-ocupacional/pcmso/:id" element={<PcmsoDetalhePage />} />
              <Route path="cipa" element={<CipaPage />} />
              <Route path="cipa/eleicao/:id" element={<ProcessoEleitoralCipaDetalhePage />} />
              <Route path="cipa/membro/:id" element={<MembroCipaDetalhePage />} />
              <Route path="cipa/reuniao/:id" element={<ReuniaoCipaDetalhePage />} />
              <Route path="cipa/sipat/:id" element={<EventoSipatDetalhePage />} />
            </Route>

            <Route path="/alertas" element={<AlertasPage />} />
            <Route path="/calendario" element={<CalendarioPage />} />

            {/* Riscos, Pessoas, Não Conformidades e Acidentes & Incidentes viraram itens de 1º
                nível na sidebar (antes eram abas de Prevenção/Operação e de Melhoria Contínua,
                respectivamente — Melhoria Contínua foi removida). Cada página já é autossuficiente
                (título + abas internas próprias), mesmo padrão já usado por EpiPage. */}
            <Route path="/pessoas" element={<PessoasPage />} />
            <Route path="/pessoas/:id" element={<TrabalhadorDetalhePage />} />
            <Route path="/riscos" element={<RiscosPage />} />
            <Route path="/nao-conformidades" element={<NaoConformidadesPage />} />
            <Route path="/nao-conformidades/:id" element={<NaoConformidadeDetalhePage />} />
            <Route path="/acidentes" element={<AcidentesPage />} />
            <Route path="/acidentes/:id" element={<AcidenteDetalhePage />} />
            <Route path="/requisitos-legais" element={<RequisitosLegaisPage />} />

            <Route path="/epi" element={<EpiPage />} />
            <Route path="/epi/:id/assinar" element={<AssinarEntregaEpiPage />} />
            <Route path="/administracao" element={<AdministracaoPage />} />

            {/* Reorganização de sidebar em grupos (2026-08-31, pedido do usuário) — 3 itens que
                ganharam lugar fixo no menu mas ainda não têm tela/dado próprio no sistema. */}
            <Route
              path="/gestao-sst/treinamentos"
              element={
                <EmConstrucaoPage
                  titulo="Treinamentos"
                  descricao="Ainda não existe uma tela dedicada de gestão de treinamentos — hoje o indicador de Treinamentos vencidos no Dashboard usa o cadastro de Treinamento por trabalhador (ver perfil de cada trabalhador em Pessoas)."
                />
              }
            />
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
            <Route path="/prevencao/riscos" element={<Navigate to="/riscos" replace />} />
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
