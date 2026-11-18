import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Card, DataTable, FeedbackInline, type Coluna } from '@ui';
import {
  api,
  type Contrato,
  type PessoaTerceirizada,
  type PendenciaPessoa,
  type ContratoEncerradoComPessoasAtivas,
  type AlertaEstoqueInsuficiente,
} from '../../lib/api';
import { useFiltroObra } from '../../lib/ObraAtivaContext';
import { dataBr } from '../../lib/datas';

export function PendenciasTab() {
  const navigate = useNavigate();
  const [obraId] = useFiltroObra();
  const [pessoasBloqueadas, setPessoasBloqueadas] = useState<PendenciaPessoa[]>([]);
  const [contratosEncerrados, setContratosEncerrados] = useState<ContratoEncerradoComPessoasAtivas[]>([]);
  const [alertasEstoque, setAlertasEstoque] = useState<AlertaEstoqueInsuficiente[]>([]);
  const [pessoas, setPessoas] = useState<PessoaTerceirizada[]>([]);
  const [contratos, setContratos] = useState<Contrato[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        setErro(null);
        const [painel, listaPessoas, listaEmpresas] = await Promise.all([
          api.terceirizados.pendencias.listar(),
          api.terceirizados.pessoas.listar().catch(() => [] as PessoaTerceirizada[]),
          api.terceirizados.empresas.listar().catch(() => []),
        ]);
        const contratosPorEmpresa = await Promise.all(
          listaEmpresas.map((e) => api.terceirizados.contratos.listarPorEmpresa(e.id).catch(() => [] as Contrato[])),
        );
        setPessoasBloqueadas(painel.pessoasBloqueadas);
        setContratosEncerrados(painel.contratosEncerradosComPessoasAtivas);
        setAlertasEstoque(painel.alertasEstoqueInsuficiente);
        setPessoas(listaPessoas);
        setContratos(contratosPorEmpresa.flat());
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar pendências.');
      } finally {
        setCarregando(false);
      }
    })();
  }, []);

  const idsContratosNaObra = useMemo(
    () => new Set(contratos.filter((c) => !obraId || c.obraId === obraId).map((c) => c.id)),
    [contratos, obraId],
  );
  const idsPessoasNaObra = useMemo(
    () => new Set(pessoas.filter((p) => !obraId || idsContratosNaObra.has(p.contratoId)).map((p) => p.trabalhadorId)),
    [pessoas, obraId, idsContratosNaObra],
  );
  const pessoasBloqueadasEscopo = useMemo(
    () => (obraId ? pessoasBloqueadas.filter((p) => idsPessoasNaObra.has(p.trabalhadorId)) : pessoasBloqueadas),
    [pessoasBloqueadas, obraId, idsPessoasNaObra],
  );
  const contratosEncerradosEscopo = useMemo(
    () => (obraId ? contratosEncerrados.filter((c) => idsContratosNaObra.has(c.contratoId)) : contratosEncerrados),
    [contratosEncerrados, obraId, idsContratosNaObra],
  );

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
    { chave: 'criadoEmUtc', rotulo: 'Data', render: (a) => dataBr(a.criadoEmUtc) },
  ];

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

      <Card titulo="Pessoas bloqueadas">
        <DataTable
          aria-label="Pessoas bloqueadas"
          colunas={colunasPessoas}
          linhas={pessoasBloqueadasEscopo}
          chaveLinha={(p) => p.trabalhadorId}
          carregando={carregando}
          aoClicarLinha={(p) => navigate(`/pessoas/${p.trabalhadorId}`)}
          vazio={{ titulo: 'Nenhuma pessoa bloqueada' }}
        />
      </Card>

      <Card titulo="Contratos encerrados com gente ainda ativa">
        <DataTable
          aria-label="Contratos encerrados com pessoas ativas"
          colunas={colunasContratos}
          linhas={contratosEncerradosEscopo}
          chaveLinha={(c) => c.contratoId}
          carregando={carregando}
          aoClicarLinha={(c) => navigate(`/terceirizados/contratos/${c.contratoId}`)}
          vazio={{ titulo: 'Nenhum contrato encerrado com gente ainda ativa' }}
        />
      </Card>

      <Card titulo="Falta de estoque de EPI">
        <DataTable
          aria-label="Alertas de estoque insuficiente"
          colunas={colunasAlertas}
          linhas={obraId ? [] : alertasEstoque}
          chaveLinha={(a) => a.alertaId}
          carregando={carregando}
          vazio={{ titulo: 'Nenhum alerta de estoque em aberto' }}
        />
      </Card>
    </div>
  );
}
