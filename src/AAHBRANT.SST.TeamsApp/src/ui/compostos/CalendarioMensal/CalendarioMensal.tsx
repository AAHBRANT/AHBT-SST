import type { ReactNode } from 'react';
import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components';
import { chaveDia, diasDoMesNaGrade } from '../../../lib/datasCalendario';
import { designTokens } from '../../tokens/tokens';

const useStyles = makeStyles({
  rolagem: { overflowX: 'auto', borderRadius: '8px', border: `1px solid ${tokens.colorNeutralStroke2}` },
  grade: { display: 'grid', gridTemplateColumns: 'repeat(7, minmax(0, 1fr))', gap: '1px', minWidth: '640px', backgroundColor: tokens.colorNeutralStroke2 },
  cabecalho: { padding: '10px', textAlign: 'center', fontWeight: 600, fontSize: '12px', backgroundColor: tokens.colorNeutralBackground3, color: tokens.colorNeutralForeground3 },
  dia: { fontFamily: 'inherit', border: 'none', padding: '8px', minHeight: '96px', display: 'flex', flexDirection: 'column', gap: '6px', textAlign: 'left', color: tokens.colorNeutralForeground1, backgroundColor: tokens.colorNeutralBackground1, cursor: 'pointer', ':hover': { backgroundColor: tokens.colorNeutralBackground1Hover }, ':focus-visible': { outline: `2px solid ${designTokens.colorPrimary}`, outlineOffset: '-2px' }, ':disabled': { cursor: 'default' } },
  fora: { backgroundColor: tokens.colorNeutralBackground2, color: tokens.colorNeutralForeground4 },
  selecionado: { boxShadow: `inset 0 0 0 2px ${designTokens.colorPrimary}` },
  numero: { display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: '23px', height: '23px', fontSize: '13px', fontWeight: 600, borderRadius: '50%' },
  hoje: { backgroundColor: designTokens.colorPrimary, color: tokens.colorNeutralForegroundStaticInverted },
});

/** Grade compartilhada pelo calendário operacional e pelo planejamento mensal de DDS. */
export function CalendarioMensal({ mes, selecionado, aoSelecionar, conteudoDia, primeiroDia = 0, desabilitarDia, rotuloDia }: {
  mes: Date;
  selecionado?: string;
  aoSelecionar: (chave: string) => void;
  conteudoDia: (chave: string, data: Date, outroMes: boolean) => ReactNode;
  primeiroDia?: 0 | 1;
  desabilitarDia?: (data: Date, outroMes: boolean) => boolean;
  rotuloDia?: (chave: string) => string;
}) {
  const e = useStyles();
  const semana = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
  const hoje = chaveDia(new Date());
  return <div className={e.rolagem}><div className={e.grade} role="group" aria-label="Calendário mensal">
    {Array.from({ length: 7 }, (_, i) => semana[(primeiroDia + i) % 7]).map(nome => <div key={nome} className={e.cabecalho}>{nome}</div>)}
    {diasDoMesNaGrade(mes, primeiroDia).map(data => {
      const chave = chaveDia(data);
      const outroMes = data.getMonth() !== mes.getMonth();
      return <button type="button" key={chave} data-dia={chave}
        className={mergeClasses(e.dia, outroMes && e.fora, selecionado === chave && e.selecionado)}
        aria-label={`${data.toLocaleDateString('pt-BR', { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' })}${rotuloDia ? `: ${rotuloDia(chave)}` : ''}`}
        aria-pressed={selecionado === chave} disabled={desabilitarDia?.(data, outroMes)} onClick={() => aoSelecionar(chave)}>
        <span className={mergeClasses(e.numero, chave === hoje && e.hoje)}>{data.getDate()}</span>
        {conteudoDia(chave, data, outroMes)}
      </button>;
    })}
  </div></div>;
}
