import { useEffect, useState } from 'react';
import {
  Card,
  Checkbox,
  DataTable,
  Field,
  FeedbackInline,
  Input,
  Select,
  StatusChip,
  Text,
  designTokens,
  LIMIAR_ALERTA_DIAS,
  type Coluna,
  type Tom,
} from '@ui';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  tipoExameAsoLabel,
  type Aso,
  type Trabalhador,
} from '../../lib/api';

// ResultadoAso: Apto=1, AptoComRestricao=2, Inapto=3, Pendente=4 — mesmo enum reaproveitado por
// AptidoesTab.tsx, mesma tabela de tons.
const tomPorResultadoAso: Record<number, Tom> = { 1: 'ok', 2: 'atencao', 3: 'alerta', 4: 'info' };

const estiloLinhaApoio = { display: 'block', fontSize: 12, marginTop: 2, color: designTokens.colorNeutralMedium };

// Compara só o dia do calendário: a API manda a validade com hora, e somar a hora/fuso fazia a
// contagem errar por 1 dia.
function diasAteValidade(dataValidade: string) {
  const [ano, mes, dia] = dataValidade.slice(0, 10).split('-').map(Number);
  const hoje = new Date(new Date().toDateString());
  return Math.round((new Date(ano, mes - 1, dia).getTime() - hoje.getTime()) / 86_400_000);
}

function formatarData(iso: string) {
  const [ano, mes, dia] = iso.slice(0, 10).split('-');
  return `${dia}/${mes}/${ano}`;
}

// "Situação hoje" cruza validade e parecer (pedido do usuário, 13/10): antes a linha mostrava
// "Vencido" na validade e "Apto" no resultado lado a lado, e quem lia entendia que o trabalhador
// estava liberado. Vencimento vence qualquer parecer; parecer só decide quando o exame ainda vale.
// Regra única do ASO vigente (26/10): só o ASO do exame mais recente da pessoa é avaliado; os anteriores aparecem como
// "Superado" (histórico, nunca "Vencido" nem bloqueio) e o exame sem parecer aparece como "Sem parecer", não como Apto.
function situacaoHoje(aso: Aso): { tom: Tom; rotulo: string; detalhe?: string } {
  if (aso.vigente === false)
    return {
      tom: 'neutro',
      rotulo: 'Superado',
      detalhe: aso.substituidoPorDataExame ? `Substituído pelo exame de ${formatarData(aso.substituidoPorDataExame)}` : 'Substituído por exame mais recente',
    };
  const dias = diasAteValidade(aso.dataValidade);
  if (dias < 0)
    return { tom: 'alerta', rotulo: 'Vencido', detalhe: `Refazer exame · venceu há ${-dias} ${-dias === 1 ? 'dia' : 'dias'}` };
  if (aso.resultadoStatus === ResultadoAso.Inapto)
    return { tom: 'alerta', rotulo: 'Não liberado', detalhe: 'Inapto no exame' };
  if (aso.resultadoStatus === ResultadoAso.Pendente)
    return { tom: 'atencao', rotulo: 'Sem parecer', detalhe: 'Lançar o resultado no G-RH ou editar o ASO' };
  if (dias <= LIMIAR_ALERTA_DIAS)
    return { tom: 'atencao', rotulo: dias === 0 ? 'Vence hoje' : `Vence em ${dias} ${dias === 1 ? 'dia' : 'dias'}`, detalhe: 'Agendar periódico' };
  if (aso.resultadoStatus === ResultadoAso.AptoComRestricao) return { tom: 'atencao', rotulo: 'Liberado com restrição' };
  return { tom: 'ok', rotulo: 'Liberado' };
}

// O G-RH às vezes tem o mesmo ASO em vários documentos (ex.: 9 linhas iguais de um trabalhador em
// 13/10), e o SST cria um registro por documento. Mostra uma linha por exame — mesmo trabalhador,
// tipo, data e validade — sem apagar nada do banco; prefere a cópia que já tem parecer lançado.
interface AsoAgrupado extends Aso {
  copias: number;
}

function agruparRepetidos(lista: Aso[]): AsoAgrupado[] {
  const grupos = new Map<string, AsoAgrupado>();
  for (const aso of lista) {
    const chave = [aso.trabalhadorId, aso.tipo, aso.dataExame?.slice(0, 10), aso.dataValidade?.slice(0, 10)].join('|');
    const atual = grupos.get(chave);
    if (!atual) {
      grupos.set(chave, { ...aso, copias: 1 });
    } else if (atual.resultadoStatus === ResultadoAso.Pendente && aso.resultadoStatus !== ResultadoAso.Pendente) {
      grupos.set(chave, { ...aso, copias: atual.copias + 1, vigente: atual.vigente || aso.vigente });
    } else {
      atual.copias += 1;
      atual.vigente = atual.vigente || aso.vigente;
    }
  }
  return [...grupos.values()];
}

// ASO só consulta (08/11/2026, pedido do usuário): os ASOs vêm do G-RH e a próxima importação sobrescreveria qualquer
// correção feita aqui — então não há edição nem exclusão nesta tela. Correção se faz no G-RH. Busca por nome ou matrícula
// e filtro pela situação de hoje.
type FiltroSituacao = 'todas' | 'liberado' | 'vence' | 'sem-parecer' | 'nao-liberado';

