import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Avatar, Button, Card, designTokens, Legenda, Spinner, StatusChip, Text, type Tom } from '@ui';
import { ArrowDownload24Regular, ShieldError24Regular, Signature24Regular } from '@fluentui/react-icons';
import {
  api,
  motivoEntregaEpiLabel,
  StatusArea,
  tipoAreaLabel,
  statusAreaLabel,
  type CatalogoEpi,
  type EntregaEpi,
  type PerfilCompletoTrabalhador,
  type RecursoPublico,
} from '../../lib/api';
import { useDashboardStyles } from '../../components/dashboard/dashboardStyles';
import { StatusDonutChart, type FatiaDonut } from '../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../components/dashboard/charts/RankingBarChart';
import { usePaletaGraficos } from '../../ui/graficos';

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

export function IdentificacaoPublicaPage() {
  const { codigoOuUid } = useParams<{ codigoOuUid: string }>();
  const navigate = useNavigate();
  const dashEstilos = useDashboardStyles();
  const paleta = usePaletaGraficos();
  const [recurso, setRecurso] = useState<RecursoPublico | null>(null);
  const [fotoUrl, setFotoUrl] = useState<string | null>(null);
  const [entregasEpi, setEntregasEpi] = useState<EntregaEpi[]>([]);
  const [catalogosEpi, setCatalogosEpi] = useState<CatalogoEpi[]>([]);
  const [perfilCompleto, setPerfilCompleto] = useState<PerfilCompletoTrabalhador | null>(null);
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
    if (recurso?.tipoRecurso !== 'trabalhador') {
      setEntregasEpi([]);
      setCatalogosEpi([]);
      setPerfilCompleto(null);
      return;
    }

    let cancelado = false;
    (async () => {
      try {
        const [entregas, catalogos, perfil] = await Promise.all([
          api.entregasEpi.listar(recurso.trabalhadorId),
          api.catalogosEpi.listar(),
          api.trabalhadores.obterPerfilCompleto(recurso.trabalhadorId),
        ]);
        if (cancelado) return;
        setEntregasEpi(entregas);
        setCatalogosEpi(catalogos);
        setPerfilCompleto(perfil);
      } catch {
        if (cancelado) return;
        setEntregasEpi([]);
        setCatalogosEpi([]);
        setPerfilCompleto(null);
      }
    })();

    return () => {
      cancelado = true;
    };
  }, [recurso]);

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

  async function baixarFichaEpi(trabalhadorId: string) {
    const blob = await api.entregasEpi.baixarFichaTrabalhador(trabalhadorId);
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `ficha-epi-${trabalhadorId}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  function nomeCatalogoEpi(catalogoEpiId: string) {
    return catalogosEpi.find((epi) => epi.id === catalogoEpiId)?.nome ?? catalogoEpiId;
  }

  const dadosDonutEpi: FatiaDonut[] = useMemo(() => {
    const epis = perfilCompleto?.episAtivos ?? [];
    let emDia = 0;
    let vencendo = 0;
    let vencido = 0;
    for (const epi of epis) {
      const status = statusEpi(epi.dataValidade);
      if (status.tom === 'alerta') vencido++;
      else if (status.tom === 'atencao') vencendo++;
      else emDia++;
    }
    return [
      { rotulo: 'Em dia', valor: emDia, cor: paleta.ok },
      { rotulo: 'Vencendo', valor: vencendo, cor: paleta.atencao },
      { rotulo: 'Vencido', valor: vencido, cor: paleta.alerta },
    ];
  }, [paleta.alerta, paleta.atencao, paleta.ok, perfilCompleto?.episAtivos]);

  const dadosFrequenciaEpi: ItemRanking[] = useMemo(
    () =>
      perfilCompleto?.frequenciaTrocas.map((f) => ({
        rotulo: f.catalogoEpiNome,
        valor: f.quantidadeTrocas,
      })) ?? [],
    [perfilCompleto?.frequenciaTrocas],
  );

  const dadosAssiduidadeDds: FatiaDonut[] = useMemo(
    () =>
      perfilCompleto
        ? [
            { rotulo: 'Participou', valor: perfilCompleto.assiduidadeDds.totalParticipados, cor: paleta.ok },
            {
              rotulo: 'Não participou',
              valor: Math.max(
                perfilCompleto.assiduidadeDds.totalRealizados - perfilCompleto.assiduidadeDds.totalParticipados,
                0,
              ),
              cor: paleta.alerta,
            },
          ]
        : [],
    [paleta.alerta, paleta.ok, perfilCompleto],
  );

  return (
    <div style={raiz} data-theme="light">
      <div style={{ width: '100%', maxWidth: recurso?.tipoRecurso === 'trabalhador' ? 920 : 480 }}>
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

            {entregasEpi.length > 0 && (
              <div style={{ marginTop: 16 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8, alignItems: 'center' }}>
                  <Text weight="semibold">Entregas de EPI</Text>
                  <Button size="small" icon={<ArrowDownload24Regular />} onClick={() => baixarFichaEpi(recurso.trabalhadorId)}>
                    Ficha
                  </Button>
                </div>

                {entregasEpi.slice(0, 6).map((entrega) => {
                  const devolvido = entrega.dataDevolucao != null;
                  return (
                    <div key={entrega.id} style={linhaItem}>
                      <div>
                        <Text weight="semibold" size={200} style={{ display: 'block' }}>
                          {nomeCatalogoEpi(entrega.catalogoEpiId)}
                        </Text>
                        <Legenda>
                          {entrega.quantidade} un. · Entregue em {entrega.dataEntrega.slice(0, 10)}
                        </Legenda>
                      </div>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        <StatusChip tom={devolvido ? 'info' : 'ok'}>{devolvido ? 'Devolvido' : 'Ativo'}</StatusChip>
                        {!devolvido && (
                          <Button
                            appearance="subtle"
                            icon={<Signature24Regular />}
                            onClick={() => navigate(`/epi/${entrega.id}/assinar`)}
                            aria-label="Assinar entrega"
                          />
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
            )}

            <div style={{ marginTop: 16 }}>
              <Text weight="semibold">Gráficos do perfil</Text>
              <div className={dashEstilos.chartRow} style={{ marginTop: 12 }}>
                <div className={dashEstilos.chartCard}>
                  <Text className={dashEstilos.chartTitulo}>Status dos EPIs</Text>
                  <div className={dashEstilos.chartSubtitulo}>Validade dos itens em posse do funcionário</div>
                  {!perfilCompleto || perfilCompleto.episAtivos.length === 0 ? (
                    <Text>Nenhum EPI ativo alimentado ainda.</Text>
                  ) : (
                    <StatusDonutChart dados={dadosDonutEpi} legendaCentral="EPIs ativos" />
                  )}
                </div>

                <div className={dashEstilos.chartCard}>
                  <Text className={dashEstilos.chartTitulo}>Frequência de trocas por EPI</Text>
                  <div className={dashEstilos.chartSubtitulo}>Histórico de trocas registradas por item</div>
                  {dadosFrequenciaEpi.length === 0 ? (
                    <Text>Sem dados de troca de EPI alimentados ainda.</Text>
                  ) : (
                    <RankingBarChart dados={dadosFrequenciaEpi} corPadrao={paleta.marca} sufixo=" trocas" />
                  )}
                </div>

                <div className={dashEstilos.chartCard}>
                  <Text className={dashEstilos.chartTitulo}>Assiduidade em DDS</Text>
                  <div className={dashEstilos.chartSubtitulo}>Participações do funcionário nos DDS da obra</div>
                  {!perfilCompleto || perfilCompleto.assiduidadeDds.totalRealizados === 0 ? (
                    <Text>Nenhum DDS realizado/alimentado ainda.</Text>
                  ) : (
                    <StatusDonutChart dados={dadosAssiduidadeDds} legendaCentral="DDS realizados" />
                  )}
                </div>

                <div className={dashEstilos.chartCard}>
                  <Text className={dashEstilos.chartTitulo}>Motivo das trocas no ano</Text>
                  <div className={dashEstilos.chartSubtitulo}>Reposições classificadas por motivo</div>
                  {!perfilCompleto || perfilCompleto.motivosTroca.length === 0 ? (
                    <Text>Nenhuma troca classificada alimentada ainda.</Text>
                  ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
                      {perfilCompleto.motivosTroca.map((m) => (
                        <div key={m.motivo} style={linhaItem}>
                          <Text>{motivoEntregaEpiLabel[m.motivo]}</Text>
                          <StatusChip tom="info">{m.quantidade}</StatusChip>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </div>
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
