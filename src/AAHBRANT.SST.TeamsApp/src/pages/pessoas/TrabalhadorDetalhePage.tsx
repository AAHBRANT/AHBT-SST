import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { Badge, Button, Tab, TabList, Text, type SelectTabData, type SelectTabEvent } from '@fluentui/react-components';
import { ArrowLeft24Regular, Eye24Regular, EyeOff24Regular } from '@fluentui/react-icons';
import { api, tipoVinculoLabel, type Funcao, type Obra, type Trabalhador } from '../../lib/api';
import { formatarCpf, mascararCpf } from '../../lib/cpf';
import { usePageStyles } from '../pageStyles';
import { AsoTab } from './AsoTab';
import { TreinamentosTab } from './TreinamentosTab';
import { EntregasEpiTab } from './EntregasEpiTab';
import { TelegramTab } from './TelegramTab';

type AbaPerfil = 'aso' | 'treinamentos' | 'epi' | 'telegram';

export function TrabalhadorDetalhePage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const estilos = usePageStyles();
  const [aba, setAba] = useState<AbaPerfil>('aso');
  const [cpfVisivel, setCpfVisivel] = useState(false);
  const [trabalhador, setTrabalhador] = useState<Trabalhador | null>(null);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  async function carregar() {
    try {
      setErro(null);
      const [trabs, obs, funcs] = await Promise.all([
        api.trabalhadores.listar(),
        api.obras.listar(),
        api.funcoes.listar(),
      ]);
      setObras(obs);
      setFuncoes(funcs);
      setTrabalhador(trabs.find((t) => t.id === id) ?? null);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao carregar trabalhador.');
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  function nomeObra(obraId: string) {
    return obras.find((o) => o.id === obraId)?.nome ?? obraId;
  }

  function nomeFuncao(funcaoId: string) {
    return funcoes.find((f) => f.id === funcaoId)?.nome ?? funcaoId;
  }

  if (!id) {
    return <Text>Trabalhador não encontrado.</Text>;
  }

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
        {trabalhador ? (
          <>
            <Text size={500} weight="semibold">
              {trabalhador.nome}
            </Text>
            <div style={{ display: 'flex', gap: 16, marginTop: 8, flexWrap: 'wrap', alignItems: 'center' }}>
              <Text>Matrícula: {trabalhador.matricula}</Text>
              <Text>Obra: {nomeObra(trabalhador.obraId)}</Text>
              <Text>Função: {nomeFuncao(trabalhador.funcaoId)}</Text>
              <Text>Admissão: {trabalhador.dataAdmissao?.slice(0, 10)}</Text>
              <Badge appearance="tint">{tipoVinculoLabel[trabalhador.vinculo]}</Badge>
              <div style={{ display: 'flex', alignItems: 'center', gap: 4 }}>
                <Text>CPF: {cpfVisivel ? formatarCpf(trabalhador.cpf) : mascararCpf(trabalhador.cpf)}</Text>
                <Button
                  appearance="subtle"
                  size="small"
                  icon={cpfVisivel ? <EyeOff24Regular /> : <Eye24Regular />}
                  onClick={() => setCpfVisivel((v) => !v)}
                  aria-label={cpfVisivel ? 'Ocultar CPF' : 'Revelar CPF'}
                />
              </div>
            </div>
          </>
        ) : (
          <Text>Carregando...</Text>
        )}
      </div>

      <TabList
        selectedValue={aba}
        onTabSelect={(_: SelectTabEvent, data: SelectTabData) => setAba(data.value as AbaPerfil)}
        style={{ marginBottom: 16 }}
      >
        <Tab value="aso">ASO</Tab>
        <Tab value="treinamentos">Treinamentos</Tab>
        <Tab value="epi">EPIs</Tab>
        <Tab value="telegram">Telegram</Tab>
      </TabList>

      {aba === 'aso' && <AsoTab trabalhadorId={id} />}
      {aba === 'treinamentos' && <TreinamentosTab trabalhadorId={id} />}
      {aba === 'epi' && <EntregasEpiTab trabalhadorId={id} />}
      {aba === 'telegram' && trabalhador && (
        <TelegramTab
          trabalhadorId={id}
          telegramVinculado={trabalhador.telegramVinculado}
          telegramCodigoVinculo={trabalhador.telegramCodigoVinculo}
          aoAtualizar={carregar}
        />
      )}
    </div>
  );
}
