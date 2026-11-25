import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { ListaSelecaoMultipla, type FiltroSelecao, type OpcaoSelecao } from '@ui';
import { api, type Equipe, type Funcao, type Trabalhador } from '../lib/api';
import { FotoTrabalhador } from './FotoTrabalhador';

// Escolha de várias pessoas (Responsáveis da APR e da PT, participantes de turma, envolvidos e
// testemunhas de acidente, inscritos da CIPA). Visual aprovado em 25/11/2026
// (docs/mockups/seletor-pessoas-2026-11-25.html): foto do cadastro, matrícula e função em cada
// linha, ordem alfabética com letra fixa, filtros "Só desta obra", por equipe e pelas funções mais
// comuns, e os escolhidos no topo.

// Nomes das funções: uma busca por sessão, reaproveitada por todos os seletores.
let funcoesCache: Promise<Funcao[]> | null = null;
function carregarFuncoes() {
  if (!funcoesCache) {
    funcoesCache = api.funcoes.listar().catch(() => {
      funcoesCache = null;
      return [] as Funcao[];
    });
  }
  return funcoesCache;
}

const MAX_FILTROS_FUNCAO = 5;
const MAX_FILTROS_EQUIPE = 3;

export interface SeletorTrabalhadoresProps {
  trabalhadores: Trabalhador[];
  selecionados: string[];
  aoMudar: (atualizar: (atuais: string[]) => string[]) => void;
  'aria-label': string;
  placeholderBusca?: string;
  /** Obra do registro: liga o filtro "Só desta obra" quando a lista tem gente de outras obras. */
  obraId?: string | null;
  /** Equipes para filtro de um toque (só as que têm alguém na lista entram). */
  equipes?: Equipe[];
  acoesEscolhidos?: ReactNode;
  rotuloEscolhidos?: [singular: string, plural: string];
}

export function SeletorTrabalhadores({
  trabalhadores, selecionados, aoMudar, 'aria-label': ariaLabel, placeholderBusca = 'Buscar por nome, matrícula ou função',
  obraId, equipes, acoesEscolhidos, rotuloEscolhidos,
}: SeletorTrabalhadoresProps) {
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  useEffect(() => {
    let vivo = true;
    void carregarFuncoes().then((f) => { if (vivo) setFuncoes(f); });
    return () => { vivo = false; };
  }, []);

  const nomeFuncao = useMemo(() => new Map(funcoes.map((f) => [f.id, f.nome])), [funcoes]);
  const porId = useMemo(() => new Map(trabalhadores.map((t) => [t.id, t])), [trabalhadores]);

  const opcoes = useMemo<OpcaoSelecao[]>(() => [...trabalhadores]
    .sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'))
    .map((t) => {
      const funcao = nomeFuncao.get(t.funcaoId);
      return {
        id: t.id,
        rotulo: t.nome,
        descricao: [t.matricula, funcao].filter(Boolean).join(' · ') || undefined,
        textoBusca: `${t.matricula ?? ''} ${funcao ?? ''}`,
        inicio: <FotoTrabalhador trabalhadorId={t.id} nome={t.nome} temFoto={t.temFoto} tamanho={32} />,
        miniatura: <FotoTrabalhador trabalhadorId={t.id} nome={t.nome} temFoto={t.temFoto} tamanho={20} />,
      };
    }), [trabalhadores, nomeFuncao]);

  const filtros = useMemo<FiltroSelecao[]>(() => {
    const lista: FiltroSelecao[] = [];
    const daObra = obraId ? trabalhadores.filter((t) => t.obraId === obraId) : [];
    if (obraId && daObra.length > 0 && daObra.length < trabalhadores.length) {
      lista.push({ id: 'obra', rotulo: 'Só desta obra', padrao: true, aceita: (id) => porId.get(id)?.obraId === obraId });
    }
    const base = daObra.length > 0 ? daObra : trabalhadores;
    const contaEquipe = new Map<string, number>();
    base.forEach((t) => { if (t.equipeId) contaEquipe.set(t.equipeId, (contaEquipe.get(t.equipeId) ?? 0) + 1); });
    (equipes ?? [])
      .filter((eq) => contaEquipe.has(eq.id))
      .sort((a, b) => (contaEquipe.get(b.id) ?? 0) - (contaEquipe.get(a.id) ?? 0))
      .slice(0, MAX_FILTROS_EQUIPE)
      .forEach((eq) => lista.push({ id: `equipe:${eq.id}`, rotulo: eq.nome, tipo: 'escolha', aceita: (id) => porId.get(id)?.equipeId === eq.id }));
    const contaFuncao = new Map<string, number>();
    base.forEach((t) => contaFuncao.set(t.funcaoId, (contaFuncao.get(t.funcaoId) ?? 0) + 1));
    // Só vale filtrar por função quando há mais de uma função na lista.
    if (contaFuncao.size > 1) {
      [...contaFuncao.entries()]
        .filter(([id]) => nomeFuncao.has(id))
        .sort((a, b) => b[1] - a[1])
        .slice(0, MAX_FILTROS_FUNCAO)
        .forEach(([id]) => lista.push({ id: `funcao:${id}`, rotulo: nomeFuncao.get(id)!, tipo: 'escolha', aceita: (tid) => porId.get(tid)?.funcaoId === id }));
    }
    return lista;
  }, [trabalhadores, obraId, equipes, nomeFuncao, porId]);

  return (
    <ListaSelecaoMultipla
      aria-label={ariaLabel}
      placeholderBusca={placeholderBusca}
      opcoes={opcoes}
      selecionados={selecionados}
      aoMudar={aoMudar}
      filtros={filtros}
      agruparPorLetra
      acoesEscolhidos={acoesEscolhidos}
      rotuloEscolhidos={rotuloEscolhidos}
    />
  );
}
