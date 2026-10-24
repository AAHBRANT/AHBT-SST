import { useEffect, useMemo, useState } from 'react';
import { Campo, Field, Legenda, SeletorPesquisavel, Select } from '@ui';
import {
  api,
  TipoInspecao,
  tipoInspecaoLabel,
  TipoResolucaoAcaoPlano,
  type CatalogoEpi,
  type CursoTreinamento,
  type TemaInspecaoResumo,
  type VinculoSugerido,
} from '../../lib/api';

// "Como se resolve" de uma ação do plano da ocorrência. Não existe mais resolução manual (24/10/2026):
// o técnico concluía o plano inteiro à mão sem executar nada. Toda ação se resolve por um fato do
// sistema — treinamento, inspeção (de um tipo fixo ou de um tema criado pela empresa, ex.: Andaime) ou
// entrega de EPI — e a inspeção só vale quando o responsável designado na ação a finaliza.
export interface ResolucaoEmEdicao {
  tipoResolucao: number | null;
  cursoTreinamentoId: string;
  tipoInspecao: string;
  temaInspecaoId: string;
  catalogoEpiId: string;
}

export const resolucaoVazia = (): ResolucaoEmEdicao => ({
  tipoResolucao: null,
  cursoTreinamentoId: '',
  tipoInspecao: '',
  temaInspecaoId: '',
  catalogoEpiId: '',
});

// Preenche a escolha com o vínculo que a IA sugeriu (nulo quando ela não achou item que case).
export function resolucaoDoVinculo(v: VinculoSugerido | null | undefined): ResolucaoEmEdicao {
  if (!v) return resolucaoVazia();
  return {
    tipoResolucao: v.tipoResolucao,
    cursoTreinamentoId: v.cursoTreinamentoId ?? '',
    tipoInspecao: v.tipoInspecao ? String(v.tipoInspecao) : '',
    temaInspecaoId: v.temaInspecaoId ?? '',
    catalogoEpiId: v.catalogoEpiId ?? '',
  };
}

// Mensagem de pendência ou null quando o vínculo está completo.
export function pendenciaResolucao(r: ResolucaoEmEdicao): string | null {
  if (r.tipoResolucao === null) return 'Escolha como a ação se resolve (treinamento, inspeção ou entrega de EPI).';
  if (r.tipoResolucao === TipoResolucaoAcaoPlano.Treinamento && !r.cursoTreinamentoId) return 'Escolha o curso do treinamento.';
  if (r.tipoResolucao === TipoResolucaoAcaoPlano.Inspecao && !r.tipoInspecao) return 'Escolha a inspeção.';
  if (r.tipoResolucao === TipoResolucaoAcaoPlano.Inspecao && Number(r.tipoInspecao) === TipoInspecao.Tema && !r.temaInspecaoId)
    return 'Escolha o tema de inspeção.';
  if (r.tipoResolucao === TipoResolucaoAcaoPlano.EntregaEpi && !r.catalogoEpiId) return 'Escolha o EPI.';
  return null;
}

export function payloadResolucao(r: ResolucaoEmEdicao) {
  const inspecao = r.tipoResolucao === TipoResolucaoAcaoPlano.Inspecao;
  return {
    tipoResolucao: r.tipoResolucao,
    cursoTreinamentoId: r.tipoResolucao === TipoResolucaoAcaoPlano.Treinamento ? r.cursoTreinamentoId : null,
    tipoInspecao: inspecao ? Number(r.tipoInspecao) : null,
    temaInspecaoId: inspecao && Number(r.tipoInspecao) === TipoInspecao.Tema ? r.temaInspecaoId : null,
    catalogoEpiId: r.tipoResolucao === TipoResolucaoAcaoPlano.EntregaEpi ? r.catalogoEpiId : null,
  };
}

// Campos de vínculo no formato do registro da ocorrência (AcaoPlanoNovaOcorrencia).
export function payloadAcaoOcorrencia(r: ResolucaoEmEdicao) {
  const p = payloadResolucao(r);
  return {
    tipoResolucao: p.tipoResolucao,
    resolucaoCursoTreinamentoId: p.cursoTreinamentoId,
    resolucaoTipoInspecao: p.tipoInspecao,
    resolucaoTemaInspecaoId: p.temaInspecaoId,
    resolucaoCatalogoEpiId: p.catalogoEpiId,
  };
}

