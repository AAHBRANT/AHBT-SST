import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Badge, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components';
import { CheckmarkCircle24Regular, ShieldError24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type DocumentoPublico } from '../../lib/api';
import { designTokens } from '../../theme';

// Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5, etapa 11) — página pública
// aberta ao escanear o QR do comprovante. Fica fora do AppShell (sem sidebar/header do Teams), mesmo
// padrão de IdentificacaoPublicaPage (módulo NTAG/Identificação), porque quem escaneia pode não estar logado
// nem no Teams.
const useStyles = makeStyles({
  root: {
    minHeight: '100vh',
    display: 'flex',
    justifyContent: 'center',
    alignItems: 'flex-start',
    padding: '32px 16px',
    backgroundColor: designTokens.colorNeutralLight,
  },
  card: {
    width: '100%',
    maxWidth: '480px',
    backgroundColor: designTokens.colorWhite,
    borderRadius: '8px',
    boxShadow: '0 1px 3px rgba(0, 0, 0, 0.08)',
    padding: '24px',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    marginBottom: '16px',
  },
  secao: {
    marginTop: '16px',
  },
  listaSimples: {
    margin: 0,
    paddingLeft: '20px',
  },
  centro: {
    display: 'flex',
    justifyContent: 'center',
    padding: '48px 0',
  },
  hash: {
    wordBreak: 'break-all',
    fontFamily: 'monospace',
    fontSize: '11px',
    color: tokens.colorNeutralForeground3,
  },
});

export function ValidarDocumentoPage() {
  const { token } = useParams<{ token: string }>();
  const estilos = useStyles();
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
    <div className={estilos.root}>
      <div className={estilos.card}>
        {carregando && (
          <div className={estilos.centro}>
            <Spinner label="Validando documento..." />
          </div>
        )}

        {!carregando && naoEncontrado && (
          <>
            <ShieldError24Regular />
            <Text as="p" weight="semibold" style={{ marginTop: 8 }}>
              Documento não encontrado.
            </Text>
            <Text as="p">Verifique se o link ou o QR Code está correto e se o documento foi finalizado.</Text>
          </>
        )}

        {!carregando && documento && (
          <>
            <div className={estilos.header}>
              <CheckmarkCircle24Regular color={tokens.colorPaletteGreenForeground1} />
              <div>
                <Text size={600} weight="semibold">
                  Documento válido
                </Text>
                <div>
                  <Text size={200}>
                    {documento.entidadeTipo} · finalizado em{' '}
                    {new Date(documento.finalizadoEm).toLocaleString('pt-BR')}
                  </Text>
                </div>
              </div>
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">Assinaturas registradas</Text>
              <ul className={estilos.listaSimples}>
                {documento.signatarios.map((s, i) => (
                  <li key={i}>
                    <Text>
                      {s.trabalhadorNome} —{' '}
                      <Badge appearance="tint" size="small">
                        {metodoAutenticacaoAssinaturaLabel[s.metodoAutenticacao] ?? 'Método desconhecido'}
                      </Badge>{' '}
                      em {new Date(s.assinadoEm).toLocaleString('pt-BR')}
                    </Text>
                  </li>
                ))}
              </ul>
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">Hash de integridade (SHA-256)</Text>
              <Text as="p" className={estilos.hash}>
                {documento.conteudoHash}
              </Text>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
