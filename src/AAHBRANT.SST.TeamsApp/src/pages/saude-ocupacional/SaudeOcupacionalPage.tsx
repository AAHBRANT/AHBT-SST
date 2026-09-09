import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { AsosTab } from './AsosTab';
import { PcmsoTab } from './PcmsoTab';
import { ExamesComplementaresTab } from './ExamesComplementaresTab';
import { AptidoesTab } from './AptidoesTab';

type AbaSaudeOcupacional = 'aso' | 'pcmso' | 'exames' | 'aptidoes';

const ABAS_VALIDAS: AbaSaudeOcupacional[] = ['aso', 'pcmso', 'exames', 'aptidoes'];

// Módulo "Saúde Ocupacional" (PR-SST-003), item de 1º nível próprio na sidebar (saiu do pilar
// Operação em 02/09 — cada item da sidebar deve abrir só o que é dele, ver AppShell.tsx). ASO/
// Exames Complementares/Aptidões têm abas próprias aqui dentro porque são dado operacional/
// cross-worker; a versão somente-leitura por trabalhador continua em PerfilGeralTab.tsx (aba
// "Geral & ASO").
//
// Suporta abrir já numa aba específica via URL (?aba=pcmso) — usado pelos itens "PCMSO" (grupo
// Gestão de SST) e "ASO & Exames" (grupo Pessoas) do menu lateral, que apontam pra essa mesma tela.
// Onda 2 Task 12 (camada ui/): aba sincronizada com a URL nos dois sentidos via useAbaNaUrl — o
// prop `abaInicial` (quando informado por quem chama) vira o `padrao` do hook, preservando a mesma
// precedência de antes (URL válida > abaInicial > 'aso').
export function SaudeOcupacionalPage({
  abaInicial: abaInicialProp,
  mostrarTitulo = true,
}: { abaInicial?: AbaSaudeOcupacional; mostrarTitulo?: boolean } = {}) {
  const [aba, setAba] = useAbaNaUrl<AbaSaudeOcupacional>('aba', ABAS_VALIDAS, abaInicialProp ?? 'aso');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Saúde Ocupacional" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Saúde Ocupacional"
        abas={[
          { valor: 'pcmso', rotulo: 'PCMSO' },
          { valor: 'aso', rotulo: 'ASO' },
          { valor: 'exames', rotulo: 'Exames Complementares' },
          { valor: 'aptidoes', rotulo: 'Aptidões' },
        ]}
      />

      {aba === 'pcmso' && <PcmsoTab />}
      {aba === 'aso' && <AsosTab />}
      {aba === 'exames' && <ExamesComplementaresTab />}
      {aba === 'aptidoes' && <AptidoesTab />}
    </div>
  );
}
