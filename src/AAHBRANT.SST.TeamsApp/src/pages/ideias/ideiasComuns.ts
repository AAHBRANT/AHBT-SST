import { StatusIdeia, type UsuarioLogado } from '../../lib/api';
import type { Tom } from '@ui';
import { useUsuarioLogado } from '../../lib/UsuarioLogadoContext';

// Cor do chip de status (mesma leitura de outros módulos: ok = concluído/aprovado, atenção = espera).
export const tomStatusIdeia: Record<number, Tom> = {
  [StatusIdeia.NovaIdeia]: 'neutro',
  [StatusIdeia.EmAnalise]: 'info',
  [StatusIdeia.AguardandoDecisao]: 'atencao',
  [StatusIdeia.Aprovada]: 'ok',
  [StatusIdeia.Priorizada]: 'ok',
  [StatusIdeia.EmDesenvolvimento]: 'info',
  [StatusIdeia.EmTesteValidacao]: 'atencao',
  [StatusIdeia.Implantada]: 'ok',
  [StatusIdeia.Adiada]: 'neutro',
  [StatusIdeia.Descartada]: 'alerta',
};

// Permissão exigida por status de destino — espelha FluxoStatusIdeia.PermissaoExigida (backend).
export function permissaoParaStatus(destino: number): string {
  if (destino === StatusIdeia.EmAnalise || destino === StatusIdeia.AguardandoDecisao) return 'ideia:analisar';
  if (
    destino === StatusIdeia.Aprovada ||
    destino === StatusIdeia.Priorizada ||
    destino === StatusIdeia.Adiada ||
    destino === StatusIdeia.Descartada
  )
    return 'ideia:decidir';
  return 'ideia:desenvolver';
}

export function temPermissaoIdeia(usuario: UsuarioLogado | null, codigo: string): boolean {
  if (!usuario) return false;
  // O servidor concede todas as permissões ideia:* ao Administrador (especificação §17).
  return usuario.ehAdministrador || usuario.permissoes.includes(codigo);
}

export function usePermissoesIdeia() {
  const { usuario } = useUsuarioLogado();
  return {
    podeAnalisar: temPermissaoIdeia(usuario, 'ideia:analisar'),
    podeDecidir: temPermissaoIdeia(usuario, 'ideia:decidir'),
    podeDesenvolver: temPermissaoIdeia(usuario, 'ideia:desenvolver'),
    pode: (codigo: string) => temPermissaoIdeia(usuario, codigo),
  };
}

export function formatarDataHora(valor?: string | null): string {
  if (!valor) return '—';
  return new Date(valor).toLocaleString('pt-BR', {
    day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit',
  });
}

export function formatarData(valor?: string | null): string {
  if (!valor) return '—';
  return new Date(valor).toLocaleDateString('pt-BR');
}

export function formatarTamanho(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
