import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button, Card, DataTable, Legenda, StatusChip, type Coluna } from '@ui';
import { Warning24Regular } from '@fluentui/react-icons';
import { api, type CadastroFacialFraco } from '../../lib/api';
import { FalhasReconhecimentoFacialPainel } from './FalhasReconhecimentoFacialPainel';

function formatarData(iso: string): string {
  return new Date(iso).toLocaleDateString('pt-BR', { timeZone: 'America/Sao_Paulo' });
}

// Funcionários com 3 ou mais falhas de reconhecimento facial em 30 dias contra o cadastro atual: o
// técnico refaz a foto antes de o problema aparecer de novo no DDS. Some quando não há ninguém para
// revisar, e também para quem não tem permissão de assinatura (o servidor responde 403). Sem ninguém para
// revisar, sobra só o atalho para a lista completa de falhas (12/10).
//
// `compacto` (lista de Pessoas, 21/10): em vez do card com a tabela, mostra só um botão-aviso com a
// contagem; a tabela abre embaixo, ocupando a linha inteira, quando o técnico clica.
export function CadastrosFaciaisFracosCard({ compacto = false }: { compacto?: boolean } = {}) {
  const navigate = useNavigate();
  const [lista, setLista] = useState<CadastroFacialFraco[]>([]);
  const [permitido, setPermitido] = useState(false);
  const [painelAberto, setPainelAberto] = useState(false);
  const [expandido, setExpandido] = useState(false);

  useEffect(() => {
    let cancelado = false;
    api.trabalhadores
      .listarCadastrosFaciaisFracos()
      .then((l) => {
        if (cancelado) return;
        setLista(l);
        setPermitido(true);
      })
      .catch(() => undefined);
    return () => {
      cancelado = true;
    };
  }, []);

  const painel = <FalhasReconhecimentoFacialPainel aberto={painelAberto} aoFechar={() => setPainelAberto(false)} />;
  const botaoFalhas = (
    <Button size="small" appearance="secondary" onClick={() => setPainelAberto(true)}>
      Ver falhas do reconhecimento facial
    </Button>
  );

  if (!permitido) return null;
  if (lista.length === 0 && compacto) {
    return (
      <>
        {botaoFalhas}
        {painel}
      </>
    );
  }
  if (lista.length === 0) {
    return (
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 8 }}>
        {botaoFalhas}
        {painel}
      </div>
    );
  }

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

  const cartao = (
    <Card titulo={`Cadastros faciais para revisar (${lista.length})`} acoes={botaoFalhas}>
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
      {painel}
    </Card>
  );

  if (!compacto) return cartao;

  return (
    <>
      <Button
        appearance="secondary"
        icon={<Warning24Regular />}
        onClick={() => setExpandido((v) => !v)}
        aria-expanded={expandido}
        aria-controls="cadastros-faciais-revisar"
      >
        {lista.length} {lista.length === 1 ? 'cadastro facial para revisar' : 'cadastros faciais para revisar'}
      </Button>
      {expandido && (
        <div id="cadastros-faciais-revisar" style={{ flexBasis: '100%', order: 1 }}>
          {cartao}
        </div>
      )}
    </>
  );
}
