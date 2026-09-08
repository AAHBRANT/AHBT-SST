import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { DimensionamentoCipaTab } from './DimensionamentoCipaTab';
import { ProcessoEleitoralCipaTab } from './ProcessoEleitoralCipaTab';
import { MembrosCipaTab } from './MembrosCipaTab';
import { ReunioesCipaTab } from './ReunioesCipaTab';
import { InspecoesCipaTab } from './InspecoesCipaTab';
import { SipatTab } from './SipatTab';

const ABAS_CIPA = ['dimensionamento', 'eleicao', 'membros', 'reunioes', 'inspecoes', 'sipat'] as const;
type AbaCipa = (typeof ABAS_CIPA)[number];

// Módulo CIPA (NR-5, requisito do usuário, 31/08/2026), item de 1º nível próprio na sidebar (saiu
// do pilar Operação em 02/09 — cada item da sidebar deve abrir só o que é dele, ver AppShell.tsx).
// Disclosure completo (dimensionamento sempre manual, apuração manual sem urna digital, PGR/GRO
// integrado via botão "Gerar Não Conformidade" e não automaticamente) em Domain/Entidades/Cipa/Cipa.cs.
// Onda 2 Task 5 (camada ui/): mesmo padrão de EpiPage.tsx (piloto 1) — a aba vive na URL (?aba=), então
// voltar e F5 preservam a aba; nivel muda entre pilar/modulo conforme a página é aberta com título
// próprio (mostrarTitulo) ou embutida como seção de outra página-pilar (hoje só o segundo caso, dentro
// de OperacaoPage — ver comentário acima sobre a saída do pilar Operação ainda não refletida no roteiro).
export function CipaPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaCipa>('aba', ABAS_CIPA, 'dimensionamento');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="CIPA" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções da CIPA"
        abas={[
          { valor: 'dimensionamento', rotulo: 'Dimensionamento' },
          { valor: 'eleicao', rotulo: 'Processo Eleitoral' },
          { valor: 'membros', rotulo: 'Membros' },
          { valor: 'reunioes', rotulo: 'Reuniões' },
          { valor: 'inspecoes', rotulo: 'Inspeções' },
          { valor: 'sipat', rotulo: 'SIPAT' },
        ]}
      />

      {aba === 'dimensionamento' && <DimensionamentoCipaTab />}
      {aba === 'eleicao' && <ProcessoEleitoralCipaTab />}
      {aba === 'membros' && <MembrosCipaTab />}
      {aba === 'reunioes' && <ReunioesCipaTab />}
      {aba === 'inspecoes' && <InspecoesCipaTab />}
      {aba === 'sipat' && <SipatTab />}
    </div>
  );
}
