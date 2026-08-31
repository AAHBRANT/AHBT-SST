import { useEffect, useMemo, useState } from 'react';
import { Field, Select, Text } from '@fluentui/react-components';
import { Location24Regular, LockClosed24Regular, ScanObject24Regular, Search24Regular } from '@fluentui/react-icons';
import {
  api,
  statusAreaLabel,
  StatusArea,
  statusTagLabel,
  StatusTag,
  tipoAreaLabel,
  tipoTagLabel,
  type AreaSst,
  type Obra,
  type TagIdentificacao,
} from '../../../lib/api';
import { CardGrid } from '../../../layout/AppShell';
import { designTokens } from '../../../theme';
import { usePageStyles } from '../../pageStyles';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';
import { KpiCard } from '../../../components/dashboard/KpiCard';
import { StatusDonutChart, type FatiaDonut } from '../../../components/dashboard/charts/StatusDonutChart';
import { RankingBarChart, type ItemRanking } from '../../../components/dashboard/charts/RankingBarChart';
import { AreasBloqueadasPanel, type AreaComContexto } from './AreasBloqueadasPanel';
import { TagsPerdidasPanel } from './TagsPerdidasPanel';

export function IdentificacaoDashboardTab() {
  const estilosPagina = usePageStyles();
  const estilos = useDashboardStyles();

  const [obras, setObras] = useState<Obra[]>([]);
  const [areas, setAreas] = useState<AreaSst[]>([]);
  const [tags, setTags] = useState<TagIdentificacao[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);

  const [obraId, setObraId] = useState('');
  const [statusAreaFiltro, setStatusAreaFiltro] = useState('');
  const [statusTagFiltro, setStatusTagFiltro] = useState('');

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [obrasResp, areasResp, tagsResp] = await Promise.all([
          api.obras.listar(),
          api.areasSst.listar(),
          api.tagsIdentificacao.listar(),
        ]);
        setObras(obrasResp);
        setAreas(areasResp);
        setTags(tagsResp);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar dados do dashboard de Identificação.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }

  const areasFiltradas = useMemo(
    () =>
      areas.filter(
        (a) =>
          (obraId === '' || a.obraId === obraId) &&
          (statusAreaFiltro === '' || a.status === Number(statusAreaFiltro)),
      ),
    [areas, obraId, statusAreaFiltro],
  );

  const areasComContexto: AreaComContexto[] = useMemo(
    () => areasFiltradas.map((area) => ({ ...area, obraNome: nomeObra(area.obraId) })),
    [areasFiltradas, obras],
  );

  const tagsFiltradas = useMemo(
    () => tags.filter((t) => statusTagFiltro === '' || t.status === Number(statusTagFiltro)),
    [tags, statusTagFiltro],
  );

  const areasBloqueadas = areasFiltradas.filter((a) => a.status === StatusArea.Bloqueada).length;
  const tagsPerdidas = tagsFiltradas.filter((t) => t.status === StatusTag.Perdida);

  const statusAreaDados: FatiaDonut[] = [
    {
      rotulo: 'Ativa',
      valor: areasFiltradas.filter((a) => a.status === StatusArea.Ativa).length,
      cor: designTokens.colorSuccess,
    },
    {
      rotulo: 'Inativa',
      valor: areasFiltradas.filter((a) => a.status === StatusArea.Inativa).length,
      cor: designTokens.colorInfo,
    },
    { rotulo: 'Bloqueada', valor: areasBloqueadas, cor: designTokens.colorAlert },
  ];

  const tipoAreaDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const area of areasFiltradas) {
      contagem.set(area.tipo, (contagem.get(area.tipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({ rotulo: tipoAreaLabel[tipo] ?? String(tipo), valor, cor: designTokens.colorPrimary }))
      .sort((a, b) => b.valor - a.valor);
  }, [areasFiltradas]);

  const obraAreaDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const area of areasComContexto) {
      contagem.set(area.obraNome, (contagem.get(area.obraNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: designTokens.colorPrimary }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [areasComContexto]);

  const statusTagDados: FatiaDonut[] = [
    {
      rotulo: 'Disponível',
      valor: tagsFiltradas.filter((t) => t.status === StatusTag.Disponivel).length,
      cor: designTokens.colorSuccess,
    },
    {
      rotulo: 'Vinculada',
      valor: tagsFiltradas.filter((t) => t.status === StatusTag.Vinculada).length,
      cor: designTokens.colorInfo,
    },
    {
      rotulo: 'Desativada',
      valor: tagsFiltradas.filter((t) => t.status === StatusTag.Desativada).length,
      cor: designTokens.colorWarning,
    },
    { rotulo: 'Perdida', valor: tagsPerdidas.length, cor: designTokens.colorAlert },
  ];

  const tipoTagDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const tag of tagsFiltradas) {
      contagem.set(tag.tipo, (contagem.get(tag.tipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({ rotulo: tipoTagLabel[tipo] ?? String(tipo), valor, cor: designTokens.colorInfo }))
      .sort((a, b) => b.valor - a.valor);
  }, [tagsFiltradas]);

  return (
    <div>
      <div className={estilos.filtros}>
        <Field label="Obra (áreas)">
          <Select value={obraId} onChange={(_, data) => setObraId(data.value)}>
            <option value="">Todas as obras</option>
            {obras.map((o) => (
              <option key={o.id} value={o.id}>
                {o.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Status da área">
          <Select value={statusAreaFiltro} onChange={(_, data) => setStatusAreaFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusAreaLabel).map(([valor, rotulo]) => (
              <option key={valor} value={valor}>
                {rotulo}
              </option>
            ))}
          </Select>
        </Field>
        <Field label="Status da tag">
          <Select value={statusTagFiltro} onChange={(_, data) => setStatusTagFiltro(data.value)}>
            <option value="">Todos os status</option>
            {Object.entries(statusTagLabel).map(([valor, rotulo]) => (
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
          <KpiCard
            rotulo="Total de áreas"
            valor={areasFiltradas.length}
            cor={designTokens.colorPrimary}
            icone={<Location24Regular />}
          />
          <KpiCard
            rotulo="Áreas bloqueadas"
            valor={areasBloqueadas}
            cor={designTokens.colorAlert}
            icone={<LockClosed24Regular />}
          />
          <KpiCard
            rotulo="Total de tags"
            valor={tagsFiltradas.length}
            cor={designTokens.colorPrimary}
            icone={<ScanObject24Regular />}
          />
          <KpiCard
            rotulo="Tags perdidas"
            valor={tagsPerdidas.length}
            cor={designTokens.colorAlert}
            icone={<Search24Regular />}
          />
        </CardGrid>
      </div>

      <div style={{ marginBottom: 8 }}>
        <Text weight="semibold">Áreas</Text>
      </div>
      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status das áreas</Text>
          <div className={estilos.chartSubtitulo}>Situação atual de cada área de SST cadastrada</div>
          <StatusDonutChart dados={statusAreaDados} legendaCentral="Áreas" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Áreas por tipo</Text>
          <div className={estilos.chartSubtitulo}>Distribuição entre área de trabalho, zona de risco e armazenamento</div>
          <RankingBarChart dados={tipoAreaDados} />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Áreas por obra</Text>
          <div className={estilos.chartSubtitulo}>Top 5 obras com mais áreas cadastradas</div>
          <RankingBarChart dados={obraAreaDados} />
        </div>
      </div>

      <div style={{ marginBottom: 8, marginTop: 16 }}>
        <Text weight="semibold">Tags (NTAG/QR/RFID)</Text>
      </div>
      <div className={estilos.chartRow}>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Status das tags</Text>
          <div className={estilos.chartSubtitulo}>Situação atual de cada tag de identificação cadastrada</div>
          <StatusDonutChart dados={statusTagDados} legendaCentral="Tags" />
        </div>
        <div className={estilos.chartCard}>
          <Text className={estilos.chartTitulo}>Tags por tipo</Text>
          <div className={estilos.chartSubtitulo}>Distribuição entre NTAG215, NTAG213, QR Code e RFID</div>
          <RankingBarChart dados={tipoTagDados} />
        </div>
      </div>

      <AreasBloqueadasPanel areas={areasComContexto} />
      <div style={{ marginTop: 16 }}>
        <TagsPerdidasPanel tags={tagsPerdidas} />
      </div>

      {!carregando && areasFiltradas.length === 0 && tagsFiltradas.length === 0 && (
        <div className={estilosPagina.card} style={{ marginTop: 16 }}>
          <Text>Nenhuma área ou tag encontrada para os filtros selecionados.</Text>
        </div>
      )}
    </div>
  );
}
