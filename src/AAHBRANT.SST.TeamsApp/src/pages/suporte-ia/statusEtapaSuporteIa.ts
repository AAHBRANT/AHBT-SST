import { StatusSolicitacaoSuporteIa } from '../../lib/api';

// Etapa (0-3) da esteira de 4 passos que está atualmente pendente de ação para o chamado, dado o
// status persistido. null = ciclo encerrado (Resolvida/Cancelada), sem etapa ativa. Compartilhado
// entre o card de triagem (SuporteIaTab, ao criar um chamado) e a tela de detalhe (SuporteIaDetalhePage).
export function etapaAtivaPorStatus(status: number): number | null {
  switch (status) {
    case StatusSolicitacaoSuporteIa.Recebida:
      return 0;
    case StatusSolicitacaoSuporteIa.Encaminhada:
    case StatusSolicitacaoSuporteIa.Reaberta:
    case StatusSolicitacaoSuporteIa.EmAnaliseTecnica:
      return 2;
    case StatusSolicitacaoSuporteIa.Respondida:
    case StatusSolicitacaoSuporteIa.AguardandoValidacao:
      return 3;
    default:
      return null;
  }
}
