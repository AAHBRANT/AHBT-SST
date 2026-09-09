import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Card, designTokens, Spinner, StatusChip, Text } from '@ui';
import { CheckmarkCircle24Regular, ShieldError24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type DocumentoPublico } from '../../lib/api';

// Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5, etapa 11) — página pública
// aberta ao escanear o QR do comprovante. Fica fora do AppShell (sem sidebar/header do Teams), mesmo
// padrão de IdentificacaoPublicaPage (módulo NTAG/Identificação) e template §4.6 "Público" (Onda 2,
// task de pontas soltas fora do escopo numerado): Card centralizado com largura máx. 480px,
// Badge→StatusChip (item 5 do Guia de conversão) — "Válido" é o próprio status do documento (tom
// "ok"), e o método de autenticação de cada signatário é rótulo/categoria sem semântica de estado,
// mesmo julgamento de colapso em tom="neutro" já registrado em RequisitosLegaisTab.tsx.
export function ValidarDocumentoPage() {
  const { token } = useParams<{ token: string }>();
  const [documento, setDocumento] = useState<DocumentoPublico | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [naoEncontrado, setNaoEncontrado] = useState(false);

  useEffect(() => {
    if (!token) return;
    setCarregando(true);
    setNaoEncontrado(false);
    setDocumento(null);
    api.validacaoPublica
      .resolver(token)
      .then(setDocumento)
      .catch(() => setNaoEncontrado(true))
      .finally(() => setCarregando(false));
  }, [token]);

  return (
    // data-theme="light" no wrapper (não só no FluentProvider de App.tsx): os tokens --sst-* que
    // Card/StatusChip consomem (spec §1.6) são custom properties de index.css escopadas por
    // [data-theme] em qualquer elemento, não só :root — mesmo achado documentado em
    // IdentificacaoPublicaPage.tsx.
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        justifyContent: 'center',
        alignItems: 'flex-start',
        padding: '32px 16px',
        backgroundColor: designTokens.colorNeutralLight,
      }}
      data-theme="light"
    >
      <div style={{ width: '100%', maxWidth: 480 }}>
        {carregando && (
          <Card>
            <div style={{ display: 'flex', justifyContent: 'center', padding: '48px 0' }}>
              <Spinner label="Validando documento..." />
            </div>
          </Card>
        )}

        {!carregando && naoEncontrado && (
          <Card>
            <ShieldError24Regular />
            <Text as="p" weight="semibold" style={{ marginTop: 8 }}>
              Documento não encontrado.
            </Text>
            <Text as="p">Verifique se o link ou o QR Code está correto e se o documento foi finalizado.</Text>
          </Card>
        )}

        {!carregando && documento && (
          <Card
            titulo="Documento válido"
            subtitulo={`${documento.entidadeTipo} · finalizado em ${new Date(documento.finalizadoEm).toLocaleString('pt-BR')}`}
            acoes={
              <StatusChip tom="ok" icone={<CheckmarkCircle24Regular />}>
                Válido
              </StatusChip>
            }
          >
            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">Assinaturas registradas</Text>
              <ul style={{ margin: 0, paddingLeft: 20 }}>
                {documento.signatarios.map((s, i) => (
                  <li key={i} style={{ marginTop: 4 }}>
                    <Text>{s.trabalhadorNome} — </Text>
                    <StatusChip tom="neutro">
                      {metodoAutenticacaoAssinaturaLabel[s.metodoAutenticacao] ?? 'Método desconhecido'}
                    </StatusChip>
                    <Text> em {new Date(s.assinadoEm).toLocaleString('pt-BR')}</Text>
                  </li>
                ))}
              </ul>
            </div>

            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">Hash de integridade (SHA-256)</Text>
              <Text
                as="p"
                style={{
                  wordBreak: 'break-all',
                  fontFamily: 'monospace',
                  fontSize: 11,
                  color: designTokens.colorNeutralMedium,
                }}
              >
                {documento.conteudoHash}
              </Text>
            </div>
          </Card>
        )}
      </div>
    </div>
  );
}
