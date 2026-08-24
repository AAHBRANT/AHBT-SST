import { useEffect, useMemo, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { Badge, Field, Select, Text } from '@fluentui/react-components';
import { CheckmarkCircle24Filled, DismissCircle24Filled } from '@fluentui/react-icons';
import { api, type Atividade, type EligibilityResult, type Trabalhador } from '../../../lib/api';
import { useDashboardStyles } from '../../../components/dashboard/dashboardStyles';

interface AptitudeEnginePanelProps {
  trabalhadores: Trabalhador[];
  obraId: string;
}

interface AvaliacaoTrabalhador {
  trabalhador: Trabalhador;
  resultado?: EligibilityResult;
  erro?: string;
}

// Consome o Motor de Elegibilidade real do backend (POST /api/elegibilidade/avaliar).
// Diferença consciente em relação à antiga simulação local: o motor real não verifica
// EPI nem curso específico por tarefa (só treinamento válido em geral) e trata
// "Apto com restrição" como sempre válido — decisão do usuário: mostrar a regra real,
// mesmo mais simples, em vez de simular uma regra mais rica que o backend não aplica.
export function AptitudeEnginePanel({ trabalhadores, obraId }: AptitudeEnginePanelProps) {
  const estilos = useDashboardStyles();
  const [atividades, setAtividades] = useState<Atividade[]>([]);
  const [atividadeId, setAtividadeId] = useState('');
  const [avaliacoes, setAvaliacoes] = useState<AvaliacaoTrabalhador[]>([]);
  const [carregando, setCarregando] = useState(false);

  useEffect(() => {
    setAtividadeId('');
    if (!obraId) {
      setAtividades([]);
      return;
    }
    (async () => {
      try {
        setAtividades(await api.atividades.listar(obraId));
      } catch {
        setAtividades([]);
      }
    })();
  }, [obraId]);

  useEffect(() => {
    if (!obraId || trabalhadores.length === 0) {
      setAvaliacoes([]);
      return;
    }
    let cancelado = false;
    (async () => {
      setCarregando(true);
      const resultados = await Promise.all(
        trabalhadores.map(async (trabalhador) => {
          try {
            const resultado = await api.elegibilidade.avaliar({
              trabalhadorId: trabalhador.id,
              obraId,
              atividadeId: atividadeId || null,
              contextoModulo: 'pessoas-dashboard',
            });
            return { trabalhador, resultado };
          } catch (e) {
            return { trabalhador, erro: e instanceof Error ? e.message : 'Falha ao avaliar elegibilidade.' };
          }
        }),
      );
      if (!cancelado) {
        setAvaliacoes(
          resultados.sort((a, b) => {
            const bloqA = a.resultado ? !a.resultado.liberado : true;
            const bloqB = b.resultado ? !b.resultado.liberado : true;
            return bloqA === bloqB ? 0 : bloqA ? -1 : 1;
          }),
        );
        setCarregando(false);
      }
    })();
    return () => {
      cancelado = true;
    };
  }, [trabalhadores, obraId, atividadeId]);

  const aptos = useMemo(() => avaliacoes.filter((a) => a.resultado?.liberado).length, [avaliacoes]);

  if (!obraId) {
    return (
      <div className={estilos.motorPainel}>
        <Text weight="semibold" size={400}>
          Motor de Elegibilidade
        </Text>
        <div>
          <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
            Selecione uma obra no filtro acima para avaliar a elegibilidade da equipe.
          </Text>
        </div>
      </div>
    );
  }

  return (
    <div className={estilos.motorPainel}>
      <div className={estilos.motorCabecalho}>
        <div>
          <Text weight="semibold" size={400}>
            Motor de Elegibilidade
          </Text>
          <div>
            <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
              Avalia ASO, treinamento e (quando a atividade exigir) APR/Permissão de Trabalho válidos, usando a regra
              oficial do backend.
            </Text>
          </div>
        </div>
        <Field label="Atividade (opcional)" style={{ minWidth: 280 }}>
          <Select value={atividadeId} onChange={(_, data) => setAtividadeId(data.value)}>
            <option value="">Regras gerais (sem atividade específica)</option>
            {atividades.map((a) => (
              <option key={a.id} value={a.id}>
                {a.nome}
              </option>
            ))}
          </Select>
        </Field>
        <Badge appearance="tint" color={aptos === avaliacoes.length && avaliacoes.length > 0 ? 'success' : 'warning'}>
          {carregando ? 'Avaliando...' : `${aptos} de ${avaliacoes.length} liberados`}
        </Badge>
      </div>

      <div className={estilos.motorLista}>
        <AnimatePresence initial={false}>
          {avaliacoes.map(({ trabalhador, resultado, erro }, indice) => (
            <motion.div
              key={trabalhador.id}
              layout
              initial={{ opacity: 0, x: -8 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
              className={`${estilos.motorLinha} ${resultado?.liberado ? estilos.motorLinhaApta : estilos.motorLinhaBloqueada}`}
            >
              <div>
                <Text weight="semibold">{trabalhador.nome}</Text>
                <div>
                  <Text size={200} style={{ color: 'var(--colorNeutralForeground3, #6D6D6D)' }}>
                    {erro ?? resultado?.motivoBloqueioResumo ?? (resultado?.liberado ? 'Sem pendências' : '')}
                  </Text>
                </div>
              </div>
              {resultado?.liberado ? (
                <Badge appearance="tint" color="success" icon={<CheckmarkCircle24Filled />}>
                  LIBERADO
                </Badge>
              ) : (
                <Badge appearance="tint" color="danger" icon={<DismissCircle24Filled />}>
                  BLOQUEADO
                </Badge>
              )}
            </motion.div>
          ))}
        </AnimatePresence>
      </div>
    </div>
  );
}
