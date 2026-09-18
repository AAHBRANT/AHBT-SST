import { useEffect, useState, type CSSProperties } from 'react';
import { useParams } from 'react-router-dom';
import { Avatar, Card, designTokens, Legenda, Spinner, StatusChip, Text, type Tom } from '@ui';
import { ShieldError24Regular } from '@fluentui/react-icons';
import { api, StatusArea, tipoAreaLabel, statusAreaLabel, type RecursoPublico } from '../../lib/api';

const raiz: CSSProperties = {
  minHeight: '100vh',
  display: 'flex',
  justifyContent: 'center',
  alignItems: 'flex-start',
  padding: '32px 16px',
  backgroundColor: designTokens.colorNeutralLight,
};

const linhaItem: CSSProperties = {
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'space-between',
  gap: 8,
  padding: '10px 0',
  borderBottom: `1px solid ${designTokens.colorCardBorder}`,
};

const tomStatusArea: Record<number, Tom> = {
  [StatusArea.Ativa]: 'ok',
  [StatusArea.Inativa]: 'atencao',
  [StatusArea.Bloqueada]: 'alerta',
};

const tomAptidao: Record<string, Tom> = {
  Apto: 'ok',
  'Apto com restrição': 'atencao',
  Inapto: 'alerta',
};

const DIAS_ALERTA_VENCIMENTO_EPI = 30;

function diasAte(data: string): number {
  return Math.round((new Date(data).getTime() - new Date(new Date().toDateString()).getTime()) / 86_400_000);
}

function statusEpi(dataValidade?: string | null): { rotulo: string; tom: Tom } {
  if (!dataValidade) return { rotulo: 'Em dia', tom: 'ok' };
  const dias = diasAte(dataValidade);
  if (dias < 0) return { rotulo: 'Vencido', tom: 'alerta' };
  if (dias <= DIAS_ALERTA_VENCIMENTO_EPI) return { rotulo: 'Vencendo', tom: 'atencao' };
  return { rotulo: 'Em dia', tom: 'ok' };
}

