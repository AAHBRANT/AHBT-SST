import { useEffect, useMemo, useState } from 'react';
import { AnimatePresence, motion } from 'framer-motion';
import { Card, designTokens, Field, Legenda, Select, StatusChip, Text, tokensUi, type Tom } from '@ui';
import { CheckmarkCircle24Filled, DismissCircle24Filled } from '@fluentui/react-icons';
import { api, type Atividade, type EligibilityResult, type Trabalhador } from '../../../lib/api';

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
//
// Onda 2 Task 3 (camada ui/): painel de dashboard no mesmo padrão de AprVencidaPanel.tsx/
// RiscosCriticosPanel.tsx — Card + StatusChip, animação de entrada por linha (framer-motion)
// preservada inline. Badge appearance="tint" color= → StatusChip tom; texto secundário cinza →
// Legenda (primitivo novo, substitui o <Text style={{color: designTokens...}}> que a Task 2 já
// tinha identificado como gap de peça).
export function AptitudeEnginePanel({ trabalhadores, obraId }: AptitudeEnginePanelProps) {
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
      <Card titulo="Motor de Elegibilidade">
        <Legenda>Selecione uma obra no filtro acima para avaliar a elegibilidade da equipe.</Legenda>
      </Card>
    );
  }

  return (
    <Card
      titulo="Motor de Elegibilidade"
      subtitulo="Avalia ASO, treinamento e (quando a atividade exigir) APR/Permissão de Trabalho válidos, usando a regra oficial do backend."
      acoes={
        <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
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
          <StatusChip tom={aptos === avaliacoes.length && avaliacoes.length > 0 ? 'ok' : 'atencao'}>
            {carregando ? 'Avaliando...' : `${aptos} de ${avaliacoes.length} liberados`}
          </StatusChip>
        </div>
      }
    >
      <div style={{ display: 'flex', flexDirection: 'column', gap: 8, maxHeight: 420, overflowY: 'auto' }}>
        <AnimatePresence initial={false}>
          {avaliacoes.map(({ trabalhador, resultado, erro }, indice) => {
            const tom: Tom = resultado?.liberado ? 'ok' : 'alerta';
            return (
              <motion.div
                key={trabalhador.id}
                layout
                initial={{ opacity: 0, x: -8 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ duration: 0.25, delay: Math.min(indice, 12) * 0.02 }}
                style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr auto',
                  alignItems: 'center',
                  gap: 12,
                  padding: '10px 14px',
                  borderRadius: 6,
                  backgroundColor: designTokens.colorNeutralLight,
                  borderLeft: `3px solid ${tokensUi.status[tom].tinta}`,
                }}
              >
                <div>
                  <Text weight="semibold">{trabalhador.nome}</Text>
                  <div>
                    <Legenda>{erro ?? resultado?.motivoBloqueioResumo ?? (resultado?.liberado ? 'Sem pendências' : '')}</Legenda>
                  </div>
                </div>
                {resultado?.liberado ? (
                  <StatusChip tom="ok" icone={<CheckmarkCircle24Filled />}>
                    LIBERADO
                  </StatusChip>
                ) : (
                  <StatusChip tom="alerta" icone={<DismissCircle24Filled />}>
                    BLOQUEADO
                  </StatusChip>
                )}
              </motion.div>
            );
          })}
        </AnimatePresence>
      </div>
    </Card>
  );
}
