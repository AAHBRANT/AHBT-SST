import { useNavigate } from 'react-router-dom';
import { Carregando, FeedbackInline } from '@ui';
import { useAgentesCamera, useCameras, useEquipamentosCamera, useObras } from '../../lib/consultas';
import { useSouAdministrador } from '../../lib/UsuarioLogadoContext';
import type { EquipamentoCamera } from '../../lib/api';
import { SecaoAgentes } from './SecaoAgentes';
import { SecaoEquipamentos } from './SecaoEquipamentos';

// Terceira aba de Câmeras (sem item de menu): os agentes (PC da obra) e os equipamentos (câmeras físicas) da câmera IP.
export function EquipamentosAgentesTab() {
  const navigate = useNavigate();
  const souAdministrador = useSouAdministrador();
  const agentes = useAgentesCamera();
  const equipamentos = useEquipamentosCamera();
  const cameras = useCameras();
  const { lista: obras } = useObras();

  const carregando = agentes.isLoading || equipamentos.isLoading;
  const falha = agentes.error ?? equipamentos.error;

  // A lente livre abre o cadastro de câmera já apontando para o equipamento: a aba Câmeras lê estes parâmetros e limpa a URL.
  function novaCamera(equipamento: EquipamentoCamera, lente: number) {
    const params = new URLSearchParams({
      aba: 'cameras',
      nova: '1',
      obra: equipamento.obraId,
      equipamento: equipamento.id,
      lente: String(lente),
    });
    navigate(`/cameras?${params.toString()}`);
  }

  if (carregando) return <Carregando variante="card" linhas={3} />;

  if (falha) {
    return (
      <FeedbackInline
        tom="erro"
        acao={{
          rotulo: 'Tentar de novo',
          aoClicar: () => {
            void agentes.refetch();
            void equipamentos.refetch();
          },
        }}
      >
        {falha instanceof Error ? falha.message : 'Não foi possível carregar os agentes e equipamentos.'}
      </FeedbackInline>
    );
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 28 }}>
      <SecaoAgentes
        agentes={agentes.data ?? []}
        equipamentos={equipamentos.data ?? []}
        obras={obras}
        souAdministrador={souAdministrador}
      />
      <SecaoEquipamentos
        equipamentos={equipamentos.data ?? []}
        agentes={agentes.data ?? []}
        cameras={cameras.data ?? []}
        aoNovaCamera={novaCamera}
      />
    </div>
  );
}
