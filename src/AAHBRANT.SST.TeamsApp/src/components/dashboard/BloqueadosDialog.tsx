import { useMemo, useState } from 'react';
import { makeStyles } from '@fluentui/react-components';
import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Legenda, StatusChip } from '@ui';
import type { LiberacaoTrabalho } from '../../lib/api';

const useStyles = makeStyles({
  filtros: { display: 'flex', gap: '6px', flexWrap: 'wrap', marginBottom: '8px' },
  lista: { display: 'flex', flexDirection: 'column', maxHeight: '60vh', overflowY: 'auto' },
  linha: {
    display: 'flex', flexDirection: 'column', gap: '6px', padding: '10px 4px', textAlign: 'left',
    backgroundColor: 'transparent', border: 'none', borderBottom: '1px solid var(--colorNeutralStroke2)',
    cursor: 'pointer', color: 'inherit', font: 'inherit',
    ':hover': { backgroundColor: 'var(--colorNeutralBackground1Hover)' },
  },
  cabecalho: { display: 'flex', justifyContent: 'space-between', gap: '12px', alignItems: 'baseline' },
  nome: { fontWeight: 600 },
  funcao: { color: 'var(--colorNeutralForeground3)', fontSize: '13px' },
  abrir: { color: 'var(--colorBrandForeground1)', fontSize: '13px', fontWeight: 600, whiteSpace: 'nowrap' },
  motivos: { display: 'flex', flexWrap: 'wrap', gap: '6px' },
});

type Filtro = 'todos' | 'Aso' | 'Treinamento';

interface BloqueadosDialogProps {
  aberto: boolean;
  liberacao: LiberacaoTrabalho | null;
  aoFechar: () => void;
  aoAbrirPessoa: (trabalhadorId: string) => void;
}

// Quem está bloqueado e por quê: mesma regra e mesmo recorte de obra do número do card.
export function BloqueadosDialog({ aberto, liberacao, aoFechar, aoAbrirPessoa }: BloqueadosDialogProps) {
  const estilos = useStyles();
  const [filtro, setFiltro] = useState<Filtro>('todos');
  const pessoas = liberacao?.pessoasBloqueadas ?? [];

  const contagem = useMemo(() => ({
    Aso: pessoas.filter((p) => p.motivos.some((m) => m.tipo === 'Aso')).length,
    Treinamento: pessoas.filter((p) => p.motivos.some((m) => m.tipo === 'Treinamento')).length,
  }), [pessoas]);

  const visiveis = filtro === 'todos' ? pessoas : pessoas.filter((p) => p.motivos.some((m) => m.tipo === filtro));

  return (
    <Dialog open={aberto} onOpenChange={(_, dados) => { if (!dados.open) aoFechar(); }}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Quem está bloqueado ({pessoas.length})</DialogTitle>
          <DialogContent>
            <div className={estilos.filtros} role="group" aria-label="Filtrar por motivo">
              <Button size="small" appearance={filtro === 'todos' ? 'primary' : 'outline'} onClick={() => setFiltro('todos')}>
                Todos ({pessoas.length})
              </Button>
              <Button size="small" appearance={filtro === 'Aso' ? 'primary' : 'outline'} onClick={() => setFiltro('Aso')}>
                ASO ({contagem.Aso})
              </Button>
              <Button size="small" appearance={filtro === 'Treinamento' ? 'primary' : 'outline'} onClick={() => setFiltro('Treinamento')}>
                Treinamento ({contagem.Treinamento})
              </Button>
            </div>
            {visiveis.length === 0 && <Legenda>Ninguém bloqueado para este filtro.</Legenda>}
            <div className={estilos.lista}>
              {visiveis.map((pessoa) => (
                <button key={pessoa.trabalhadorId} type="button" className={estilos.linha} onClick={() => aoAbrirPessoa(pessoa.trabalhadorId)}>
                  <span className={estilos.cabecalho}>
                    <span>
                      <span className={estilos.nome}>{pessoa.nome}</span>
                      <span className={estilos.funcao}>
                        {' · '}{[pessoa.funcao, pessoa.obra].filter(Boolean).join(' · ')}
                      </span>
                    </span>
                    <span className={estilos.abrir}>Abrir perfil ›</span>
                  </span>
                  <span className={estilos.motivos}>
                    {pessoa.motivos.map((motivo) => (
                      <StatusChip key={motivo.texto} tom={motivo.tipo === 'Aso' ? 'alerta' : 'atencao'}>
                        {motivo.texto}
                      </StatusChip>
                    ))}
                  </span>
                </button>
              ))}
            </div>
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={aoFechar}>Fechar</Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
