import { useMemo } from 'react';
import {
  BarraProgresso,
  Card,
  Legenda,
  RankingBarChart,
  StatusChip,
  StatusDonutChart,
  Text,
  designTokens,
  nivelVencimento,
  rotuloDeVencimento,
  tokensUi,
  tomDeVencimento,
  usePaletaGraficos,
  type FatiaDonut,
  type ItemRanking,
} from '@ui';
import { ShieldCheckmark20Regular } from '@fluentui/react-icons';
import { motivoEntregaEpiLabel, type PerfilCompletoTrabalhador } from '../../lib/api';

function diasAte(data: string): number {
  return Math.round((new Date(data).getTime() - new Date(new Date().toDateString()).getTime()) / 86_400_000);
}

function dataBr(iso?: string | null): string {
  return iso ? iso.slice(0, 10).split('-').reverse().join('/') : '—';
}

function textoPrazo(dias: number): string {
  if (dias < 0) return `vencido há ${-dias} ${-dias === 1 ? 'dia' : 'dias'}`;
  if (dias === 0) return 'vence hoje';
  return `faltam ${dias} ${dias === 1 ? 'dia' : 'dias'}`;
}

// Fração da validade já consumida desde a entrega (0 a 1).
function consumoValidade(inicio: string, fim: string): number {
  const i = new Date(inicio).getTime();
  const f = new Date(fim).getTime();
  if (!(f > i)) return 1;
  return Math.max(0, Math.min(1, (Date.now() - i) / (f - i)));
}

