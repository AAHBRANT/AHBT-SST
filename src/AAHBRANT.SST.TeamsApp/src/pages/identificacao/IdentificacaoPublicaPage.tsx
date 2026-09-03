import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { Avatar, Badge, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components';
import { ShieldError24Regular } from '@fluentui/react-icons';
import { api, StatusArea, tipoAreaLabel, statusAreaLabel, type RecursoPublico } from '../../lib/api';
import { designTokens } from '../../theme';

// NTAG.md §3.B.4 — página pública aberta ao escanear o QR/NFC de uma Área OU de um Trabalhador
// (crachá digital). Fica fora do AppShell (sem sidebar/header do Teams) porque quem escaneia em
// campo pode não estar logado nem no Teams. Renomeada de AreaPublicaPage (03/09) ao ganhar o segundo
// tipo de recurso — o discriminador tipoRecurso da resposta decide qual card renderizar.
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
  linhaItem: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: '8px',
    padding: '10px 0',
    borderBottom: `1px solid ${designTokens.colorCardBorder}`,
  },
});

const corStatusArea: Record<number, 'success' | 'warning' | 'danger'> = {
  [StatusArea.Ativa]: 'success',
  [StatusArea.Inativa]: 'warning',
  [StatusArea.Bloqueada]: 'danger',
};

const corAptidao: Record<string, 'success' | 'warning' | 'danger' | 'informative'> = {
  Apto: 'success',
  'Apto com restrição': 'warning',
  Inapto: 'danger',
};

// Mesma janela de "vencendo" usada no Dashboard do Trabalhador (PerfilGeralTab.tsx) — consistência
// entre a visão interna (autenticada) e a visão pública do mesmo dado.
const DIAS_ALERTA_VENCIMENTO_EPI = 30;

function diasAte(data: string): number {
  return Math.round((new Date(data).getTime() - new Date(new Date().toDateString()).getTime()) / 86_400_000);
}

function statusEpi(dataValidade?: string | null): { rotulo: string; cor: 'success' | 'warning' | 'danger' } {
  if (!dataValidade) return { rotulo: 'Em dia', cor: 'success' };
  const dias = diasAte(dataValidade);
  if (dias < 0) return { rotulo: 'Vencido', cor: 'danger' };
  if (dias <= DIAS_ALERTA_VENCIMENTO_EPI) return { rotulo: 'Vencendo', cor: 'warning' };
  return { rotulo: 'Em dia', cor: 'success' };
}

