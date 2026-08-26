import { FluentProvider } from '@fluentui/react-components';
import { HashRouter, Navigate, Outlet, Route, Routes, useParams } from 'react-router-dom';
import { aahbrantTheme } from './theme';
import { AppShell } from './layout/AppShell';
import { PillarLayout } from './layout/PillarLayout';
import { DashboardPage } from './pages/DashboardPage';
import { ObrasPage } from './pages/ObrasPage';
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
import { NaoConformidadeDetalhePage } from './pages/naoconformidades/NaoConformidadeDetalhePage';
import { AlertasPage } from './pages/alertas/AlertasPage';
import { AcidentesPage } from './pages/acidentes/AcidentesPage';
import { AcidenteDetalhePage } from './pages/acidentes/AcidenteDetalhePage';
import { MatrizLegalPage } from './pages/matrizlegal/MatrizLegalPage';
import { RequisitoLegalDetalhePage } from './pages/matrizlegal/RequisitoLegalDetalhePage';
import { DocumentosGestaoPage } from './pages/gestaodocumental/DocumentosGestaoPage';
import { DocumentoGestaoDetalhePage } from './pages/gestaodocumental/DocumentoGestaoDetalhePage';
import { DdsPage } from './pages/dds/DdsPage';
import { DdsDetalhePage } from './pages/dds/DdsDetalhePage';
import { AssinarDdsPage } from './pages/dds/AssinarDdsPage';
import { EpiPage } from './pages/epi/EpiPage';
import { AssinarEntregaEpiPage } from './pages/epi/AssinarEntregaEpiPage';

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

            {/* Módulo Conformidade: Matriz Legal + Gestão Documental */}
            <Route
              path="/conformidade"
              element={
                <PillarLayout
                  titulo="Conformidade"
                  prefixo="conformidade"
                  abas={[
                    { valor: 'matriz-legal', rotulo: 'Matriz Legal' },
                    { valor: 'gestao-documental', rotulo: 'Gestão Documental' },
                  ]}
                />
              }
            >
              <Route index element={<Navigate to="matriz-legal" replace />} />
              <Route path="matriz-legal" element={<MatrizLegalPage />} />
              <Route path="matriz-legal/:id" element={<RequisitoLegalDetalhePage />} />
              <Route path="gestao-documental" element={<DocumentosGestaoPage />} />
              <Route path="gestao-documental/:id" element={<DocumentoGestaoDetalhePage />} />
            </Route>

            {/* Módulo Prevenção: Riscos, PGR, Inspeções, DDS */}
            <Route
              path="/prevencao"
              element={
                <PillarLayout
                  titulo="Prevenção"
                  prefixo="prevencao"
                  abas={[
                    { valor: 'riscos', rotulo: 'Riscos' },
                    { valor: 'pgr', rotulo: 'PGR' },
                    { valor: 'inspecoes', rotulo: 'Inspeções' },
                    { valor: 'dds', rotulo: 'DDS' },
                  ]}
                />
              }
            >
              <Route index element={<Navigate to="riscos" replace />} />
              <Route path="riscos" element={<RiscosPage />} />
              <Route path="pgr" element={<PgrsPage />} />
              <Route path="pgr/:id" element={<PgrDetalhePage />} />
              <Route path="inspecoes" element={<InspecoesPage />} />
              <Route path="inspecoes/:id" element={<InspecaoDetalhePage />} />
              <Route path="dds" element={<DdsPage />} />
              <Route path="dds/:id" element={<DdsDetalhePage />} />
              <Route path="dds/:id/assinar" element={<AssinarDdsPage />} />
            </Route>

            {/* Módulo Operação: Obras, Pessoas, APR, PT, Identificação & Acesso */}
            <Route
              path="/operacao"
              element={
                <PillarLayout
                  titulo="Operação"
                  prefixo="operacao"
                  abas={[
                    { valor: 'obras', rotulo: 'Obras' },
                    { valor: 'pessoas', rotulo: 'Pessoas' },
                    { valor: 'apr', rotulo: 'APR' },
                    { valor: 'pt', rotulo: 'PT (Permissão de Trabalho)' },
                    { valor: 'identificacao', rotulo: 'Identificação & Acesso' },
                    { valor: 'ativos', rotulo: 'Ativos (Extintores & Equipamentos)' },
                  ]}
                />
              }
            >
              <Route index element={<Navigate to="obras" replace />} />
              <Route path="obras" element={<ObrasPage />} />
              <Route path="pessoas" element={<PessoasPage />} />
              <Route path="pessoas/:id" element={<TrabalhadorDetalhePage />} />
              <Route path="apr" element={<AprsPage />} />
              <Route path="apr/:id" element={<AprDetalhePage />} />
              <Route path="pt" element={<PermissoesTrabalhoPage />} />
              <Route path="pt/:id" element={<PermissaoTrabalhoDetalhePage />} />
              <Route path="pt/:id/assinar" element={<AssinarPtPage />} />
              <Route path="identificacao" element={<IdentificacaoPage />} />
              <Route path="ativos" element={<AtivosPage />} />
            </Route>

            {/* Módulo Melhoria Contínua: Não Conformidades, Acidentes & Incidentes */}
            <Route
              path="/melhoria"
              element={
                <PillarLayout
                  titulo="Melhoria Contínua"
                  prefixo="melhoria"
                  abas={[
                    { valor: 'nao-conformidades', rotulo: 'Não Conformidades' },
                    { valor: 'acidentes', rotulo: 'Acidentes & Incidentes' },
                  ]}
                />
              }
            >
              <Route index element={<Navigate to="nao-conformidades" replace />} />
              <Route path="nao-conformidades" element={<NaoConformidadesPage />} />
              <Route path="nao-conformidades/:id" element={<NaoConformidadeDetalhePage />} />
              <Route path="acidentes" element={<AcidentesPage />} />
              <Route path="acidentes/:id" element={<AcidenteDetalhePage />} />
            </Route>

            <Route path="/alertas" element={<AlertasPage />} />
            <Route path="/epi" element={<EpiPage />} />
            <Route path="/epi/:id/assinar" element={<AssinarEntregaEpiPage />} />
            <Route path="/administracao" element={<AdministracaoPage />} />

            {/* Redirecionamentos legados: caminhos antigos (pré-consolidação de 24/08) apontando
                para as novas sub-rotas dentro dos módulos-pilar — preserva links/favoritos antigos. */}
            <Route path="/matriz-legal" element={<Navigate to="/conformidade/matriz-legal" replace />} />
            <Route
              path="/matriz-legal/:id"
              element={<RedirecionarComId para={(id) => `/conformidade/matriz-legal/${id}`} />}
            />
            <Route path="/gestao-documental" element={<Navigate to="/conformidade/gestao-documental" replace />} />
            <Route
              path="/gestao-documental/:id"
              element={<RedirecionarComId para={(id) => `/conformidade/gestao-documental/${id}`} />}
            />

            <Route path="/riscos" element={<Navigate to="/prevencao/riscos" replace />} />
            <Route path="/pgr" element={<Navigate to="/prevencao/pgr" replace />} />
            <Route path="/pgr/:id" element={<RedirecionarComId para={(id) => `/prevencao/pgr/${id}`} />} />
            <Route path="/inspecoes" element={<Navigate to="/prevencao/inspecoes" replace />} />
            <Route
              path="/inspecoes/:id"
              element={<RedirecionarComId para={(id) => `/prevencao/inspecoes/${id}`} />}
            />
            <Route path="/dds" element={<Navigate to="/prevencao/dds" replace />} />
            <Route path="/dds/:id" element={<RedirecionarComId para={(id) => `/prevencao/dds/${id}`} />} />

            <Route path="/obras" element={<Navigate to="/operacao/obras" replace />} />
            <Route path="/pessoas" element={<Navigate to="/operacao/pessoas" replace />} />
            <Route path="/pessoas/:id" element={<RedirecionarComId para={(id) => `/operacao/pessoas/${id}`} />} />
            <Route path="/apr" element={<Navigate to="/operacao/apr" replace />} />
            <Route path="/apr/:id" element={<RedirecionarComId para={(id) => `/operacao/apr/${id}`} />} />
            <Route path="/pt" element={<Navigate to="/operacao/pt" replace />} />
            <Route path="/pt/:id" element={<RedirecionarComId para={(id) => `/operacao/pt/${id}`} />} />
            <Route path="/identificacao" element={<Navigate to="/operacao/identificacao" replace />} />

            <Route path="/naoconformidades" element={<Navigate to="/melhoria/nao-conformidades" replace />} />
            <Route
              path="/naoconformidades/:id"
              element={<RedirecionarComId para={(id) => `/melhoria/nao-conformidades/${id}`} />}
            />
            <Route path="/acidentes" element={<Navigate to="/melhoria/acidentes" replace />} />
            <Route
              path="/acidentes/:id"
              element={<RedirecionarComId para={(id) => `/melhoria/acidentes/${id}`} />}
            />
          </Route>
        </Routes>
      </HashRouter>
    </FluentProvider>
  );
}

export default App;
