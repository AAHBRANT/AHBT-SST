import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, Legenda, StatusChip, type Coluna } from '@ui';
import { api, type CadastroFacialFraco } from '../../lib/api';

function formatarData(iso: string): string {
  return new Date(iso).toLocaleDateString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

// Funcionários com 3 ou mais falhas de reconhecimento facial em 30 dias contra o cadastro atual: o
// técnico refaz a foto antes de o problema aparecer de novo no DDS. Some quando não há ninguém para
// revisar, e também para quem não tem permissão de assinatura (o servidor responde 403).
export function CadastrosFaciaisFracosCard() {
  const navigate = useNavigate();
  const [lista, setLista] = useState<CadastroFacialFraco[]>([]);

  useEffect(() => {
    let cancelado = false;
    api.trabalhadores
      .listarCadastrosFaciaisFracos()
      .then((l) => !cancelado && setLista(l))
      .catch(() => undefined);
    return () => {
      cancelado = true;
    };
  }, []);

  if (lista.length === 0) return null;

  const colunas: Coluna<CadastroFacialFraco>[] = [
    {
      chave: 'nome',
      rotulo: 'Funcionário',
      render: (f) => (
        <div>
          <div>{f.nome}</div>
          {f.matricula && <Legenda>Mat. {f.matricula}</Legenda>}
        </div>
      ),
    },
    { chave: 'obra', rotulo: 'Obra', render: (f) => f.obraNome },
    { chave: 'falhas', rotulo: 'Falhas (30 dias)', render: (f) => <StatusChip tom={f.falhas >= 5 ? 'alerta' : 'atencao'}>{f.falhas}</StatusChip> },
    { chave: 'motivo', rotulo: 'Último motivo', render: (f) => f.ultimoMotivo },
    { chave: 'cadastro', rotulo: 'Cadastro de', render: (f) => formatarData(f.cadastroEm) },
  ];

  return (
    <Card titulo={`Cadastros faciais para revisar (${lista.length})`}>
      <Legenda>
        Funcionários com 3 ou mais falhas de reconhecimento facial nos últimos 30 dias. Abra o cadastro e use
        Refazer cadastro facial.
      </Legenda>
      <DataTable
        aria-label="Cadastros faciais para revisar"
        colunas={colunas}
        linhas={lista}
        chaveLinha={(f) => f.trabalhadorId}
        densidade="compacta"
        acoesLinha={(f) => (
          <Button
            size="small"
            onClick={() => navigate(`/pessoas/${f.trabalhadorId}`, { state: { aba: 'cofre' } })}
          >
            Abrir cadastro
          </Button>
        )}
      />
    </Card>
  );
}
