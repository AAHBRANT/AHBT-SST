// Pop-up central de erros/pendências. Antes, cada tela mostrava o erro num texto no topo da página:
// quem estava no fim do formulário (onde fica o botão Salvar) não via nada. Agora qualquer tela
// (ou o interceptador de erros do servidor) chama mostrarAvisoErro e o modal único montado no
// AppShell abre no centro da tela, onde quer que o usuário esteja.

export interface ItemPendencia {
  // Nome do campo como o usuário o vê na tela ("Obra", "Foto do item").
  nome: string;
  // O que fazer para resolver ("Selecione a obra da inspeção").
  dica?: string;
  // Campo para onde o botão "Ir" leva: o próprio elemento, ou o id dele no DOM.
  alvo?: HTMLElement | string | null;
}

export interface AvisoErro {
  titulo: string;
  descricao?: string;
  itens?: ItemPendencia[];
  // 'pendencia' = faltou preencher (âmbar); 'erro' = o servidor recusou (vermelho).
  tipo?: 'pendencia' | 'erro';
}

type Ouvinte = (aviso: AvisoErro) => void;
const ouvintes = new Set<Ouvinte>();

export function mostrarAvisoErro(aviso: AvisoErro): void {
  ouvintes.forEach((ouvinte) => ouvinte(aviso));
}

export function ouvirAvisoErro(ouvinte: Ouvinte): () => void {
  ouvintes.add(ouvinte);
  return () => ouvintes.delete(ouvinte);
}

export interface CampoObrigatorio extends ItemPendencia {
  // true quando o campo já está preenchido corretamente.
  ok: boolean;
}

// Uso típico no início do "salvar" de uma tela:
//   if (!conferirPendencias([{ nome: 'Obra', dica: 'Selecione a obra', ok: !!obraId, alvo: 'campo-obra' }])) return;
// Devolve true quando não falta nada; senão abre o pop-up com a lista e devolve false.
export function conferirPendencias(campos: CampoObrigatorio[], titulo?: string): boolean {
  const faltando = campos.filter((campo) => !campo.ok);
  if (faltando.length === 0) return true;
  mostrarAvisoErro({
    tipo: 'pendencia',
    titulo:
      titulo ??
      (faltando.length === 1 ? 'Falta 1 item para salvar' : `Faltam ${faltando.length} itens para salvar`),
    descricao: 'Preencha os itens abaixo. Clique em "Ir" para ir direto ao campo.',
    itens: faltando.map(({ nome, dica, alvo }) => ({ nome, dica, alvo })),
  });
  return false;
}

export function resolverAlvo(alvo: ItemPendencia['alvo']): HTMLElement | null {
  if (!alvo) return null;
  return typeof alvo === 'string' ? document.getElementById(alvo) : alvo;
}

// Rola até o campo, pisca o contorno e coloca o cursor nele.
export function irParaCampo(alvo: ItemPendencia['alvo']): void {
  const elemento = resolverAlvo(alvo);
  if (!elemento) return;
  elemento.scrollIntoView({ behavior: 'smooth', block: 'center' });
  const focavel =
    elemento.matches('input, select, textarea, button, [tabindex]')
      ? elemento
      : elemento.querySelector<HTMLElement>('input, select, textarea, button, [tabindex]') ?? elemento;
  elemento.animate(
    [
      { boxShadow: '0 0 0 0 rgba(180,35,24,0)' },
      { boxShadow: '0 0 0 4px rgba(180,35,24,.45)' },
      { boxShadow: '0 0 0 0 rgba(180,35,24,0)' },
    ],
    { duration: 1400, easing: 'ease-out' },
  );
  window.setTimeout(() => focavel.focus({ preventScroll: true }), 400);
}