export function AsosTab() {
  const [asos, setAsos] = useState<AsoAgrupado[]>([]);
  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [erro, setErro] = useState<string | null>(null);
  const [carregandoLista, setCarregandoLista] = useState(true);
  const [busca, setBusca] = useState('');
  const [filtro, setFiltro] = useState<FiltroSituacao>('todas');
  const [soVigentes, setSoVigentes] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const [lista, listaTrabalhadores] = await Promise.all([api.asos.listar(), api.trabalhadores.listar()]);
        setAsos(agruparRepetidos(lista));
        setTrabalhadores(listaTrabalhadores);
      } catch (e) {
        setErro(e instanceof Error ? e.message : 'Falha ao carregar ASOs.');
      } finally {
        setCarregandoLista(false);
      }
    })();
  }, []);

  const porId = new Map(trabalhadores.map((t) => [t.id, t]));
  const nomeTrabalhador = (id: string) => porId.get(id)?.nome ?? id;

  const termo = busca.trim().toLowerCase();
  const linhas = asos.filter((a) => {
    if (soVigentes && a.vigente === false) return false;
    if (termo) {
      const t = porId.get(a.trabalhadorId);
      const texto = `${t?.nome ?? ''} ${t?.matricula ?? ''}`.toLowerCase();
      if (!texto.includes(termo)) return false;
    }
    if (filtro === 'todas') return true;
    const sit = situacaoHoje(a).rotulo;
    if (filtro === 'liberado') return sit === 'Liberado' || sit === 'Liberado com restrição';
    if (filtro === 'vence') return sit.startsWith('Vence') || sit === 'Vencido';
    if (filtro === 'sem-parecer') return sit === 'Sem parecer';
    return sit === 'Não liberado';
  });

  const colunas: Coluna<AsoAgrupado>[] = [
    {
      chave: 'trabalhador',
      rotulo: 'Funcionário',
      render: (a) => (
        <>
          {nomeTrabalhador(a.trabalhadorId)}
          {porId.get(a.trabalhadorId)?.matricula && <span style={estiloLinhaApoio}>{porId.get(a.trabalhadorId)?.matricula}</span>}
          {a.copias > 1 && (
            <span style={estiloLinhaApoio} title={`O mesmo exame foi recebido ${a.copias} vezes do G-RH.`}>
              {a.copias} documentos iguais
            </span>
          )}
        </>
      ),
    },
    { chave: 'tipo', rotulo: 'Tipo', render: (a) => tipoExameAsoLabel[a.tipo] },
    { chave: 'exame', rotulo: 'Exame', render: (a) => (a.dataExame ? formatarData(a.dataExame) : '—') },
    { chave: 'validade', rotulo: 'Validade', render: (a) => (a.dataValidade ? formatarData(a.dataValidade) : '—') },
    {
      chave: 'situacao',
      rotulo: 'Situação hoje',
      render: (a) => {
        const situacao = situacaoHoje(a);
        return (
          <>
            <StatusChip tom={situacao.tom}>{situacao.rotulo}</StatusChip>
            {situacao.detalhe && <span style={estiloLinhaApoio}>{situacao.detalhe}</span>}
          </>
        );
      },
    },
    {
      chave: 'resultado',
      rotulo: 'Parecer do médico',
      render: (a) =>
        a.vigente === false || diasAteValidade(a.dataValidade) < 0 ? (
          <>
            <StatusChip tom="neutro">{resultadoAsoLabel[a.resultadoStatus]}</StatusChip>
            <span style={estiloLinhaApoio}>no exame de {formatarData(a.dataExame)}</span>
          </>
        ) : (
          <StatusChip tom={tomPorResultadoAso[a.resultadoStatus] ?? 'neutro'}>{resultadoAsoLabel[a.resultadoStatus]}</StatusChip>
        ),
    },
  ];

  return (
    <>
      {erro && (
        <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>
          {erro}
        </FeedbackInline>
      )}
      <Card titulo="ASOs" subtitulo="Recebidos automaticamente do G-RH. Para corrigir um ASO, ajuste no G-RH: a próxima importação atualiza aqui.">
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end', marginBottom: 12 }}>
          <Field label="Buscar" style={{ minWidth: 240 }}>
            <Input value={busca} onChange={(_, d) => setBusca(d.value)} placeholder="Nome ou matrícula" />
          </Field>
          <Field label="Situação" style={{ minWidth: 220 }}>
            <Select value={filtro} onChange={(_, d) => setFiltro(d.value as FiltroSituacao)}>
              <option value="todas">Todas</option>
              <option value="liberado">Liberados</option>
              <option value="vence">Vencendo ou vencidos</option>
              <option value="sem-parecer">Sem parecer</option>
              <option value="nao-liberado">Não liberados</option>
            </Select>
          </Field>
          <Checkbox label="Só o exame mais recente de cada um" checked={soVigentes} onChange={(_, d) => setSoVigentes(!!d.checked)} />
        </div>
        <DataTable
          aria-label="ASOs recebidos do G-RH"
          colunas={colunas}
          linhas={linhas}
          chaveLinha={(a) => a.id}
          carregando={carregandoLista}
          vazio={{ titulo: busca || filtro !== 'todas' ? 'Nenhum ASO com esse filtro.' : 'Nenhum ASO recebido do G-RH ainda.' }}
        />
        <Text size={200} style={{ color: designTokens.colorNeutralMedium, display: 'block', marginTop: 8 }}>
          {linhas.length} ASO(s) na lista.
        </Text>
      </Card>
    </>
  );
}
