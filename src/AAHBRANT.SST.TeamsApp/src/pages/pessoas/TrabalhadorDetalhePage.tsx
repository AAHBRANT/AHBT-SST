import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  Avatar,
  Button,
  Card,
  PageHeader,
  Abas,
  StatusChip,
  FeedbackInline,
  Carregando,
  Text,
  usePaletaGraficos,
  RankingBarChart,
  StatusDonutChart,
  type ItemRanking,
  type FatiaDonut,
  type Tom,
} from '@ui';
import { ArrowDownload24Regular, Eye24Regular, EyeOff24Regular } from '@fluentui/react-icons';
import { api, tipoVinculoLabel, type PerfilCompletoTrabalhador } from '../../lib/api';
import { formatarCpf, mascararCpf } from '../../lib/cpf';
import { PerfilGeralTab } from './PerfilGeralTab';
import { TreinamentosTab } from './TreinamentosTab';
import { RiscosTab } from './RiscosTab';
import { OcorrenciasTab } from './OcorrenciasTab';
import { CofreAssinaturasTab } from './CofreAssinaturasTab';

type AbaPerfil = 'geral' | 'epi' | 'treinamentos' | 'riscos' | 'ocorrencias' | 'cofre';

const ABAS_PERFIL = [
  { valor: 'geral', rotulo: 'Geral & ASO' },
  { valor: 'epi', rotulo: 'EPI & Matriz' },
  { valor: 'treinamentos', rotulo: 'Treinamentos & DDS' },
  { valor: 'riscos', rotulo: 'Riscos & OS' },
  { valor: 'ocorrencias', rotulo: 'Ocorrências' },
  { valor: 'cofre', rotulo: 'Cofre de Assinaturas' },
] as const satisfies readonly { valor: AbaPerfil; rotulo: string }[];

const tomAptidao: Record<string, Tom> = {
  Apto: 'ok',
  'Apto com restrição': 'atencao',
  Inapto: 'alerta',
};

// Ficha do trabalhador: cabeçalho fixo (foto, dados, aptidão) + seis abas de conteúdo. Sem fluxo de
// estados/workflow (a aptidão é só leitura, calculada a partir do ASO mais recente) — por isso
// PageHeader + Card empilhados em vez de DetailPageLayout, que existe para lateral fixa de ações de
// fluxo (ver NaoConformidadeDetalhePage). As abas são navegação interna da página, não da URL — não
// há `useAbaNaUrl` aqui de propósito, ela é reservada aos dois níveis da página-pilar (spec §4.1).
export function TrabalhadorDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const [aba, setAba] = useState<AbaPerfil>('geral');
  const [cpfVisivel, setCpfVisivel] = useState(false);
  const [perfil, setPerfil] = useState<PerfilCompletoTrabalhador | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [baixandoRelatorio, setBaixandoRelatorio] = useState(false);
  const [fotoUrl, setFotoUrl] = useState<string | null>(null);
  const paleta = usePaletaGraficos();

  async function carregar() {
    if (!id) return;
    try {
      setErro(null);
      setPerfil(await api.trabalhadores.obterPerfilCompleto(id));
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar perfil do funcionário.');
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
    return <FeedbackInline tom="erro">Funcionário não encontrado.</FeedbackInline>;
  }

  const dadosFrequenciaEpi: ItemRanking[] =
    perfil?.frequenciaTrocas.map((f) => ({ rotulo: f.catalogoEpiNome, valor: f.quantidadeTrocas })) ?? [];

  const dadosAssiduidadeDds: FatiaDonut[] = perfil
    ? [
        { rotulo: 'Participou', valor: perfil.assiduidadeDds.totalParticipados, cor: paleta.ok },
        {
          rotulo: 'Não participou',
          valor: Math.max(perfil.assiduidadeDds.totalRealizados - perfil.assiduidadeDds.totalParticipados, 0),
          cor: paleta.alerta,
        },
      ]
    : [];

  return (
    <div>
      <PageHeader
        voltarPara="/operacao/pessoas"
        rotuloVoltar="Pessoas"
        titulo={perfil?.nome ?? 'Carregando…'}
        subtitulo={
          perfil &&
          `Matrícula ${perfil.matricula} · ${perfil.obraNome} · ${perfil.funcaoNome} · Admissão em ${perfil.dataAdmissao?.slice(0, 10)}`
        }
        status={perfil && <StatusChip tom={tomAptidao[perfil.statusAptidao] ?? 'neutro'}>{perfil.statusAptidao}</StatusChip>}
        acoes={
          <Button
            appearance="primary"
            icon={<ArrowDownload24Regular />}
            onClick={baixarRelatorio}
            disabled={baixandoRelatorio || !perfil}
          >
            Emitir relatório de fiscalização (PDF)
          </Button>
        }
      />

      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      {!perfil ? (
        <Carregando variante="detalhe" linhas={6} />
      ) : (
        <>
          <div style={{ marginBottom: 16 }}>
            <Card densidade="compacta">
              <div style={{ display: 'flex', alignItems: 'center', gap: 16, flexWrap: 'wrap' }}>
                <Avatar
                  name={perfil.nome}
                  image={fotoUrl ? { src: fotoUrl } : undefined}
                  color="brand"
                  size={64}
                />
                <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap', alignItems: 'center' }}>
                  <StatusChip tom="neutro">{tipoVinculoLabel[perfil.vinculo]}</StatusChip>
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
              </div>
            </Card>
          </div>

          <div style={{ marginBottom: 16 }}>
            <Abas nivel="modulo" aria-label="Seções do perfil" abas={ABAS_PERFIL} valor={aba} aoMudar={setAba} />
          </div>

          {aba === 'geral' && <PerfilGeralTab perfil={perfil} />}
          {aba === 'epi' && (
            <Card titulo="Frequência de trocas por EPI">
              {dadosFrequenciaEpi.length === 0 ? (
                <Text>Sem dados de troca de EPI para exibir.</Text>
              ) : (
                <RankingBarChart dados={dadosFrequenciaEpi} corPadrao={paleta.marca} sufixo=" trocas" />
              )}
            </Card>
          )}
          {aba === 'treinamentos' && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
              <Card titulo="Assiduidade em DDS">
                {perfil.assiduidadeDds.totalRealizados === 0 ? (
                  <Text>Nenhum DDS realizado na obra desde a admissão.</Text>
                ) : (
                  <StatusDonutChart dados={dadosAssiduidadeDds} legendaCentral="DDS realizados" />
                )}
              </Card>
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
