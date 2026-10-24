import { useEffect, useState } from 'react';
import { Card, DataTable, FeedbackInline, type Coluna } from '@ui';
import {
  api,
  type PendenciaPessoa,
  type ContratoEncerradoComPessoasAtivas,
  type AlertaEstoqueInsuficiente,
} from '../../lib/api';

export function PendenciasTab() {
  const [pessoasBloqueadas, setPessoasBloqueadas] = useState<PendenciaPessoa[]>([]);
  const [contratosEncerrados, setContratosEncerrados] = useState<ContratoEncerradoComPessoasAtivas[]>([]);
  const [alertasEstoque, setAlertasEstoque] = useState<AlertaEstoqueInsuficiente[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const painel = await api.terceirizados.pendencias.listar();
        setPessoasBloqueadas(painel.pessoasBloqueadas);
        setContratosEncerrados(painel.contratosEncerradosComPessoasAtivas);
        setAlertasEstoque(painel.alertasEstoqueInsuficiente);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar pendências.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const colunasPessoas: Coluna<PendenciaPessoa>[] = [
    { chave: 'nome', rotulo: 'Nome' },
    { chave: 'empresaRazaoSocial', rotulo: 'Empresa' },
    { chave: 'pendencias', rotulo: 'Pendências', render: (p) => p.pendencias.join('; ') },
  ];

  const colunasContratos: Coluna<ContratoEncerradoComPessoasAtivas>[] = [
    { chave: 'numeroContrato', rotulo: 'Contrato' },
    { chave: 'empresaRazaoSocial', rotulo: 'Empresa' },
    { chave: 'quantidadePessoasAtivas', rotulo: 'Pessoas ainda ativas', alinhar: 'direita' },
  ];

  const colunasAlertas: Coluna<AlertaEstoqueInsuficiente>[] = [
    { chave: 'titulo', rotulo: 'Alerta' },
    { chave: 'descricao', rotulo: 'Detalhe' },
    { chave: 'criadoEmUtc', rotulo: 'Data', render: (a) => a.criadoEmUtc.slice(0, 10) },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Pessoas bloqueadas">
        <DataTable
          aria-label="Pessoas bloqueadas"
          colunas={colunasPessoas}
          linhas={pessoasBloqueadas}
          chaveLinha={(p) => p.trabalhadorId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhuma pessoa bloqueada' }}
        />
      </Card>

      <Card titulo="Contratos encerrados com gente ainda ativa">
        <DataTable
          aria-label="Contratos encerrados com pessoas ativas"
          colunas={colunasContratos}
          linhas={contratosEncerrados}
          chaveLinha={(c) => c.contratoId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum contrato encerrado com gente ainda ativa' }}
        />
      </Card>

      <Card titulo="Falta de estoque de EPI">
        <DataTable
          aria-label="Alertas de estoque insuficiente"
          colunas={colunasAlertas}
          linhas={alertasEstoque}
          chaveLinha={(a) => a.alertaId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum alerta de estoque em aberto' }}
        />
      </Card>
    </div>
  );
}
