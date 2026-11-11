import { useEffect, useState } from 'react';
import { estaAgenteLocalDisponivel, obterDispositivoLocal, type DispositivoLocal } from '../../lib/agenteBiometricoLocal';

// Estado do Agente Biométrico desta máquina (leitor Futronic). Extraído de SessaoTreinamentoDetalhePage
// (11/11/2026) para a turma comum e a integração em etapas usarem o mesmo leitor.
export function useAgenteBiometricoLocal() {
  const [agenteDisponivel, setAgenteDisponivel] = useState<boolean | null>(null);
  const [dispositivoLocal, setDispositivoLocal] = useState<DispositivoLocal | null>(null);

  useEffect(() => {
    let ativo = true;
    estaAgenteLocalDisponivel().then(async (disponivel) => {
      if (!ativo) return;
      setAgenteDisponivel(disponivel);
      if (disponivel) {
        const dispositivo = await obterDispositivoLocal();
        if (ativo) setDispositivoLocal(dispositivo);
      }
    });
    return () => {
      ativo = false;
    };
  }, []);

  return { agenteDisponivel, dispositivoLocal };
}
