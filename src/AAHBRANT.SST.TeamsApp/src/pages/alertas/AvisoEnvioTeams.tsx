import { useEffect, useState } from 'react';
import { FeedbackInline } from '@ui';
import { api, type SaudeEnvioTeams } from '../../lib/api';

function formatarDataHora(iso: string) {
  return new Date(iso).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
}

// Aviso de falha silenciosa (pedido do usuário, 25/09/2026): as notificações do sininho do Teams
// falharam por semanas sem ninguém perceber. Aparece só quando o envio mais recente falhou e some
// sozinho assim que um envio volta a dar certo. Erro na própria consulta não mostra nada — não é
// papel deste aviso alarmar por outra causa.
export function AvisoEnvioTeams() {
  const [saude, setSaude] = useState<SaudeEnvioTeams | null>(null);

  useEffect(() => {
    let ativo = true;
    api.alertas
      .saudeEnvioTeams()
      .then((s) => ativo && setSaude(s))
      .catch(() => undefined);
    return () => {
      ativo = false;
    };
  }, []);

  if (!saude?.falhando || !saude.ultimaFalhaEmUtc) return null;

  return (
    <FeedbackInline tom="erro">
      <strong>As notificações de alerta não estão chegando no Teams.</strong>{' '}
      {saude.falhasDesdeUltimoSucesso} tentativa(s) de envio falharam desde{' '}
      {saude.ultimoSucessoEmUtc ? `o último sucesso (${formatarDataHora(saude.ultimoSucessoEmUtc)})` : 'o início'}; última
      falha em {formatarDataHora(saude.ultimaFalhaEmUtc)}. Acione o suporte técnico.
      {saude.ultimoErro && (
        <div style={{ marginTop: 4, fontSize: 12, opacity: 0.8, wordBreak: 'break-word' }}>Detalhe: {saude.ultimoErro}</div>
      )}
    </FeedbackInline>
  );
}
