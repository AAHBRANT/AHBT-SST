import { useEffect, useState } from 'react';
import { Button, Input, StatusChip, Text } from '@ui';
import { api, type FuncaoGerada, type ResultadoBuscaCbo } from '../../lib/api';

// CBO da função pela tabela oficial da CBO 2002 (mockup aprovado em 29/10/2026). Só aceita código escolhido na
// busca: não dá para digitar um código que não existe. O que veio do cadastro fora da tabela aparece marcado.
export function formatarCbo(codigo: string): string {
  return codigo.length === 6 ? `${codigo.slice(0, 4)}-${codigo.slice(4)}` : codigo;
}

export function situacaoCbo(f: FuncaoGerada): 'sugerido' | 'valido' | 'fora' | 'vazio' {
  if (!f.cbo) return 'vazio';
  if (!f.cboTitulo) return 'fora';
  return f.cboSugeridoPelaIa ? 'sugerido' : 'valido';
}

export function ChipCbo({ f }: { f: FuncaoGerada }) {
  switch (situacaoCbo(f)) {
    case 'sugerido': return <StatusChip tom="atencao">CBO sugerido pela IA, confira</StatusChip>;
    case 'valido': return <StatusChip tom="ok">CBO da tabela oficial</StatusChip>;
    case 'fora': return <StatusChip tom="alerta">CBO fora da tabela oficial</StatusChip>;
    default: return <StatusChip tom="atencao">sem CBO</StatusChip>;
  }
}

export function CampoCbo({ f, editavel, aoEscolher }: {
  f: FuncaoGerada;
  editavel: boolean;
  aoEscolher: (r: ResultadoBuscaCbo) => void;
}) {
  const situacao = situacaoCbo(f);
  const [buscando, setBuscando] = useState(situacao === 'vazio' || situacao === 'fora');
  const [busca, setBusca] = useState('');
  const [resultados, setResultados] = useState<ResultadoBuscaCbo[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (busca.trim().length < 2) { setResultados([]); return; }
    const t = setTimeout(() => {
      api.cbo.buscar(busca.trim()).then((r) => { setResultados(r); setErro(null); }).catch(() => setErro('Não foi possível buscar na tabela da CBO.'));
    }, 300);
    return () => clearTimeout(t);
  }, [busca]);

  const escolhido = situacao === 'valido' || situacao === 'sugerido';
  return (
    <div style={{ display: 'grid', gap: 4 }}>
      <Text size={200}>CBO</Text>
      {escolhido && (
        <div style={{
          display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap', padding: '6px 8px', borderRadius: 4,
          border: '1px solid var(--colorNeutralStroke2)',
          background: situacao === 'sugerido' ? 'var(--colorPaletteYellowBackground1)' : undefined,
        }}>
          <Text weight="semibold" style={{ fontVariantNumeric: 'tabular-nums' }}>{formatarCbo(f.cbo!)}</Text>
          <Text style={{ flex: 1, minWidth: 0 }}>{f.cboTitulo}</Text>
          {editavel && <Button size="small" onClick={() => setBuscando((x) => !x)}>{buscando ? 'Fechar' : 'Trocar'}</Button>}
        </div>
      )}
      {situacao === 'fora' && (
        <Text size={200} style={{ color: 'var(--colorPaletteRedForeground1)' }}>
          O valor "{f.cbo}" não é um código da tabela oficial. Escolha o código certo abaixo.
        </Text>
      )}
      {editavel && buscando && (
        <>
          <Input value={busca} placeholder="Buscar por código ou nome da ocupação (ex.: servente, 7170)" aria-label="Buscar CBO"
            onChange={(_, d) => setBusca(d.value)} />
          {erro && <Text size={200}>{erro}</Text>}
          {resultados.length > 0 && (
            <div role="listbox" aria-label="Resultados da tabela CBO"
              style={{ border: '1px solid var(--colorNeutralStroke2)', borderRadius: 6, maxHeight: 240, overflowY: 'auto' }}>
              {resultados.map((r) => (
                <button key={r.codigo} type="button" role="option" aria-selected={f.cbo === r.codigo}
                  onClick={() => { aoEscolher(r); setBuscando(false); setBusca(''); setResultados([]); }}
                  style={{
                    display: 'flex', gap: 10, width: '100%', textAlign: 'left', padding: '7px 10px', cursor: 'pointer',
                    border: 0, borderTop: '1px solid var(--colorNeutralStroke2)', background: 'transparent', color: 'inherit', font: 'inherit',
                  }}>
                  <span style={{ fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>{r.codigoFormatado}</span>
                  <span style={{ flex: 1 }}>{r.titulo}{r.sinonimo ? <span style={{ opacity: 0.7 }}> · também chamado {r.sinonimo}</span> : null}</span>
                </button>
              ))}
            </div>
          )}
          {busca.trim().length >= 2 && resultados.length === 0 && !erro && <Text size={200}>Nenhuma ocupação encontrada.</Text>}
        </>
      )}
      {situacao === 'sugerido' && <Text size={200}>Se ninguém trocar, entra como a IA sugeriu.</Text>}
      {situacao === 'vazio' && !editavel && <Text size={200}>Sem CBO.</Text>}
    </div>
  );
}
