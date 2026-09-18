import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { EmpresasTab } from './EmpresasTab';
import { PessoasTab } from './PessoasTab';
import { PendenciasTab } from './PendenciasTab';

// Módulo Terceirizado (docs/superpowers/specs/2026-09-18-modulo-terceirizado-design.md) — item
// próprio na sidebar (decisão explícita do usuário, quebra a convenção de módulo-como-aba-de-pilar
// usada pelos demais). PessoasTab/PendenciasTab entram no Task 15.
const ABAS = ['empresas', 'pessoas', 'pendencias'] as const;
type AbaTerceirizado = (typeof ABAS)[number];

export function TerceirizadoPage() {
  const [aba, setAba] = useAbaNaUrl<AbaTerceirizado>('aba', ABAS, 'empresas');

  return (
    <div>
      <PageHeader titulo="Terceirizado" />

      <Abas
        nivel="pilar"
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Terceirizado"
        abas={[
          { valor: 'empresas', rotulo: 'Empresas' },
          { valor: 'pessoas', rotulo: 'Pessoas' },
          { valor: 'pendencias', rotulo: 'Pendências' },
        ]}
      />

      {aba === 'empresas' && <EmpresasTab />}
      {aba === 'pessoas' && <PessoasTab />}
      {aba === 'pendencias' && <PendenciasTab />}
    </div>
  );
}
