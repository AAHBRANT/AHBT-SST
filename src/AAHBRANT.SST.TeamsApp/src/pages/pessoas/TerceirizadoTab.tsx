import { useEffect, useState } from 'react';
import { Card, StatusChip, Text, FeedbackInline, Carregando } from '@ui';
import { api, type PerfilCompletoTrabalhador, type StatusLiberacaoTrabalhador } from '../../lib/api';

interface Props {
  trabalhadorId: string;
  perfil: PerfilCompletoTrabalhador;
}

// Aba "Terceirizado" — só aparece na ficha de trabalhadores com Vinculo=Terceirizado (ver
// TrabalhadorDetalhePage.tsx). Mostra empresa/contrato do vínculo (vindos do Perfil de Vida
// agregado) e o status de liberação calculado pela CalculadoraLiberacaoTerceirizado.
export function TerceirizadoTab({ trabalhadorId, perfil }: Props) {
  const [statusLiberacao, setStatusLiberacao] = useState<StatusLiberacaoTrabalhador | null>(null);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        setStatusLiberacao(await api.terceirizados.pessoas.obterStatus(trabalhadorId));
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar status de liberação.');
      }
    })();
  }, [trabalhadorId]);

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      <Card titulo="Empresa e contrato">
        <div style={{ display: 'flex', gap: 20, flexWrap: 'wrap', alignItems: 'center' }}>
          <Text>
            <strong>Empresa:</strong> {perfil.empresaRazaoSocial ?? '—'}
          </Text>
          <Text>
            <strong>Contrato:</strong> {perfil.numeroContrato ?? '—'}
          </Text>
          <Text>
            <strong>Função:</strong> {perfil.funcaoNome}
          </Text>
        </div>
      </Card>
      <Card titulo="Status de liberação">
        {!statusLiberacao ? (
          <Carregando variante="detalhe" linhas={3} />
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
            <StatusChip tom={statusLiberacao.status === 'Liberada' ? 'ok' : 'alerta'}>{statusLiberacao.status}</StatusChip>
            {statusLiberacao.pendencias.length > 0 && (
              <ul style={{ margin: 0, paddingLeft: 20 }}>
                {statusLiberacao.pendencias.map((p) => (
                  <li key={p}>
                    <Text>{p}</Text>
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </Card>
    </div>
  );
}
