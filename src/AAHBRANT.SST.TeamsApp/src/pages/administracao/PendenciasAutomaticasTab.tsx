import { useCallback, useEffect, useState } from 'react';
import { Card, DataTable, FeedbackInline, Legenda, Select, StatusChip, Switch, type Coluna } from '@ui';
import {
  api,
  FrequenciaPendenciaAutomatica,
  TipoPendenciaAutomatica,
  type ConfiguracaoPendenciaComTotal,
} from '../../lib/api';

const NOMES: Record<TipoPendenciaAutomatica, { nome: string; regra: string }> = {
  [TipoPendenciaAutomatica.RecadastroFacial]: {
    nome: 'Recadastro de biometria facial',
    regra: 'Funcionário ativo sem cadastro facial',
  },
  [TipoPendenciaAutomatica.DigitaisIncompletas]: {
    nome: 'Digitais incompletas',
    regra: 'Precisa de 2 digitais: uma de cada mão',
  },
};

// Pendências automáticas (03/11): o sistema monta a lista sozinho a partir dos cadastros, por obra.
// Aqui o Administrador só liga/desliga cada tipo e escolhe quando o pop-up abre. Só Administrador
// (a API recusa os demais perfis).
export function PendenciasAutomaticasTab() {
  const [lista, setLista] = useState<ConfiguracaoPendenciaComTotal[]>([]);
  const [carregando, setCarregando] = useState(true);
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);

  const carregar = useCallback(async () => {
    setCarregando(true);
    setErro(null);
    try {
      setLista(await api.pendenciasAutomaticas.configuracao());
    } catch {
      setErro('Não foi possível carregar as pendências. Esta tela é só para o perfil Administrador.');
    } finally {
      setCarregando(false);
    }
  }, []);

  useEffect(() => {
    void carregar();
  }, [carregar]);

  async function salvar(item: ConfiguracaoPendenciaComTotal, mudanca: Partial<ConfiguracaoPendenciaComTotal>) {
    const novo = { ...item, ...mudanca };
    setLista((atual) => atual.map((c) => (c.tipo === item.tipo ? novo : c)));
    try {
      await api.pendenciasAutomaticas.salvarConfiguracao(novo);
      setAviso(`${NOMES[item.tipo].nome}: alteração salva.`);
    } catch {
      setLista((atual) => atual.map((c) => (c.tipo === item.tipo ? item : c)));
      setErro('Não foi possível salvar a alteração. Tente de novo.');
    }
  }

  const colunas: Coluna<ConfiguracaoPendenciaComTotal>[] = [
    {
      chave: 'tipo',
      rotulo: 'Pendência',
      render: (c) => (
        <div>
          <strong>{NOMES[c.tipo].nome}</strong>
          <Legenda>{NOMES[c.tipo].regra}</Legenda>
        </div>
      ),
    },
    {
      chave: 'ligada',
      rotulo: 'Ligada',
      render: (c) => (
        <Switch
          checked={c.ligada}
          aria-label={`Ligar ${NOMES[c.tipo].nome}`}
          onChange={(_, d) => void salvar(c, { ligada: d.checked })}
        />
      ),
    },
    {
      chave: 'frequencia',
      rotulo: 'Quando abre',
      render: (c) => (
        <Select
          value={String(c.frequencia)}
          aria-label="Quando abre"
          onChange={(_, d) => void salvar(c, { frequencia: Number(d.value) as FrequenciaPendenciaAutomatica })}
        >
          <option value={FrequenciaPendenciaAutomatica.TodaEntrada}>Toda vez que entrar</option>
          <option value={FrequenciaPendenciaAutomatica.UmaVezPorDia}>1 vez por dia</option>
        </Select>
      ),
    },
    {
      chave: 'podeFechar',
      rotulo: 'Pode fechar?',
      render: (c) => (
        <Select
          value={c.podeFechar ? 'sim' : 'nao'}
          aria-label="Pode fechar"
          onChange={(_, d) => void salvar(c, { podeFechar: d.value === 'sim' })}
        >
          <option value="sim">Sim, até a próxima entrada</option>
          <option value="nao">Não</option>
        </Select>
      ),
    },
    {
      chave: 'totalPendentes',
      rotulo: 'Hoje',
      render: (c) =>
        c.totalPendentes > 0 ? (
          <StatusChip tom="atencao">{`${c.totalPendentes} funcionários`}</StatusChip>
        ) : (
          <StatusChip tom="ok">Tudo em dia</StatusChip>
        ),
    },
  ];

  return (
    <div style={{ display: 'grid', gap: 12 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
      {aviso && <FeedbackInline tom="sucesso" aoFechar={() => setAviso(null)}>{aviso}</FeedbackInline>}
      <Card titulo="Pendências automáticas">
        <Legenda>
          O sistema monta a lista sozinho a partir dos cadastros. Cada Técnico de Segurança vê só os funcionários das
          obras dele; o Administrador vê todas as obras.
        </Legenda>
        <DataTable aria-label="Pendências automáticas" colunas={colunas} linhas={lista} chaveLinha={(c) => String(c.tipo)} carregando={carregando} />
      </Card>
    </div>
  );
}
