import { useEffect, useMemo, useState } from 'react';
import { Badge, Button, Field, Select, Text } from '@fluentui/react-components';
import { CheckmarkCircle24Regular, DismissCircle24Regular, PlayCircle24Regular } from '@fluentui/react-icons';
import {
  api,
  categoriaAlertaRotulo,
  severidadeAlertaLabel,
  statusAlertaLabel,
  StatusAlerta,
  SeveridadeAlerta,
  type Alerta,
  type Obra,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';

function severidadeCor(severidade: number) {
  if (severidade === SeveridadeAlerta.Critico) return designTokens.colorAlert;
  if (severidade === SeveridadeAlerta.Atencao) return designTokens.colorWarning;
  return designTokens.colorInfo;
}

export function AlertasDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [alertas, setAlertas] = useState<Alerta[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [processandoId, setProcessandoId] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [categoriaFiltro, setCategoriaFiltro] = useState('');
  const [severidadeFiltro, setSeveridadeFiltro] = useState('');

  async function carregar() {
    try {
      setErro(null);
      const [obrasResp, alertasResp] = await Promise.all([api.obras.listar(), api.alertas.listar({})]);
      setObras(obrasResp);
      setAlertas(alertasResp);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de alertas.');
    } finally {
      setCarregando(false);
    }
  }

  useEffect(() => {
    carregar();
  }, []);

  const categorias = useMemo(() => {
    const vistas = new Set(alertas.map((a) => a.entidadeOrigemTipo));
    return [...vistas].sort((a, b) => categoriaAlertaRotulo(a).localeCompare(categoriaAlertaRotulo(b)));
  }, [alertas]);

  const alertasFiltrados = useMemo(
    () =>
      alertas.filter(
        (a) =>
          (obraId === '' || a.obraId === obraId) &&
          (categoriaFiltro === '' || a.entidadeOrigemTipo === categoriaFiltro) &&
          (severidadeFiltro === '' || a.severidade === Number(severidadeFiltro)),
      ),
    [alertas, obraId, categoriaFiltro, severidadeFiltro],
  );

  const abertos = alertasFiltrados.filter((a) => a.status === StatusAlerta.Aberto).length;
  const emTratamento = alertasFiltrados.filter((a) => a.status === StatusAlerta.EmTratamento).length;
  const escalonados = alertasFiltrados.filter((a) => a.status === StatusAlerta.Escalonado).length;
  const resolvidos = alertasFiltrados.filter((a) => a.status === StatusAlerta.Resolvido).length;
  const ignorados = alertasFiltrados.filter((a) => a.status === StatusAlerta.Ignorado).length;

  const alertasAtivos = useMemo(
    () =>
      alertasFiltrados.filter(
        (a) =>
          a.status === StatusAlerta.Aberto ||
          a.status === StatusAlerta.EmTratamento ||
          a.status === StatusAlerta.Escalonado,
      ),
    [alertasFiltrados],
  );

  const severidadeDados: FatiaDonut[] = [
    { rotulo: 'Crítico', valor: alertasAtivos.filter((a) => a.severidade === SeveridadeAlerta.Critico).length, cor: designTokens.colorAlert },
    { rotulo: 'Atenção', valor: alertasAtivos.filter((a) => a.severidade === SeveridadeAlerta.Atencao).length, cor: designTokens.colorWarning },
    { rotulo: 'Informativo', valor: alertasAtivos.filter((a) => a.severidade === SeveridadeAlerta.Info).length, cor: designTokens.colorInfo },
  ];

  const categoriaDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const alerta of alertasAtivos) {
      contagem.set(alerta.entidadeOrigemTipo, (contagem.get(alerta.entidadeOrigemTipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({ rotulo: categoriaAlertaRotulo(tipo), valor, cor: designTokens.colorPrimary }))
      .sort((a, b) => b.valor - a.valor);
  }, [alertasAtivos]);

  const maisUrgentes = useMemo(
    () =>
      [...alertasAtivos].sort((a, b) => {
        if (b.severidade !== a.severidade) return b.severidade - a.severidade;
        const prazoA = a.dataLimiteTratamento ? new Date(a.dataLimiteTratamento).getTime() : Infinity;
        const prazoB = b.dataLimiteTratamento ? new Date(b.dataLimiteTratamento).getTime() : Infinity;
        return prazoA - prazoB;
      }),
    [alertasAtivos],
  );

  async function executar(acao: (id: string) => Promise<void>, id: string, mensagemErro: string) {
    try {
      setErro(null);
      setProcessandoId(id);
      await acao(id);
      await carregar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : mensagemErro);
    } finally {
      setProcessandoId(null);
    }
  }

  return (
    <div>
      <div className={estilos.filtros}>
        <Field label="Obra">
          <Select value={obraId} onChange={(_, data) => setObraId(data.value)}>
            <option value="">Todas as obras</option>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>
                {o.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Categoria">
          <Select value={categoriaFiltro} onChange={(_, data) => setCategoriaFiltro(data.value)}>
            <option value="">Todas as categorias</option>
            {categorias.map((tipo) => (
              <option key={tipo} value={tipo}>
                {categoriaAlertaRotulo(tipo)}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Severidade">
          <Select value={severidadeFiltro} onChange={(_, data) => setSeveridadeFiltro(data.value)}>
            <option value="">Todas as severidades</option>
            {Object.entries(severidadeAlertaLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
      </div>

      {erro && <Text className={estilosPagina.erro}>{erro}</Text>}

      <div style={{ marginBottom: 16 }}>
        <CardGrid>
          <KpiCard rotulo="Abertos" valor={abertos} cor={designTokens.colorAlert} />
          <KpiCard rotulo="Em tratamento" valor={emTratamento} cor={designTokens.colorWarning} />
          <KpiCard rotulo="Escalonados" valor={escalonados} cor={designTokens.colorPrimary} />
          <KpiCard rotulo="Resolvidos" valor={resolvidos} cor={designTokens.colorSuccess} />
          <KpiCard rotulo="Ignorados" valor={ignorados} cor={designTokens.colorInfo} />
        </CardGrid>
      </div>

      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Severidade dos alertas ativos</Text>
          <div className={estilos.chartSubtitulo}>Abertos, em tratamento e escalonados</div>
          <StatusDonutChart dados={severidadeDados} legendaCentral="alertas" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Alertas ativos por categoria</Text>
          <div className={estilos.chartSubtitulo}>Distribuição dos alertas ativos por módulo de origem</div>
          <RankingBarChart dados={categoriaDados} />
        </div>
      </div>

      <div className={estilosPagina.card} style={{ marginTop: 16 }}>
        <Text weight="semibold">Alertas mais urgentes</Text>
        {maisUrgentes.slice(0, 10).map((alerta) => (
          <div
            key={alerta.id}
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              gap: 12,
              padding: '8px 0',
              borderBottom: '1px solid var(--colorNeutralStroke2)',
            }}
          >
            <div style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                <Badge appearance="tint" style={{ backgroundColor: severidadeCor(alerta.severidade) + '22', color: severidadeCor(alerta.severidade) }}>
                  {severidadeAlertaLabel[alerta.severidade]}
                </Badge>
                <Text weight="semibold">{alerta.titulo}</Text>
              </div>
              <Text size={200}>
                {categoriaAlertaRotulo(alerta.entidadeOrigemTipo)} · {statusAlertaLabel[alerta.status]}
                {alerta.obraNome ? ` · ${alerta.obraNome}` : ''}
                {alerta.dataLimiteTratamento ? ` · prazo ${alerta.dataLimiteTratamento.slice(0, 10)}` : ''}
              </Text>
            </div>
            <div style={{ display: 'flex', gap: 4 }}>
              {alerta.status === StatusAlerta.Aberto && (
                <Button
                  appearance="subtle"
                  icon={<PlayCircle24Regular />}
                  title="Iniciar tratamento"
                  disabled={processandoId === alerta.id}
                  onClick={() => executar(api.alertas.iniciarTratamento, alerta.id, 'Falha ao iniciar tratamento.')}
                />
              )}
              <Button
                appearance="subtle"
                icon={<CheckmarkCircle24Regular />}
                title="Resolver"
                disabled={processandoId === alerta.id}
                onClick={() => executar(api.alertas.resolver, alerta.id, 'Falha ao resolver alerta.')}
              />
              <Button
                appearance="subtle"
                icon={<DismissCircle24Regular />}
                title="Ignorar"
                disabled={processandoId === alerta.id}
                onClick={() => executar(api.alertas.ignorar, alerta.id, 'Falha ao ignorar alerta.')}
              />
            </div>
          </div>
        ))}
        {!carregando && maisUrgentes.length === 0 && <Text>Nenhum alerta ativo para os filtros selecionados.</Text>}
      </div>
    </div>
  );
}
