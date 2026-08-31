import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Avatar,
  Badge,
  Button,
  Tab,
  TabList,
  Text,
  type PresenceBadgeStatus,
  type SelectTabData,
  type SelectTabEvent,
} from '@fluentui/react-components';
import { ArrowDownload24Regular, ArrowLeft24Regular, Eye24Regular, EyeOff24Regular } from '@fluentui/react-icons';
import { api, tipoVinculoLabel, type PerfilCompletoTrabalhador } from '../../lib/api';
import { formatarCpf, mascararCpf } from '../../lib/cpf';
import { usePageStyles, usePillTabStyles } from '../pageStyles';
import { RankingBarChart, type ItemRanking } from '../../components/dashboard/charts/RankingBarChart';
import { StatusDonutChart, type FatiaDonut } from '../../components/dashboard/charts/StatusDonutChart';
import { PerfilGeralTab } from './PerfilGeralTab';
import { TreinamentosTab } from './TreinamentosTab';
import { RiscosTab } from './RiscosTab';
import { OcorrenciasTab } from './OcorrenciasTab';
import { CofreAssinaturasTab } from './CofreAssinaturasTab';

type AbaPerfil = 'geral' | 'epi' | 'treinamentos' | 'riscos' | 'ocorrencias' | 'cofre';

const corAptidao: Record<string, 'success' | 'warning' | 'danger' | 'informative'> = {
  Apto: 'success',
  'Apto com restrição': 'warning',
  Inapto: 'danger',
};

const badgeAptidao: Record<string, PresenceBadgeStatus> = {
  Apto: 'available',
  'Apto com restrição': 'away',
  Inapto: 'busy',
};

