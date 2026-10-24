import { useMemo } from 'react';
import { Campo, Field, Legenda, SeletorPesquisavel, Select } from '@ui';
import { tipoInspecaoLabel, TipoResolucaoAcaoPlano, type CatalogoEpi, type CursoTreinamento } from '../../lib/api';

// "Como se resolve" de uma ação do plano da ocorrência (23/10/2026, etapa 2 de
// docs/mockups/plano-acao-conclusao-v1.html): Manual, ou vinculada a um fato que a conclui sozinho
// — só aquele curso, aquele tipo de inspeção ou aquele EPI conta.
export interface ResolucaoEmEdicao {
  tipoResolucao: number | null;
  cursoTreinamentoId: string;
  tipoInspecao: string;
  catalogoEpiId: string;
}

export const resolucaoManual = (): ResolucaoEmEdicao => ({ tipoResolucao: null, cursoTreinamentoId: '', tipoInspecao: '', catalogoEpiId: '' });

// Mensagem de pendência ou null quando o vínculo está completo.
export function pendenciaResolucao(r: ResolucaoEmEdicao): string | null {
  if (r.tipoResolucao === TipoResolucaoAcaoPlano.Treinamento && !r.cursoTreinamentoId) return 'Escolha o curso do treinamento.';
  if (r.tipoResolucao === TipoResolucaoAcaoPlano.Inspecao && !r.tipoInspecao) return 'Escolha o tipo de inspeção.';
  if (r.tipoResolucao === TipoResolucaoAcaoPlano.EntregaEpi && !r.catalogoEpiId) return 'Escolha o EPI.';
  return null;
}

export function payloadResolucao(r: ResolucaoEmEdicao) {
  return {
    tipoResolucao: r.tipoResolucao,
    cursoTreinamentoId: r.tipoResolucao === TipoResolucaoAcaoPlano.Treinamento ? r.cursoTreinamentoId : null,
    tipoInspecao: r.tipoResolucao === TipoResolucaoAcaoPlano.Inspecao ? Number(r.tipoInspecao) : null,
    catalogoEpiId: r.tipoResolucao === TipoResolucaoAcaoPlano.EntregaEpi ? r.catalogoEpiId : null,
  };
}

const AJUDA: Record<number, string> = {
  [TipoResolucaoAcaoPlano.Treinamento]:
    'Só uma turma deste curso, concluída a partir da data da ocorrência e com os envolvidos presentes, resolve a ação. Treinamento de outro curso não conta.',
  [TipoResolucaoAcaoPlano.Inspecao]: 'Resolve quando uma inspeção deste tipo for finalizada nesta obra, a partir da data da ocorrência.',
  [TipoResolucaoAcaoPlano.EntregaEpi]: 'Resolve quando a entrega deste EPI for assinada por cada envolvido da ocorrência.',
};

export interface CampoComoSeResolveProps {
  valor: ResolucaoEmEdicao;
  aoMudar: (valor: ResolucaoEmEdicao) => void;
  cursos: CursoTreinamento[];
  epis: CatalogoEpi[];
}

export function CampoComoSeResolve({ valor, aoMudar, cursos, epis }: CampoComoSeResolveProps) {
  const opcoesCursos = useMemo(() => cursos.map((c) => ({ id: c.id, rotulo: c.nome, descricao: c.normaReferencia ?? undefined })), [cursos]);
  const opcoesEpis = useMemo(() => epis.map((e) => ({ id: e.id, rotulo: e.nome, descricao: e.certificadoAprovacaoNumero ? `CA ${e.certificadoAprovacaoNumero}` : undefined })), [epis]);

  return (
    <>
      <Campo span={4}>
        <Field label="Como se resolve" required>
          <Select
            value={valor.tipoResolucao === null ? '' : String(valor.tipoResolucao)}
            onChange={(_, d) => aoMudar({ ...resolucaoManual(), tipoResolucao: d.value ? Number(d.value) : null })}
          >
            <option value="">Manual</option>
            <option value={TipoResolucaoAcaoPlano.Treinamento}>Treinamento concluído</option>
            <option value={TipoResolucaoAcaoPlano.Inspecao}>Inspeção finalizada</option>
            <option value={TipoResolucaoAcaoPlano.EntregaEpi}>Entrega de EPI assinada</option>
          </Select>
        </Field>
      </Campo>
      {valor.tipoResolucao === TipoResolucaoAcaoPlano.Treinamento && (
        <Campo span={8}>
          <Field label="Curso" required>
            <SeletorPesquisavel
              opcoes={opcoesCursos}
              valor={valor.cursoTreinamentoId}
              aoMudar={(id) => aoMudar({ ...valor, cursoTreinamentoId: id })}
              placeholder="Buscar curso"
              vazio="Nenhum curso encontrado."
            />
          </Field>
        </Campo>
      )}
      {valor.tipoResolucao === TipoResolucaoAcaoPlano.Inspecao && (
        <Campo span={8}>
          <Field label="Tipo de inspeção" required>
            <Select value={valor.tipoInspecao} onChange={(_, d) => aoMudar({ ...valor, tipoInspecao: d.value })}>
              <option value="">Selecione</option>
              {Object.entries(tipoInspecaoLabel).map(([v, rotulo]) => (
                <option key={v} value={v}>
                  {rotulo}
                </option>
              ))}
            </Select>
          </Field>
        </Campo>
      )}
      {valor.tipoResolucao === TipoResolucaoAcaoPlano.EntregaEpi && (
        <Campo span={8}>
          <Field label="EPI" required>
            <SeletorPesquisavel
              opcoes={opcoesEpis}
              valor={valor.catalogoEpiId}
              aoMudar={(id) => aoMudar({ ...valor, catalogoEpiId: id })}
              placeholder="Buscar EPI"
              vazio="Nenhum EPI encontrado."
            />
          </Field>
        </Campo>
      )}
      {valor.tipoResolucao !== null && (
        <Campo span={12}>
          <Legenda>{AJUDA[valor.tipoResolucao]}</Legenda>
        </Campo>
      )}
    </>
  );
}
