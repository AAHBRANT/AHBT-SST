import { useEffect, useState } from 'react';
import { Card, Legenda, StatusChip, designTokens, tokensUi } from '@ui';
import { Checkmark16Regular } from '@fluentui/react-icons';
import { api, type RegistroIntegracaoRealizada } from '../../lib/api';

function dataBr(iso?: string | null) {
  return iso ? iso.slice(0, 10).split('-').reverse().join('/') : '—';
}

// Bloco "Integração realizada" do perfil (11/11/2026): a integração deixou de gerar certificado. Quando a
// pessoa conclui todas as etapas e assina a Ordem de Serviço (NR-01), o perfil ganha este registro com a
// data, a obra e o técnico. Certificados de integrações antigas continuam na lista de treinamentos.
// Some sozinho enquanto não houver registro (ou se o servidor ainda não oferecer a consulta).
export function IntegracaoRealizadaCard({ trabalhadorId }: { trabalhadorId: string }) {
  const [registros, setRegistros] = useState<RegistroIntegracaoRealizada[]>([]);

  useEffect(() => {
    let ativo = true;
    api.integracoesAtendimento
      .registrosDoTrabalhador(trabalhadorId)
      .then((lista) => ativo && setRegistros(lista ?? []))
      .catch(() => ativo && setRegistros([]));
    return () => {
      ativo = false;
    };
  }, [trabalhadorId]);

  if (registros.length === 0) return null;

  return (
    <Card titulo="Integração realizada" subtitulo="Registro da integração. Não é um certificado.">
      {registros.map((r) => (
        <div
          key={r.id}
          style={{
            display: 'grid',
            gridTemplateColumns: 'minmax(0, 1fr) auto',
            gap: tokensUi.espaco.md,
            alignItems: 'center',
            padding: `${tokensUi.espaco.md} 0`,
            borderTop: `1px solid ${designTokens.colorCardBorder}`,
          }}
        >
          <div>
            <b>{dataBr(r.realizadaEmUtc)}</b> · {r.obraNome}
            <Legenda>Técnico: {r.tecnicoNome}{r.funcaoNome ? ` · Função: ${r.funcaoNome}` : ''} · {r.numeroAtendimento}</Legenda>
          </div>
          <StatusChip tom="ok" icone={<Checkmark16Regular />}>Integração realizada</StatusChip>
        </div>
      ))}
    </Card>
  );
}
