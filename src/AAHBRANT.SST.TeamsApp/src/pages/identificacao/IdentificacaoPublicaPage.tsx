import { useEffect, useState, type CSSProperties } from 'react';
import { useParams } from 'react-router-dom';
import { Avatar, Card, designTokens, Legenda, Spinner, StatusChip, Text, type Tom } from '@ui';
import { ShieldError24Regular } from '@fluentui/react-icons';
import { api, StatusArea, tipoAreaLabel, statusAreaLabel, type RecursoPublico } from '../../lib/api';

// NTAG.md §3.B.4 — página pública aberta ao escanear o QR/NFC de uma Área OU de um Trabalhador
// (crachá digital). Fica fora do AppShell (sem sidebar/header do Teams) e sempre em tema claro —
// ambos já garantidos em App.tsx (rota /p/:codigoOuUid fora de LayoutComTeams, FluentProvider
// próprio com aahbrantLightTheme). Onda 2 Task 9 (camada ui/), template §4.6 "Público": Card
// centralizado com largura máx. 480px, Avatar grande, Badge→StatusChip (item 3, 5 do Guia).
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

// Mesma janela de "vencendo" usada no Dashboard do Trabalhador (PerfilGeralTab.tsx) — consistência
// entre a visão interna (autenticada) e a visão pública do mesmo dado.
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
        // Sem foto não impede a leitura do crachá — Avatar cai para as iniciais do nome.
      }
    })();
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [codigoOuUid, recurso]);

  return (
    // data-theme="light" no wrapper (não só no FluentProvider do App.tsx): os tokens --sst-* que
    // Card/StatusChip/Legenda consomem (spec §1.6) são custom properties de index.css escopadas por
    // [data-theme] em QUALQUER elemento, não só :root — sem isso, um dispositivo que já usou o app
    // interno em modo escuro (ThemeModeContext, localStorage global) escurecia junto o crachá público,
    // ficando ilegível (achado real ao migrar para Card, item 3 do Guia — corrigido aqui, na raiz da
    // página, e não dentro de Card, porque só a página "Público" precisa deste forçamento).
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
              Código ou tag não encontrada.
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
              <StatusChip tom={tomAptidao[recurso.statusAptidao] ?? 'info'}>{recurso.statusAptidao}</StatusChip>
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
