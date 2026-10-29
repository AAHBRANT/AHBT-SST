import { useEffect, useState } from 'react';
import { Menu, MenuItemRadio, MenuList, MenuPopover, MenuTrigger, makeStyles, mergeClasses } from '@fluentui/react-components';
import { Location16Regular, ChevronDown12Regular, Dismiss12Regular } from '@fluentui/react-icons';
import { api, type Obra } from '../lib/api';
import { useObraAtiva } from '../lib/ObraAtivaContext';
import { designTokens } from '@ui';

// Seletor "📍 Obra" do cabeçalho (28/10/2026): mostra em qual obra o usuário está, em qualquer
// página, e troca a obra do sistema inteiro. Ao lado do botão Atalhos.

const useStyles = makeStyles({
  pilula: {
    display: 'inline-flex',
    alignItems: 'center',
    gap: '6px',
    height: '32px',
    maxWidth: '240px',
    minWidth: 0,
    padding: '0 12px',
    borderRadius: '999px',
    border: '1px solid color-mix(in srgb, var(--sst-botao-marca) 45%, var(--sst-color-card-border))',
    backgroundColor: 'color-mix(in srgb, var(--sst-botao-marca) 9%, var(--sst-color-surface))',
    color: designTokens.colorNeutralDark,
    fontSize: '13px',
    cursor: 'pointer',
    transitionProperty: 'box-shadow',
    transitionDuration: '150ms',
    ':hover': { boxShadow: '0 0 0 3px color-mix(in srgb, var(--sst-botao-marca) 18%, transparent)' },
    ':focus-visible': { outline: '2px solid var(--sst-botao-marca)', outlineOffset: '2px' },
  },
  pulsa: {
    animationName: {
      '0%': { boxShadow: '0 0 0 0 color-mix(in srgb, var(--sst-botao-marca) 35%, transparent)' },
      '100%': { boxShadow: '0 0 0 8px transparent' },
    },
    animationDuration: '0.9s',
    animationIterationCount: 2,
  },
  icone: { color: 'var(--sst-acao-marca-tinta)', flexShrink: 0 },
  rotulo: { color: designTokens.colorNeutralMedium, fontSize: '12px' },
  nome: { fontWeight: 700, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  titulo: {
    padding: '6px 10px 4px',
    fontSize: '11px',
    fontWeight: 700,
    letterSpacing: '0.06em',
    textTransform: 'uppercase',
    color: designTokens.colorNeutralMedium,
  },
  rodape: {
    padding: '6px 10px 4px',
    fontSize: '11.5px',
    color: designTokens.colorNeutralMedium,
    borderTop: `1px solid ${designTokens.colorCardBorder}`,
    marginTop: '4px',
  },
  faixa: {
    display: 'flex',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: '8px',
    margin: '0 0 14px',
    padding: '8px 12px',
    borderRadius: '8px',
    fontSize: '13px',
    color: designTokens.colorNeutralDark,
    backgroundColor: 'color-mix(in srgb, var(--sst-botao-marca) 8%, var(--sst-color-surface))',
    border: '1px solid color-mix(in srgb, var(--sst-botao-marca) 30%, transparent)',
  },
  faixaBotao: {
    marginLeft: 'auto',
    display: 'inline-flex',
    alignItems: 'center',
    gap: '4px',
    border: 'none',
    backgroundColor: 'transparent',
    color: 'var(--sst-acao-marca-tinta)',
    fontWeight: 700,
    fontSize: '12.5px',
    cursor: 'pointer',
    padding: '4px 6px',
    borderRadius: '6px',
    ':hover': { backgroundColor: 'var(--sst-acao-marca-wash)' },
  },
});

/** Lista de obras para o seletor; carregada uma vez por sessão do cabeçalho. */
function useObras() {
  const [obras, setObras] = useState<Obra[]>([]);
  useEffect(() => {
    let cancelado = false;
    api.obras
      .listar()
      .then((lista) => {
        if (!cancelado) setObras([...lista].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')));
      })
      .catch(() => {
        if (!cancelado) setObras([]);
      });
    return () => {
      cancelado = true;
    };
  }, []);
  return obras;
}

export function SeletorObraAtiva() {
  const e = useStyles();
  const { obraAtivaId, obraAtivaNome, definirObraAtiva } = useObraAtiva();
  const obras = useObras();
  const [pulsando, setPulsando] = useState(false);

  // Uma obra salva que não existe mais (ou que o usuário perdeu acesso) volta para "Todas".
  // Escolhida numa tela (que só conhece o id), o nome é completado aqui para a faixa das páginas.
  useEffect(() => {
    if (!obraAtivaId || obras.length === 0) return;
    const obra = obras.find((o) => o.id === obraAtivaId);
    if (!obra) definirObraAtiva('');
    else if (obra.nome !== obraAtivaNome) definirObraAtiva(obra.id, obra.nome);
  }, [obraAtivaId, obraAtivaNome, obras, definirObraAtiva]);

  // A pílula pisca de leve sempre que a obra muda (escolha aqui ou em qualquer tela).
  useEffect(() => {
    setPulsando(true);
    const id = window.setTimeout(() => setPulsando(false), 1900);
    return () => window.clearTimeout(id);
  }, [obraAtivaId]);

  const nome = obraAtivaId ? obras.find((o) => o.id === obraAtivaId)?.nome ?? obraAtivaNome : 'Todas as obras';

  return (
    <Menu
      checkedValues={{ obra: [obraAtivaId || 'todas'] }}
      onCheckedValueChange={(_, dados) => {
        const valor = dados.checkedItems[0];
        const id = valor === 'todas' ? '' : valor;
        definirObraAtiva(id, obras.find((o) => o.id === id)?.nome ?? '');
      }}
    >
      <MenuTrigger disableButtonEnhancement>
        <button
          type="button"
          className={mergeClasses(e.pilula, pulsando && e.pulsa)}
          aria-label={`Obra ativa: ${nome}. Trocar a obra do sistema`}
          title="Em qual obra você está trabalhando"
          data-botao="livre"
        >
          <Location16Regular className={e.icone} />
          <span className={e.rotulo}>Obra</span>
          <span className={e.nome}>{nome || 'Obra selecionada'}</span>
          <ChevronDown12Regular />
        </button>
      </MenuTrigger>
      <MenuPopover>
        <div className={e.titulo}>Em qual obra você está trabalhando?</div>
        <MenuList>
          <MenuItemRadio name="obra" value="todas">
            Todas as obras
          </MenuItemRadio>
          {obras.map((obra) => (
            <MenuItemRadio key={obra.id} name="obra" value={obra.id}>
              {obra.nome}
            </MenuItemRadio>
          ))}
        </MenuList>
        <div className={e.rodape}>Vale para o Início e para os painéis de todos os módulos. Fica lembrada no próximo acesso.</div>
      </MenuPopover>
    </Menu>
  );
}

/** Faixa no topo das páginas quando uma obra está escolhida, com atalho para voltar a todas. */
export function FaixaObraAtiva() {
  const e = useStyles();
  const { obraAtivaId, obraAtivaNome, definirObraAtiva } = useObraAtiva();
  if (!obraAtivaId) return null;
  return (
    <div className={e.faixa} role="status">
      <Location16Regular className={e.icone} />
      <span>
        Você está na obra <b>{obraAtivaNome || 'selecionada'}</b>. O Início e os painéis mostram só os dados dela.
      </span>
      <button type="button" className={e.faixaBotao} onClick={() => definirObraAtiva('')}>
        <Dismiss12Regular /> Ver todas as obras
      </button>
    </div>
  );
}