export function IdentificacaoPublicaPage() {
  const { codigoOuUid } = useParams<{ codigoOuUid: string }>();
  const estilos = useStyles();
  const [recurso, setRecurso] = useState<RecursoPublico | null>(null);
  const [fotoUrl, setFotoUrl] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);
  const [naoEncontrado, setNaoEncontrado] = useState(false);

  useEffect(() => {
    if (!codigoOuUid) return;
    setCarregando(true);
    setNaoEncontrado(false);
    setRecurso(null);
    api.identificacaoPublica
      .resolver(codigoOuUid)
      .then(setRecurso)
      .catch(() => setNaoEncontrado(true))
      .finally(() => setCarregando(false));
  }, [codigoOuUid]);

  useEffect(() => {
    if (!codigoOuUid || recurso?.tipoRecurso !== 'trabalhador' || !recurso.temFoto) return;
    let cancelado = false;
    let urlCriada: string | null = null;
    (async () => {
      try {
        const blob = await api.identificacaoPublica.baixarFotoTrabalhador(codigoOuUid);
        if (cancelado) return;
        urlCriada = URL.createObjectURL(blob);
        setFotoUrl(urlCriada);
      } catch {
        // Sem foto não impede a leitura do crachá — Avatar cai para as iniciais do nome.
      }
    })();
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [codigoOuUid, recurso]);

  return (
    <div className={estilos.root}>
      <div className={estilos.card}>
        {carregando && (
          <div className={estilos.centro}>
            <Spinner label="Carregando..." />
          </div>
        )}

        {!carregando && naoEncontrado && (
          <>
            <ShieldError24Regular />
            <Text as="p" weight="semibold" style={{ marginTop: 8 }}>
              Código ou tag não encontrada.
            </Text>
            <Text as="p">Verifique se o QR Code/NFC está correto e vinculado a uma área ou funcionário ativo.</Text>
          </>
        )}

        {!carregando && recurso?.tipoRecurso === 'area' && (
          <>
            <div className={estilos.header}>
              <div>
                <Text size={600} weight="semibold">
                  {recurso.nome}
                </Text>
                <div>
                  <Text size={200}>
                    {recurso.codigo} · {tipoAreaLabel[recurso.tipo]}
                  </Text>
                </div>
              </div>
              <Badge color={corStatusArea[recurso.status] ?? 'informative'} appearance="tint" size="large">
                {statusAreaLabel[recurso.status]}
              </Badge>
            </div>

            {recurso.detalhesLocalizacao && (
              <div className={estilos.secao}>
                <Text weight="semibold">Localização</Text>
                <Text as="p">{recurso.detalhesLocalizacao}</Text>
              </div>
            )}

            <div className={estilos.secao}>
              <Text weight="semibold">Riscos desta área</Text>
              {recurso.riscos.length === 0 ? (
                <Text as="p" style={{ color: tokens.colorNeutralForeground3 }}>
                  Nenhum risco cadastrado.
                </Text>
              ) : (
                <ul className={estilos.listaSimples}>
                  {recurso.riscos.map((r) => (
                    <li key={r}>
                      <Text>{r}</Text>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">Requisitos de acesso/EPI</Text>
              {recurso.requisitos.length === 0 ? (
                <Text as="p" style={{ color: tokens.colorNeutralForeground3 }}>
                  Nenhum requisito cadastrado.
                </Text>
              ) : (
                <ul className={estilos.listaSimples}>
                  {recurso.requisitos.map((r) => (
                    <li key={r}>
                      <Text>{r}</Text>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </>
        )}

        {!carregando && recurso?.tipoRecurso === 'trabalhador' && (
          <>
            <div className={estilos.header}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <Avatar name={recurso.nome} image={fotoUrl ? { src: fotoUrl } : undefined} color="brand" size={56} />
                <div>
                  <Text size={500} weight="semibold">
                    {recurso.nome}
                  </Text>
                  <div>
                    <Text size={200}>
                      Matrícula {recurso.matricula} · {recurso.funcaoNome}
                    </Text>
                  </div>
                  <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                    {recurso.obraNome}
                  </Text>
                </div>
              </div>
              <Badge color={corAptidao[recurso.statusAptidao] ?? 'informative'} appearance="tint" size="large">
                {recurso.statusAptidao}
              </Badge>
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">EPIs em uso</Text>
              {recurso.episAtivos.length === 0 ? (
                <Text as="p" style={{ color: tokens.colorNeutralForeground3 }}>
                  Nenhum EPI ativo registrado.
                </Text>
              ) : (
                recurso.episAtivos.map((epi, indice) => {
                  const status = statusEpi(epi.dataValidade);
                  return (
                    <div key={`${epi.catalogoEpiNome}-${indice}`} className={estilos.linhaItem}>
                      <Text>{epi.catalogoEpiNome}</Text>
                      <Badge color={status.cor} appearance="tint">
                        {status.rotulo}
                      </Badge>
                    </div>
                  );
                })
              )}
            </div>

            <div className={estilos.secao}>
              <Text weight="semibold">Treinamentos (NR)</Text>
              {recurso.treinamentos.length === 0 ? (
                <Text as="p" style={{ color: tokens.colorNeutralForeground3 }}>
                  Nenhum treinamento registrado.
                </Text>
              ) : (
                recurso.treinamentos.map((t, indice) => {
                  const valido = diasAte(t.dataValidade) >= 0;
                  return (
                    <div key={`${t.cursoNome}-${indice}`} className={estilos.linhaItem}>
                      <div>
                        <Text weight="semibold" size={200} style={{ display: 'block' }}>
                          {t.cursoNome}
                        </Text>
                        <Text size={200} style={{ color: tokens.colorNeutralForeground3 }}>
                          {valido ? 'Válido até' : 'Vencido em'} {t.dataValidade.slice(0, 10)}
                        </Text>
                      </div>
                      <Badge color={valido ? 'success' : 'danger'} appearance="tint">
                        {valido ? 'APTO' : 'BLOQUEADO'}
                      </Badge>
                    </div>
                  );
                })
              )}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
