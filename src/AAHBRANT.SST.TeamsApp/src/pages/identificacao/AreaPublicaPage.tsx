import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Badge, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components';
import { ShieldError24Regular } from '@fluentui/react-icons';
import { api, StatusArea, tipoAreaLabel, statusAreaLabel, type AreaPublicaDto } from '../../lib/api';
import { designTokens } from '../../theme';

// NTAG.md §3.B.4 — página pública aberta ao escanear o QR/NFC de uma área. Fica fora do AppShell
// (sem sidebar/header do Teams) porque quem escaneia em campo pode não estar logado nem no Teams.
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
    justifyContent: 'space-between',
    alignItems: 'flex-start',
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
});

const corStatus: Record<number, 'success' | 'warning' | 'danger'> = {
  [StatusArea.Ativa]: 'success',
  [StatusArea.Inativa]: 'warning',
  [StatusArea.Bloqueada]: 'danger',
};

export function AreaPublicaPage() {
  const { codigoOuUid } = useParams<{ codigoOuUid: string }>();
  const estilos = useStyles();
  const [area, setArea] = useState<AreaPublicaDto | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [naoEncontrada, setNaoEncontrada] = useState(false);

  useEffect(() => {
    if (!codigoOuUid) return;
    setCarregando(true);
    setNaoEncontrada(false);
    setArea(null);
    api.identificacaoPublica
      .resolver(codigoOuUid)
      .then(setArea)
      .catch(() => setNaoEncontrada(true))
      .finally(() => setCarregando(false));
  }, [codigoOuUid]);

  return (
    <div className={estilos.root}>
      <div className={estilos.card}>
        {carregando && (
          <div className={estilos.centro}>
            <Spinner label="Carregando informações da área..." />
          </div>
        )}

        {!carregando && naoEncontrada && (
          <>
            <ShieldError24Regular />
            <Text as="p" weight="semibold" style={{ marginTop: 8 }}>
              Código ou tag não encontrada.
            </Text>
            <Text as="p">Verifique se o QR Code/NFC está correto e vinculado a uma área ativa.</Text>
          </>
        )}

        {!carregando && area && (
          <>
            <div className={estilos.header}>
              <div>
                <Text size={600} weight="semibold">
                  {area.nome}
                </Text>
                <div>
                  <Text size={200}>
                    {area.codigo} · {tipoAreaLabel[area.tipo]}
                  </Text>
                </div>
              </div>
              <Badge color={corStatus[area.status] ?? 'informative'} appearance="tint" size="large">
                {statusAreaLabel[area.status]}
              </Badge>
            </div>

            {area.detalhesLocalizacao && (
              <div className={estilos.secao}>
                <Text weight="semibold">Localização</Text>
                <Text as="p">{area.detalhesLocalizacao}</Text>
              </div>
            )}

            <div className={estilos.secao}>
              <Text weight="semibold">Riscos desta área</Text>
              {area.riscos.length === 0 ? (
                <Text as="p" style={{ color: tokens.colorNeutralForeground3 }}>
                  Nenhum risco cadastrado.
                </Text>
              ) : (
                <ul className={estilos.listaSimples}>
                  {area.riscos.map((r) => (
                    <li key={r}>
                      <Text>{r}</Text>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">Requisitos de acesso/EPI</Text>
              {area.requisitos.length === 0 ? (
                <Text as="p" style={{ color: tokens.colorNeutralForeground3 }}>
                  Nenhum requisito cadastrado.
                </Text>
              ) : (
                <ul className={estilos.listaSimples}>
                  {area.requisitos.map((r) => (
                    <li key={r}>
                      <Text>{r}</Text>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