// Aba EPI do perfil — Proposta 1 de cards (21/10): situação dos itens em posse, motivos e frequência
// de trocas lado a lado, e a lista de EPIs em posse com barra de prazo (mesma regra de 30 dias do
// PR-SST-003 usada no resto do sistema). O termo de compromisso continua em TermoCompromissoEpiCartao.
export function EpiPerfilSecao({ perfil }: { perfil: PerfilCompletoTrabalhador }) {
  const paleta = usePaletaGraficos();

  // O perfil não traz o nome do catálogo em episAtivos; frequenciaTrocas traz, e todo EPI ativo
  // passou por pelo menos uma entrega.
  const nomePorCatalogo = useMemo(
    () => new Map(perfil.frequenciaTrocas.map((f) => [f.catalogoEpiId, f.catalogoEpiNome])),
    [perfil.frequenciaTrocas],
  );

  const situacao = useMemo(() => {
    let emDia = 0;
    let vencendo = 0;
    let vencido = 0;
    for (const epi of perfil.episAtivos) {
      const nivel = nivelVencimento(epi.dataValidade);
      if (nivel === 'vencido') vencido++;
      else if (nivel === 'alerta') vencendo++;
      else emDia++;
    }
    return { emDia, vencendo, vencido };
  }, [perfil.episAtivos]);

  const dadosDonut: FatiaDonut[] = [
    { rotulo: 'Em dia', valor: situacao.emDia, cor: paleta.ok },
    { rotulo: 'Vence em 30 dias', valor: situacao.vencendo, cor: paleta.atencao },
    { rotulo: 'Vencido', valor: situacao.vencido, cor: paleta.alerta },
  ];

  const episOrdenados = useMemo(
    () =>
      [...perfil.episAtivos].sort((a, b) => (a.dataValidade ?? '9999').localeCompare(b.dataValidade ?? '9999')),
    [perfil.episAtivos],
  );

  const motivosTroca = perfil.motivosTroca ?? [];
  const totalMotivos = motivosTroca.reduce((soma, m) => soma + m.quantidade, 0);
  const dadosFrequencia: ItemRanking[] = perfil.frequenciaTrocas.map((f) => ({ rotulo: f.catalogoEpiNome, valor: f.quantidadeTrocas }));

  return (
    <>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: tokensUi.espaco.lg }}>
        <Card titulo="Situação dos EPIs" subtitulo="Validade dos itens em posse">
          {perfil.episAtivos.length === 0 ? (
            <Text>Nenhum EPI em posse.</Text>
          ) : (
            <StatusDonutChart dados={dadosDonut} legendaCentral="EPIs em posse" />
          )}
        </Card>

        <Card titulo="Motivo das trocas" subtitulo={`Reposições de EPI em ${new Date().getFullYear()}`}>
          {motivosTroca.length === 0 ? (
            <Text>Nenhuma troca registrada este ano.</Text>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: tokensUi.espaco.md }}>
              {motivosTroca.map((m) => {
                const pct = totalMotivos > 0 ? Math.round((m.quantidade / totalMotivos) * 100) : 0;
                return (
                  <div key={m.motivo}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6 }}>
                      <Text size={200} weight="semibold">
                        {motivoEntregaEpiLabel[m.motivo]}
                      </Text>
                      <Legenda>
                        {pct}% ({m.quantidade})
                      </Legenda>
                    </div>
                    <BarraProgresso percentual={pct} tom="atencao" aria-label={`${motivoEntregaEpiLabel[m.motivo]}, ${pct}%`} />
                  </div>
                );
              })}
            </div>
          )}
        </Card>
      </div>

      <Card titulo="EPIs em posse" subtitulo="Ordenados pela validade">
        {episOrdenados.length === 0 ? (
          <Text>Nenhum EPI em posse.</Text>
        ) : (
          <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
            {episOrdenados.map((epi, i) => {
              const nivel = nivelVencimento(epi.dataValidade);
              const tom = nivel ? tomDeVencimento(nivel) : 'neutro';
              const dias = epi.dataValidade ? diasAte(epi.dataValidade) : null;
              const nome = nomePorCatalogo.get(epi.catalogoEpiId) ?? 'EPI';
              return (
                <li
                  key={epi.id}
                  style={{
                    display: 'grid',
                    gridTemplateColumns: 'auto minmax(0, 1fr) auto',
                    gap: tokensUi.espaco.md,
                    alignItems: 'center',
                    padding: '11px 0',
                    borderTop: i === 0 ? 'none' : `1px solid ${designTokens.colorCardBorder}`,
                  }}
                >
                  <span
                    aria-hidden="true"
                    style={{
                      width: 32,
                      height: 32,
                      borderRadius: tokensUi.raio.sm,
                      display: 'grid',
                      placeItems: 'center',
                      backgroundColor: tokensUi.status[tom].fundo,
                      color: tokensUi.status[tom].tinta,
                    }}
                  >
                    <ShieldCheckmark20Regular />
                  </span>
                  <div style={{ display: 'flex', flexDirection: 'column', gap: 6, minWidth: 0 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', gap: tokensUi.espaco.sm, flexWrap: 'wrap' }}>
                      <Text weight="semibold">{nome}</Text>
                      <Legenda>
                        Entregue em {dataBr(epi.dataEntrega)}
                        {dias !== null ? ` · ${textoPrazo(dias)}` : ' · sem validade definida'}
                      </Legenda>
                    </div>
                    {epi.dataValidade && (
                      <BarraProgresso
                        percentual={consumoValidade(epi.dataEntrega, epi.dataValidade) * 100}
                        tom={tom}
                        aria-label={`${nome}: ${dias !== null ? textoPrazo(dias) : ''}`}
                      />
                    )}
                  </div>
                  <StatusChip tom={tom}>{nivel ? rotuloDeVencimento(nivel) : 'Sem validade'}</StatusChip>
                </li>
              );
            })}
          </ul>
        )}
      </Card>

      <Card titulo="Frequência de trocas por EPI" subtitulo="Todas as entregas desde a admissão">
        {dadosFrequencia.length === 0 ? (
          <Text>Sem dados de troca de EPI para exibir.</Text>
        ) : (
          <RankingBarChart dados={dadosFrequencia} corPadrao={paleta.marca} sufixo=" trocas" />
        )}
      </Card>
    </>
  );
}
