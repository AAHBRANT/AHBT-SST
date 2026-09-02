import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Badge, Button, Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { ArrowLeft24Regular } from '@fluentui/react-icons';
import { api, statusPgrLabel, type Obra, type PgrDetalhe } from '../../lib/api';
import { usePageStyles, usePillTabStyles } from '../pageStyles';
import { InventarioTab } from './InventarioTab';
import { PlanoAcaoTab } from './PlanoAcaoTab';
import { PgrRevisoesTab } from './PgrRevisoesTab';

type AbaPgr = 'inventario' | 'planoAcao' | 'revisoes';

export function PgrDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const estilosAba = usePillTabStyles();
  const [aba, setAba] = useState<AbaPgr>('inventario');
  const [detalhe, setDetalhe] = useState<PgrDetalhe | null>(null);
  const [obras, setObras] = useState<Obra[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    async function carregar() {
      if (!id) return;
      try {
        setErro(null);
        const [det, obrs] = await Promise.all([api.pgrs.obterDetalhe(id), api.obras.listar()]);
        setDetalhe(det);
        setObras(obrs);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar PGR.');
      }
    }
    carregar();
  }, [id]);

  function nomeObra(obraId: string) {
    return obras.find((o) => o.id === obraId)?.nome ?? obraId;
  }

  if (!id) {
    return <Text>PGR não encontrado.</Text>;
  }

  const riscosDisponiveis = detalhe?.atividades.flatMap((a) => a.riscos) ?? [];

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/prevencao/pgr')}
        style={{ marginBottom: 12 }}
      >
        Voltar para PGR
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {detalhe ? (
          <>
            <Text size={500} weight="semibold">
              {detalhe.pgr.nome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Obra: {nomeObra(detalhe.pgr.obraId)}</Text>
              <Text>Elaboração: {detalhe.pgr.dataElaboracao?.slice(0, 10)}</Text>
              {detalhe.pgr.dataProximaRevisao && (
                <Text>Próxima revisão: {detalhe.pgr.dataProximaRevisao.slice(0, 10)}</Text>
              )}
              {detalhe.pgr.dataTermino && <Text>Término: {detalhe.pgr.dataTermino.slice(0, 10)}</Text>}
              <Badge appearance="tint">{statusPgrLabel[detalhe.pgr.status]}</Badge>
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPgr)}
        className={estilosAba.lista}
      >
        <Tab value="inventario">Inventário de riscos</Tab>
        <Tab value="planoAcao">Plano de ação</Tab>
        <Tab value="revisoes">Revisões</Tab>
      </TabList>

      {aba === 'inventario' && <InventarioTab atividades={detalhe?.atividades ?? []} />}
      {aba === 'planoAcao' && <PlanoAcaoTab pgrId={id} riscosDisponiveis={riscosDisponiveis} />}
      {aba === 'revisoes' && <PgrRevisoesTab pgrId={id} />}
    </div>
  );
}
