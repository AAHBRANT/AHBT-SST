import { useEffect, useMemo, useState } from 'react';
import {
  Card,
  EstadoVazio,
  Field,
  FeedbackInline,
  KpiCard,
  RankingBarChart,
  Select,
  StatusDonutChart,
  usePaletaGraficos,
  type FatiaDonut,
  type ItemRanking,
} from '@ui';
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
import { AreasBloqueadasPanel, type AreaComContexto } from './AreasBloqueadasPanel';
import { TagsPerdidasPanel } from './TagsPerdidasPanel';

// Onda 2 Task 9 (camada ui/): dashboard de Identificação — KpiCard + gráficos com cores lidas de
// usePaletaGraficos (paleta.ts, spec §1.6), grade CSS Grid simples (spec §4.4, sem componente de
// grade próprio), mesmo padrão de AprDashboardTab.tsx (Task 11). A lógica de agregação no cliente
// não muda nesta frente.
export function IdentificacaoDashboardTab() {
  const paleta = usePaletaGraficos();

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
    { rotulo: 'Ativa', valor: areasFiltradas.filter((a) => a.status === StatusArea.Ativa).length, cor: paleta.ok },
    { rotulo: 'Inativa', valor: areasFiltradas.filter((a) => a.status === StatusArea.Inativa).length, cor: paleta.info },
    { rotulo: 'Bloqueada', valor: areasBloqueadas, cor: paleta.alerta },
  ];

  const tipoAreaDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const area of areasFiltradas) {
      contagem.set(area.tipo, (contagem.get(area.tipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({ rotulo: tipoAreaLabel[tipo] ?? String(tipo), valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor);
  }, [areasFiltradas, paleta.marca]);

  const obraAreaDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<string, number>();
    for (const area of areasComContexto) {
      contagem.set(area.obraNome, (contagem.get(area.obraNome) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([rotulo, valor]) => ({ rotulo, valor, cor: paleta.marca }))
      .sort((a, b) => b.valor - a.valor)
      .slice(0, 5);
  }, [areasComContexto, paleta.marca]);

  const statusTagDados: FatiaDonut[] = [
    { rotulo: 'Disponível', valor: tagsFiltradas.filter((t) => t.status === StatusTag.Disponivel).length, cor: paleta.ok },
    { rotulo: 'Vinculada', valor: tagsFiltradas.filter((t) => t.status === StatusTag.Vinculada).length, cor: paleta.info },
    { rotulo: 'Desativada', valor: tagsFiltradas.filter((t) => t.status === StatusTag.Desativada).length, cor: paleta.atencao },
    { rotulo: 'Perdida', valor: tagsPerdidas.length, cor: paleta.alerta },
  ];

  const tipoTagDados: ItemRanking[] = useMemo(() => {
    const contagem = new Map<number, number>();
    for (const tag of tagsFiltradas) {
      contagem.set(tag.tipo, (contagem.get(tag.tipo) ?? 0) + 1);
    }
    return [...contagem.entries()]
      .map(([tipo, valor]) => ({ rotulo: tipoTagLabel[tipo] ?? String(tipo), valor, cor: paleta.info }))
      .sort((a, b) => b.valor - a.valor);
  }, [tagsFiltradas, paleta.info]);

  return (
    <div>
      <div style={{ display: 'flex', gap: 16, marginBottom: 16, flexWrap: 'wrap' }}>
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

      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(185px, 1fr))', gap: 16, marginBottom: 16 }}>
        <KpiCard rotulo="Total de áreas" valor={areasFiltradas.length} tom="info" indice={0} icone={<Location24Regular />} />
        <KpiCard rotulo="Áreas bloqueadas" valor={areasBloqueadas} tom="alerta" indice={1} icone={<LockClosed24Regular />} />
        <KpiCard rotulo="Total de tags" valor={tagsFiltradas.length} tom="info" indice={2} icone={<ScanObject24Regular />} />
        <KpiCard rotulo="Tags perdidas" valor={tagsPerdidas.length} tom="alerta" indice={3} icone={<Search24Regular />} />
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status das áreas" subtitulo="Situação atual de cada área de SST cadastrada">
          <StatusDonutChart dados={statusAreaDados} legendaCentral="Áreas" />
        </Card>
        <Card titulo="Áreas por tipo" subtitulo="Distribuição entre área de trabalho, zona de risco e armazenamento">
          <RankingBarChart dados={tipoAreaDados} />
        </Card>
        <Card titulo="Áreas por obra" subtitulo="Top 5 obras com mais áreas cadastradas">
          <RankingBarChart dados={obraAreaDados} />
        </Card>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(340px, 1fr))', gap: 16, marginBottom: 16 }}>
        <Card titulo="Status das tags" subtitulo="Situação atual de cada tag de identificação cadastrada">
          <StatusDonutChart dados={statusTagDados} legendaCentral="Tags" />
        </Card>
        <Card titulo="Tags por tipo" subtitulo="Distribuição entre NTAG215, NTAG213, QR Code e RFID">
          <RankingBarChart dados={tipoTagDados} />
        </Card>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
        <AreasBloqueadasPanel areas={areasComContexto} />
        <TagsPerdidasPanel tags={tagsPerdidas} />
      </div>

      {!carregando && areasFiltradas.length === 0 && tagsFiltradas.length === 0 && (
        <div style={{ marginTop: 16 }}>
          <EstadoVazio
            titulo="Nenhuma área ou tag encontrada"
            descricao="Ajuste os filtros de obra ou status para ver resultados."
          />
        </div>
      )}
    </div>
  );
}