const AJUDA: Record<number, string> = {
  [TipoResolucaoAcaoPlano.Treinamento]:
    'Só uma turma deste curso, concluída a partir da data da ocorrência e com os envolvidos presentes, resolve a ação. Treinamento de outro curso não conta.',
  [TipoResolucaoAcaoPlano.Inspecao]:
    'Resolve quando o responsável designado nesta ação finalizar a inspeção escolhida, nesta obra, depois de a ação ser criada. Inspeção de outra pessoa não conta.',
  [TipoResolucaoAcaoPlano.EntregaEpi]: 'Resolve quando a entrega deste EPI for assinada por cada envolvido da ocorrência.',
};

const PREFIXO_TEMA = 'tema:';

// Catálogos que alimentam as escolhas; buscados uma vez por sessão da tela.
let catalogosEmCache: Promise<{ cursos: CursoTreinamento[]; epis: CatalogoEpi[]; temas: TemaInspecaoResumo[] }> | null = null;

function carregarCatalogos() {
  catalogosEmCache ??= Promise.all([
    api.cursosTreinamento.listar().catch(() => [] as CursoTreinamento[]),
    api.catalogosEpi.listar().catch(() => [] as CatalogoEpi[]),
    api.temasInspecao.listar().catch(() => [] as TemaInspecaoResumo[]),
  ]).then(([cursos, epis, temas]) => ({ cursos, epis, temas }));
  return catalogosEmCache;
}

export interface CampoComoSeResolveProps {
  valor: ResolucaoEmEdicao;
  aoMudar: (valor: ResolucaoEmEdicao) => void;
}

export function CampoComoSeResolve({ valor, aoMudar }: CampoComoSeResolveProps) {
  const [catalogos, setCatalogos] = useState<{ cursos: CursoTreinamento[]; epis: CatalogoEpi[]; temas: TemaInspecaoResumo[] }>({
    cursos: [],
    epis: [],
    temas: [],
  });
  useEffect(() => {
    let ativo = true;
    void carregarCatalogos().then((c) => {
      if (ativo) setCatalogos(c);
    });
    return () => {
      ativo = false;
    };
  }, []);

  const opcoesCursos = useMemo(() => catalogos.cursos.map((c) => ({ id: c.id, rotulo: c.nome, descricao: c.normaReferencia ?? undefined })), [catalogos.cursos]);
  const opcoesEpis = useMemo(
    () => catalogos.epis.map((e) => ({ id: e.id, rotulo: e.nome, descricao: e.certificadoAprovacaoNumero ? `CA ${e.certificadoAprovacaoNumero}` : undefined })),
    [catalogos.epis],
  );

  const valorInspecao =
    Number(valor.tipoInspecao) === TipoInspecao.Tema && valor.temaInspecaoId ? `${PREFIXO_TEMA}${valor.temaInspecaoId}` : valor.tipoInspecao;

  function escolherInspecao(selecionado: string) {
    if (selecionado.startsWith(PREFIXO_TEMA))
      aoMudar({ ...valor, tipoInspecao: String(TipoInspecao.Tema), temaInspecaoId: selecionado.slice(PREFIXO_TEMA.length) });
    else aoMudar({ ...valor, tipoInspecao: selecionado, temaInspecaoId: '' });
  }

  return (
    <>
      <Campo span={4}>
        <Field label="Como se resolve" required>
          <Select
            value={valor.tipoResolucao === null ? '' : String(valor.tipoResolucao)}
            onChange={(_, d) => aoMudar({ ...resolucaoVazia(), tipoResolucao: d.value ? Number(d.value) : null })}
          >
            <option value="">Selecione</option>
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
          <Field label="Inspeção" required>
            <Select value={valorInspecao} onChange={(_, d) => escolherInspecao(d.value)}>
              <option value="">Selecione</option>
              {Object.entries(tipoInspecaoLabel)
                .filter(([v]) => Number(v) !== TipoInspecao.Tema)
                .map(([v, rotulo]) => (
                  <option key={v} value={v}>
                    {rotulo}
                  </option>
                ))}
              {catalogos.temas.length > 0 && (
                <optgroup label="Temas criados pela empresa">
                  {catalogos.temas.map((t) => (
                    <option key={t.id} value={`${PREFIXO_TEMA}${t.id}`}>
                      {t.nome}
                    </option>
                  ))}
                </optgroup>
              )}
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