// NTAG.md §1/§3.B.4 — "crachá digital" público: quem encosta o celular na tag em campo (fiscal,
// auditor, o próprio trabalhador) não está no Teams e não tem token — a página inteira PRECISA
// funcionar sem login. Por isso só consome api.identificacaoPublica.* aqui: qualquer outra chamada
// (api.entregasEpi, api.trabalhadores.obterPerfilCompleto etc.) exige autenticação e, sem sessão
// Microsoft no aparelho, dispara um login redirect nesta tela — foi exatamente isso que quebrou o
// crachá de campo em 18/09 (AADSTS50011: o pathname da leitura anônima não bate com nenhum redirect
// URI cadastrado no App Registration). Todo o dado exibido abaixo já vem de
// ResolverTrabalhadorPublicoQuery, que deliberadamente omite o que é sensível demais pra leitura
// anônima (status de aptidão do ASO, evidência de presença em DDS) — não adicione de volta uma
// chamada autenticada aqui sem antes criar um endpoint público equivalente no backend.
export function IdentificacaoPublicaPage() {
  const { codigoOuUid } = useParams<{ codigoOuUid: string }>();
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
        // Sem foto não impede a leitura do crachá; Avatar cai para as iniciais do nome.
      }
    })();
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [codigoOuUid, recurso]);

  return (
    <div style={raiz} data-theme="light">
      <div style={{ width: '100%', maxWidth: 480 }}>
        {carregando && (
          <Card>
            <div style={{ display: 'flex', justifyContent: 'center', padding: '48px 0' }}>
              <Spinner label="Carregando..." />
            </div>
          </Card>
        )}

        {!carregando && naoEncontrado && (
          <Card>
            <ShieldError24Regular />
            <Text as="p" weight="semibold" style={{ marginTop: 8 }}>
              Código ou tag não encontrada ou sem acesso.
            </Text>
            <Text as="p">Verifique se o QR Code/NFC está correto e vinculado a uma área ou funcionário ativo.</Text>
          </Card>
        )}

        {!carregando && recurso?.tipoRecurso === 'area' && (
          <Card
            titulo={recurso.nome}
            subtitulo={`${recurso.codigo} · ${tipoAreaLabel[recurso.tipo]}`}
            acoes={
              <StatusChip tom={tomStatusArea[recurso.status] ?? 'info'}>{statusAreaLabel[recurso.status]}</StatusChip>
            }
          >
            {recurso.detalhesLocalizacao && (
              <div style={{ marginTop: 16 }}>
                <Text weight="semibold">Localização</Text>
                <Text as="p">{recurso.detalhesLocalizacao}</Text>
              </div>
            )}

            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">Riscos desta área</Text>
              {recurso.riscos.length === 0 ? (
                <Legenda>Nenhum risco cadastrado.</Legenda>
              ) : (
                <ul style={{ margin: 0, paddingLeft: 20 }}>
                  {recurso.riscos.map((r) => (
                    <li key={r}>
                      <Text>{r}</Text>
                    </li>
                  ))}
                </ul>
              )}
            </div>

            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">Requisitos de acesso/EPI</Text>
              {recurso.requisitos.length === 0 ? (
                <Legenda>Nenhum requisito cadastrado.</Legenda>
              ) : (
                <ul style={{ margin: 0, paddingLeft: 20 }}>
                  {recurso.requisitos.map((r) => (
                    <li key={r}>
                      <Text>{r}</Text>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </Card>
        )}

        {!carregando && recurso?.tipoRecurso === 'trabalhador' && (
          <Card
            titulo={
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <Avatar name={recurso.nome} image={fotoUrl ? { src: fotoUrl } : undefined} color="brand" size={64} />
                <div>
                  <Text size={500} weight="semibold">
                    {recurso.nome}
                  </Text>
                  <div>
                    <Text size={200}>
                      Matrícula {recurso.matricula} · {recurso.funcaoNome}
                    </Text>
                  </div>
                  <Legenda>{recurso.obraNome}</Legenda>
                </div>
              </div>
            }
            acoes={
              // Vem ausente na leitura anônima da tag: o status de aptidão sai do ASO (dado de
              // saúde) e só é retornado a chamador autenticado — ver ResolverTrabalhadorPublicoQuery.
              recurso.statusAptidao ? (
                <StatusChip tom={tomAptidao[recurso.statusAptidao] ?? 'info'}>{recurso.statusAptidao}</StatusChip>
              ) : undefined
            }
          >
            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">EPIs em uso</Text>
              {recurso.episAtivos.length === 0 ? (
                <Legenda>Nenhum EPI ativo registrado.</Legenda>
              ) : (
                recurso.episAtivos.map((epi, indice) => {
                  const status = statusEpi(epi.dataValidade);
                  return (
                    <div key={`${epi.catalogoEpiNome}-${indice}`} style={linhaItem}>
                      <Text>{epi.catalogoEpiNome}</Text>
                      <StatusChip tom={status.tom}>{status.rotulo}</StatusChip>
                    </div>
                  );
                })
              )}
            </div>

            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">Treinamentos (NR)</Text>
              {recurso.treinamentos.length === 0 ? (
                <Legenda>Nenhum treinamento registrado.</Legenda>
              ) : (
                recurso.treinamentos.map((t, indice) => {
                  const valido = diasAte(t.dataValidade) >= 0;
                  return (
                    <div key={`${t.cursoNome}-${indice}`} style={linhaItem}>
                      <div>
                        <Text weight="semibold" size={200} style={{ display: 'block' }}>
                          {t.cursoNome}
                        </Text>
                        <Legenda>
                          {valido ? 'Válido até' : 'Vencido em'} {t.dataValidade.slice(0, 10)}
                        </Legenda>
                      </div>
                      <StatusChip tom={valido ? 'ok' : 'alerta'}>{valido ? 'APTO' : 'BLOQUEADO'}</StatusChip>
                    </div>
                  );
                })
              )}
            </div>

            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">Histórico de DDS</Text>
              {recurso.historicoDds.length === 0 ? (
                <Legenda>Nenhuma participação em DDS registrada.</Legenda>
              ) : (
                recurso.historicoDds.map((d, indice) => (
                  <div key={`${d.data}-${indice}`} style={linhaItem}>
                    <div>
                      <Text weight="semibold" size={200} style={{ display: 'block' }}>
                        {d.tema ?? 'DDS do dia'}
                      </Text>
                      <Legenda>
                        {d.data.slice(0, 10)} · {d.obraNome}
                      </Legenda>
                    </div>
                  </div>
                ))
              )}
            </div>
          </Card>
        )}
      </div>
    </div>
  );
}
