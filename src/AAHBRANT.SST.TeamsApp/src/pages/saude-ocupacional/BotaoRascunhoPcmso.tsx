import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, FeedbackInline } from '@ui';
import { api } from '../../lib/api';

// Cria o rascunho do PCMSO da obra a partir do PGR vigente: só funções e riscos, sem exames nem médico. O servidor
// recusa quando a obra já tem PCMSO em vigor, em aprovação ou em rascunho, e a mensagem dele aparece aqui.
export function BotaoRascunhoPcmso({ obraId, desabilitado, aoCriar }: {
  obraId: string;
  desabilitado?: boolean;
  aoCriar?: () => void;
}) {
  const [criando, setCriando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [criadoId, setCriadoId] = useState<string | null>(null);
  const navigate = useNavigate();

  async function criar() {
    try {
      setCriando(true);
      setErro(null);
      const r = await api.escopoPcmso.criarRascunho(obraId);
      setCriadoId(r.id);
      aoCriar?.();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível criar o rascunho do PCMSO.');
    } finally {
      setCriando(false);
    }
  }

  if (criadoId) {
    return (
      <>
        <FeedbackInline tom="sucesso">Rascunho do PCMSO criado a partir do PGR. Exames e médico ficam para o médico preencher.</FeedbackInline>
        <Button appearance="primary" onClick={() => navigate(`/operacao/saude-ocupacional/pcmso/${criadoId}`)}>Abrir o rascunho</Button>
      </>
    );
  }

  return (
    <>
      <Button appearance="primary" disabled={desabilitado || criando || !obraId} onClick={criar}>
        {criando ? 'Criando…' : 'Criar rascunho do PCMSO'}
      </Button>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
    </>
  );
}