export function TrabalhadorDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const estilosAba = usePillTabStyles();
  const [aba, setAba] = useState<AbaPerfil>('geral');
  const [cpfVisivel, setCpfVisivel] = useState(false);
  const [perfil, setPerfil] = useState<PerfilCompletoTrabalhador | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [baixandoRelatorio, setBaixandoRelatorio] = useState(false);
  const [fotoUrl, setFotoUrl] = useState<string | null>(null);

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setPerfil(await api.trabalhadores.obterPerfilCompleto(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar perfil do trabalhador.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  // Foto real do trabalhador — baixada sob demanda quando temFoto=true, mesmo padrão de
  // TrabalhadoresTab.tsx/ObrasPage.tsx. Sem foto, o Avatar cai para as iniciais do nome.
  useEffect(() => {
    if (!perfil?.temFoto) return;
    let cancelado = false;
    let urlCriada: string | null = null;
    (async () => {
      try {
        const blob = await api.trabalhadores.baixarFoto(perfil.id);
        if (cancelado) return;
        urlCriada = URL.createObjectURL(blob);
        setFotoUrl(urlCriada);
      } catch {
        // Falha ao carregar a foto não impede o uso da página; o trabalhador fica com iniciais.
      }
    })();
    return () => {
      cancelado = true;
      if (urlCriada) URL.revokeObjectURL(urlCriada);
    };
  }, [perfil?.id, perfil?.temFoto]);

  async function baixarRelatorio() {
    if (!id) return;
    try {
      setBaixandoRelatorio(true);
      setErro(null);
      const blob = await api.trabalhadores.baixarRelatorioFiscalizacao(id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `relatorio-fiscalizacao-${id}.pdf`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar o relatório de fiscalização.');
    } finally {
      setBaixandoRelatorio(false);
    }
  }

  if (!id) {
    return <Text>Trabalhador não encontrado.</Text>;
  }

  const dadosFrequenciaEpi: ItemRanking[] =
    perfil?.frequenciaTrocas.map((f) => ({ rotulo: f.catalogoEpiNome, valor: f.quantidadeTrocas })) ?? [];

  const dadosAssiduidadeDds: FatiaDonut[] = perfil
    ? [
        { rotulo: 'Participou', valor: perfil.assiduidadeDds.totalParticipados, cor: '#2E7D32' },
        {
          rotulo: 'Não participou',
          valor: Math.max(perfil.assiduidadeDds.totalRealizados - perfil.assiduidadeDds.totalParticipados, 0),
          cor: '#C62828',
        },
      ]
    : [];

  return (
    <div>
      <Button
        appearance="subtle"
        icon={<ArrowLeft24Regular />}
        onClick={() => navigate('/operacao/pessoas')}
        style={{ marginBottom: 12 }}
      >
        Voltar para Pessoas
      </Button>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div className={estilos.card} style={{ marginBottom: 16 }}>
        {perfil ? (
          <>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 8 }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <Avatar
                  name={perfil.nome}
                  image={fotoUrl ? { src: fotoUrl } : undefined}
                  color="brand"
                  size={64}
                  badge={{ status: badgeAptidao[perfil.statusAptidao] ?? 'unknown' }}
                />
                <Text size={500} weight="semibold">
                  {perfil.nome}
                </Text>
              </div>
              <Button
                appearance="primary"
                icon={<ArrowDownload24Regular />}
                onClick={baixarRelatorio}
                disabled={baixandoRelatorio}
              >
                Emitir relatório de fiscalização (PDF)
              </Button>
            </div>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Matrícula: {perfil.matricula}</Text>
              <Text>Obra: {perfil.obraNome}</Text>
              <Text>Função: {perfil.funcaoNome}</Text>
              <Text>Admissão: {perfil.dataAdmissao?.slice(0, 10)}</Text>
              <Badge appearance="tint">{tipoVinculoLabel[perfil.vinculo]}</Badge>
              <Badge color={corAptidao[perfil.statusAptidao] ?? 'informative'} appearance="tint">
                {perfil.statusAptidao}
              </Badge>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <Text>CPF: {cpfVisivel ? formatarCpf(perfil.cpf) : mascararCpf(perfil.cpf)}</Text>
                <Button
                  appearance="subtle"
                  size="small"
                  icon={cpfVisivel ? <EyeOff24Regular /> : <Eye24Regular />}
                  onClick={() => setCpfVisivel((v) => !v)}
                  aria-label={cpfVisivel ? 'Ocultar CPF' : 'Revelar CPF'}
                />
              </div>
              {perfil.rg && <Text>RG: {perfil.rg}</Text>}
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPerfil)}
        className={estilosAba.lista}
      >
        <Tab value="geral">Geral & ASO</Tab>
        <Tab value="epi">EPI & Matriz</Tab>
        <Tab value="treinamentos">Treinamentos & DDS</Tab>
        <Tab value="riscos">Riscos & OS</Tab>
        <Tab value="ocorrencias">Ocorrências</Tab>
        <Tab value="cofre">Cofre de Assinaturas</Tab>
      </TabList>

      {!perfil ? (
        <Text>Carregando...</Text>
      ) : (
        <>
          {aba === 'geral' && <PerfilGeralTab asos={perfil.asos} />}
          {aba === 'epi' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <div className={estilos.card}>
                <div className={estilos.toolbar}>
                  <Text weight="semibold">Frequência de trocas por EPI</Text>
                </div>
                {dadosFrequenciaEpi.length === 0 ? (
                  <Text>Sem dados de troca de EPI para exibir.</Text>
                ) : (
                  <RankingBarChart dados={dadosFrequenciaEpi} corPadrao="#7B1E2B" sufixo=" trocas" />
                )}
              </div>
            </div>
          )}
          {aba === 'treinamentos' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <div className={estilos.card}>
                <div className={estilos.toolbar}>
                  <Text weight="semibold">Assiduidade em DDS</Text>
                </div>
                {perfil.assiduidadeDds.totalRealizados === 0 ? (
                  <Text>Nenhum DDS realizado na obra desde a admissão.</Text>
                ) : (
                  <StatusDonutChart dados={dadosAssiduidadeDds} legendaCentral="DDS realizados" />
                )}
              </div>
              <TreinamentosTab trabalhadorId={id} />
            </div>
          )}
          {aba === 'riscos' && <RiscosTab riscos={perfil.riscos} />}
          {aba === 'ocorrencias' && <OcorrenciasTab ocorrencias={perfil.ocorrencias} />}
          {aba === 'cofre' && <CofreAssinaturasTab trabalhadorId={id} assinaturas={perfil.assinaturas} />}
        </>
      )}
    </div>
  );
}
