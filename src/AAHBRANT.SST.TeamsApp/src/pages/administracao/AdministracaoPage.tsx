import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { ObrasPage } from '../ObrasPage';
import { ControleAcessoTab } from './ControleAcessoTab';
import { TrilhaAuditoriaTab } from './TrilhaAuditoriaTab';
import { PainelAssinaturasTab } from './PainelAssinaturasTab';
import { IntegracaoGrhTab } from './IntegracaoGrhTab';
import { IntegracaoGsupriTab } from './IntegracaoGsupriTab';
import { TagsIdentificacaoTab } from '../identificacao/TagsIdentificacaoTab';
import { LeitorNfcTab } from '../identificacao/LeitorNfcTab';
import { NovidadesTab } from './NovidadesTab';
import { LeitoresDigitalTab } from './LeitoresDigitalTab';
import { DestinatariosRelatoriosTab } from './DestinatariosRelatoriosTab';
import { PendenciasAutomaticasTab } from './PendenciasAutomaticasTab';

const ABAS_PILAR = ['obras', 'acesso', 'integracoes', 'dispositivos', 'notificacoes', 'novidades'] as const;
type AbaPilarAdministracao = (typeof ABAS_PILAR)[number];

type SubAcesso = 'acesso' | 'auditoria' | 'assinaturas';
type SubIntegracoes = 'grh' | 'gsupri';
type SubDispositivos = 'tags' | 'leitor' | 'leitoresDigital';
type SubNotificacoes = 'destinatarios' | 'pendencias';

const MAPA_ABA_LEGADA: Record<string, { aba: AbaPilarAdministracao; sub: string }> = {
  auditoria: { aba: 'acesso', sub: 'auditoria' },
  assinaturas: { aba: 'acesso', sub: 'assinaturas' },
  grh: { aba: 'integracoes', sub: 'grh' },
  gsupri: { aba: 'integracoes', sub: 'gsupri' },
  tags: { aba: 'dispositivos', sub: 'tags' },
  leitor: { aba: 'dispositivos', sub: 'leitor' },
  leitoresDigital: { aba: 'dispositivos', sub: 'leitoresDigital' },
  destinatarios: { aba: 'notificacoes', sub: 'destinatarios' },
  pendencias: { aba: 'notificacoes', sub: 'pendencias' },
};

// Consolidação das 12 abas horizontais em 6 grupos temáticos de 1º nível + sub-abas internas,
// mantendo compatibilidade total com links diretos antigos (?aba=auditoria, ?aba=grh, etc.).
export function AdministracaoPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const abaBruta = searchParams.get('aba') ?? '';
  const subNaUrl = searchParams.get('sub') ?? '';

  useEffect(() => {
    const mapeada = MAPA_ABA_LEGADA[abaBruta];
    if (mapeada) {
      setSearchParams(
        (p) => {
          const novo = new URLSearchParams(p);
          novo.set('aba', mapeada.aba);
          novo.set('sub', mapeada.sub);
          return novo;
        },
        { replace: true },
      );
    }
  }, [abaBruta, setSearchParams]);

  const [aba, setAba] = useAbaNaUrl<AbaPilarAdministracao>('aba', ABAS_PILAR, 'obras');

  const mudarSub = (valor: string) =>
    setSearchParams(
      (p) => {
        const novo = new URLSearchParams(p);
        novo.set('sub', valor);
        return novo;
      },
      { replace: true },
    );

  const subAcesso: SubAcesso =
    subNaUrl === 'auditoria' || subNaUrl === 'assinaturas' ? subNaUrl : 'acesso';
  const subIntegracoes: SubIntegracoes = subNaUrl === 'gsupri' ? 'gsupri' : 'grh';
  const subDispositivos: SubDispositivos =
    subNaUrl === 'leitor' || subNaUrl === 'leitoresDigital' ? subNaUrl : 'tags';
  const subNotificacoes: SubNotificacoes = subNaUrl === 'pendencias' ? 'pendencias' : 'destinatarios';

  return (
    <div>
      <PageHeader titulo="Administração" />

      <Abas
        nivel="pilar"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Administração"
        abas={[
          { valor: 'obras', rotulo: 'Obras' },
          { valor: 'acesso', rotulo: 'Acesso & Auditoria' },
          { valor: 'integracoes', rotulo: 'Integrações (G-RH / G-SUPRI)' },
          { valor: 'dispositivos', rotulo: 'Tags & Biometria' },
          { valor: 'notificacoes', rotulo: 'Relatórios & Pendências' },
          { valor: 'novidades', rotulo: 'Novidades da versão' },
        ]}
      />

      {aba === 'obras' && <ObrasPage />}

      {aba === 'acesso' && (
        <>
          <Abas
            nivel="interno"
            valor={subAcesso}
            aoMudar={mudarSub}
            aria-label="Acesso e Auditoria"
            abas={[
              { valor: 'acesso', rotulo: 'Controle de Acesso' },
              { valor: 'auditoria', rotulo: 'Trilha de Auditoria' },
              { valor: 'assinaturas', rotulo: 'Assinaturas' },
            ]}
          />
          {subAcesso === 'acesso' && <ControleAcessoTab />}
          {subAcesso === 'auditoria' && <TrilhaAuditoriaTab />}
          {subAcesso === 'assinaturas' && <PainelAssinaturasTab />}
        </>
      )}

      {aba === 'integracoes' && (
        <>
          <Abas
            nivel="interno"
            valor={subIntegracoes}
            aoMudar={mudarSub}
            aria-label="Integrações externas"
            abas={[
              { valor: 'grh', rotulo: 'Integração G-RH' },
              { valor: 'gsupri', rotulo: 'Integração G-SUPRI' },
            ]}
          />
          {subIntegracoes === 'grh' && <IntegracaoGrhTab />}
          {subIntegracoes === 'gsupri' && <IntegracaoGsupriTab />}
        </>
      )}

      {aba === 'dispositivos' && (
        <>
          <Abas
            nivel="interno"
            valor={subDispositivos}
            aoMudar={mudarSub}
            aria-label="Tags e Leitores Biométricos"
            abas={[
              { valor: 'tags', rotulo: 'Tags (NFC/QR)' },
              { valor: 'leitor', rotulo: 'Leitor / Teste NFC' },
              { valor: 'leitoresDigital', rotulo: 'Leitores de digital' },
            ]}
          />
          {subDispositivos === 'tags' && <TagsIdentificacaoTab />}
          {subDispositivos === 'leitor' && <LeitorNfcTab />}
          {subDispositivos === 'leitoresDigital' && <LeitoresDigitalTab />}
        </>
      )}

      {aba === 'notificacoes' && (
        <>
          <Abas
            nivel="interno"
            valor={subNotificacoes}
            aoMudar={mudarSub}
            aria-label="Relatórios e Pendências Automáticas"
            abas={[
              { valor: 'destinatarios', rotulo: 'Destinatários dos relatórios' },
              { valor: 'pendencias', rotulo: 'Pendências automáticas' },
            ]}
          />
          {subNotificacoes === 'destinatarios' && <DestinatariosRelatoriosTab />}
          {subNotificacoes === 'pendencias' && <PendenciasAutomaticasTab />}
        </>
      )}

      {aba === 'novidades' && <NovidadesTab />}
    </div>
  );
}
