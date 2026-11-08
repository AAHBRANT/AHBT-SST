import { useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Abas, PageHeader, useAbaNaUrl } from '@ui';
import { CursosTreinamentoTab } from '../pessoas/CursosTreinamentoTab';
import { MatrizTreinamentoTab } from '../pessoas/MatrizTreinamentoTab';
import { TurmasTab } from './TurmasTab';
import { CursosObrigatoriosObraTab } from './CursosObrigatoriosObraTab';
import { RegrasCursoObrigatorioTab } from './RegrasCursoObrigatorioTab';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';

// 'certificados' saiu em 07/11/2026: certificado é dado de cada funcionário e fica no perfil (aba Treinamentos).
type AbaTreinamentos = 'turmas' | 'obrigatorios' | 'configuracoes';
type AbaConfiguracao = 'cursos' | 'regras' | 'matriz';

const ABAS: AbaTreinamentos[] = ['turmas', 'obrigatorios', 'configuracoes'];
const CONFIGURACOES: AbaConfiguracao[] = ['cursos', 'regras', 'matriz'];

// Item "Treinamentos" da sidebar (02/09): módulo próprio. Aba "Turmas" (04/09): turma com
// participantes pré-selecionados, presença biométrica e certificado individual automático.
// 07/11/2026: abas do dia a dia (Turmas, Certificados, Cursos obrigatórios) separadas da configuração
// feita de vez em quando (Catálogo de Cursos, Regras por NR e Matriz por Função), que foi para dentro
// de "Configurações". Links antigos (?aba=cursos|regras|matriz) abrem direto a sub-aba certa.
export function TreinamentosPage({ mostrarTitulo = true }: { mostrarTitulo?: boolean } = {}) {
  // Configurações (catálogo, regras por NR, matriz) só para o Admin (07/11/2026): vale para todas as obras.
  const souAdmin = useSouAdministrador();
  const [searchParams, setSearchParams] = useSearchParams();
  const abaAntiga = searchParams.get('aba');
  // Sub-aba de Configurações fica na URL (?sub=), para o link sobreviver ao recarregar.
  const subNaUrl = searchParams.get('sub');
  const config: AbaConfiguracao = CONFIGURACOES.includes(subNaUrl as AbaConfiguracao) ? (subNaUrl as AbaConfiguracao) : 'cursos';
  const setConfig = (valor: AbaConfiguracao) =>
    setSearchParams(
      (p) => {
        const novo = new URLSearchParams(p);
        novo.set('sub', valor);
        return novo;
      },
      { replace: true },
    );

  useEffect(() => {
    if (CONFIGURACOES.includes(abaAntiga as AbaConfiguracao)) {
      setSearchParams(
        (p) => {
          const novo = new URLSearchParams(p);
          novo.set('aba', 'configuracoes');
          novo.set('sub', abaAntiga as string);
          return novo;
        },
        { replace: true },
      );
    }
  }, [abaAntiga, setSearchParams]);

  const [aba, setAba] = useAbaNaUrl<AbaTreinamentos>('aba', ABAS, 'turmas');

  return (
    <div>
      {mostrarTitulo && <PageHeader titulo="Treinamentos" />}

      <Abas
        nivel={mostrarTitulo ? 'pilar' : 'modulo'}
        valor={aba}
        aoMudar={setAba}
        aria-label="Seções de Treinamentos"
        abas={[
          { valor: 'turmas', rotulo: 'Turmas' },
          { valor: 'obrigatorios', rotulo: 'Cursos obrigatórios por obra' },
          ...(souAdmin ? [{ valor: 'configuracoes' as const, rotulo: 'Configurações' }] : []),
        ]}
      />

      {aba === 'turmas' && <TurmasTab />}
      {aba === 'obrigatorios' && <CursosObrigatoriosObraTab />}
      {aba === 'configuracoes' && souAdmin && (
        <>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', margin: '4px 0 16px' }} role="tablist" aria-label="Configurações de treinamento">
            {(
              [
                ['cursos', 'Catálogo de Cursos'],
                ['regras', 'Regras por NR'],
                ['matriz', 'Matriz por Função (obra sem PGR)'],
              ] as [AbaConfiguracao, string][]
            ).map(([valor, rotulo]) => {
              const ativo = config === valor;
              return (
                <button
                  key={valor}
                  type="button"
                  role="tab"
                  aria-selected={ativo}
                  onClick={() => setConfig(valor)}
                  style={{
                    padding: '6px 16px',
                    borderRadius: 999,
                    border: `1px solid ${ativo ? '#670000' : '#d1d5db'}`,
                    background: ativo ? '#670000' : '#fff',
                    color: ativo ? '#fff' : '#374151',
                    fontWeight: 600,
                    fontSize: 13,
                    cursor: 'pointer',
                    font: 'inherit',
                  }}
                >
                  {rotulo}
                </button>
              );
            })}
          </div>
          {config === 'cursos' && <CursosTreinamentoTab />}
          {config === 'regras' && <RegrasCursoObrigatorioTab />}
          {config === 'matriz' && <MatrizTreinamentoTab />}
        </>
      )}
    </div>
  );
}
