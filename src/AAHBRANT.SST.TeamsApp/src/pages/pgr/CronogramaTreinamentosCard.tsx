import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, FeedbackInline, StatusChip, Text, type Coluna } from '@ui';
import { api, type CronogramaTreinamentosGeracao, type LinhaTreinamentoPgr } from '../../lib/api';

const colunas: Coluna<LinhaTreinamentoPgr>[] = [
  { chave: 'funcao', rotulo: 'Função' },
  { chave: 'curso', rotulo: 'Curso' },
  { chave: 'carga', rotulo: 'Carga horária', render: (l) => (l.cargaHorariaMinima > 0 ? `${l.cargaHorariaMinima} h` : '—') },
  { chave: 'validade', rotulo: 'Validade', render: (l) => (l.validadeEmMeses > 0 ? `${l.validadeEmMeses} meses` : '—') },
  { chave: 'motivo', rotulo: 'Motivo (PGR)', render: (l) => l.motivo || '—' },
  { chave: 'situacao', rotulo: 'Situação', render: (l) => <StatusChip tom={l.aprovado ? 'ok' : 'atencao'}>{l.aprovado ? 'Confirmado' : 'A confirmar'}</StatusChip> },
];

// Cronograma de treinamentos do PGR aprovado (NR-01, item 1.7): os cursos obrigatórios de cada função, os mesmos
// da seção 7.1 do PDF. "A confirmar" é sugestão do sistema até o engenheiro aprovar na aba de cursos obrigatórios.
export function CronogramaTreinamentosCard({ geracaoId }: { geracaoId: string }) {
  const [dados, setDados] = useState<CronogramaTreinamentosGeracao | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const navigate = useNavigate();

  useEffect(() => {
    api.geracoesPgrEap.cronogramaTreinamentos(geracaoId).then(setDados)
      .catch((e) => setErro(e instanceof Error ? e.message : 'Não foi possível carregar o cronograma de treinamentos.'));
  }, [geracaoId]);

  if (erro) return <FeedbackInline tom="erro">{erro}</FeedbackInline>;
  if (!dados) return null;

  const aConfirmar = dados.linhas.filter((l) => !l.aprovado).length;
  const grupos = dados.ghes
    .map((g) => ({ ghe: g, linhas: dados.linhas.filter((l) => l.gheNumero === g.numero) }))
    .filter((x) => x.linhas.length > 0);

  return (
    <Card
      titulo="Cronograma de treinamentos"
      acoes={<Button onClick={() => navigate('/gestao-sst?secao=treinamentos&aba=obrigatorios')}>Abrir cursos obrigatórios por obra</Button>}
    >
      <div style={{ marginBottom: 8 }}>
        {dados.linhas.length === 0
          ? <StatusChip tom="atencao">Nenhum curso</StatusChip>
          : aConfirmar > 0
            ? <StatusChip tom="atencao">{aConfirmar} curso(s) a confirmar pelo engenheiro</StatusChip>
            : <StatusChip tom="ok">Todos confirmados</StatusChip>}
      </div>
      {dados.linhas.length === 0 ? (
        <Text size={200}>
          Nenhum curso obrigatório foi sugerido para as funções deste PGR. Abra a aba de cursos obrigatórios por obra para cadastrar os cursos.
        </Text>
      ) : grupos.map(({ ghe, linhas }) => (
        <div key={ghe.numero} style={{ marginBottom: 12 }}>
          <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>{`GHE ${String(ghe.numero).padStart(2, '0')} · ${ghe.nome}`}</Text>
          <DataTable
            aria-label={`Treinamentos do GHE ${ghe.numero}`}
            colunas={colunas}
            linhas={linhas}
            chaveLinha={(l) => `${l.gheNumero}|${l.funcao}|${l.curso}`}
            densidade="compacta"
          />
        </div>
      ))}
    </Card>
  );
}
