import { useEffect, useMemo, useState } from 'react';
import { Button, Carregando, Checkbox, DataTable, Select, EstadoVazio, FeedbackInline, FormRodape, StatusChip, useTipografia, type Coluna } from '@ui';
import { api } from '../../lib/api';
import {
  CORES_CAPACETE_OPCOES,
  corCapaceteNome,
  itensParaAplicar,
  montarLinhasPadraoCores,
  resumoPadraoCores,
  rotuloBotaoAplicar,
  selecaoInicialPadraoCores,
  type LinhaPadraoCores,
} from '../../lib/corCapacete';
import { PilulaCorCapacete } from '../../components/CorCapacete';
import { useEstilosPadraoCores } from './estilosPadraoCores';

interface Props {
  aoCancelar: () => void;
  // Chamado depois de gravar, com a quantidade de funções atualizadas (quem chama recarrega a lista).
  aoAplicado: (quantidade: number) => Promise<void> | void;
}

// Conteúdo do quadro de conferência: sugestão do padrão AAHBRANT por função, com cor de hoje x sugerida e uma
// caixa por linha. Nada é salvo antes de "Aplicar". Montado só enquanto o painel está aberto, então a
// sugestão é buscada de novo a cada abertura (reflete cores mudadas desde a última vez).
export function QuadroPadraoCores({ aoCancelar, aoAplicado }: Props) {
  const estilos = useEstilosPadraoCores();
  const tipo = useTipografia();
  const [linhas, setLinhas] = useState<LinhaPadraoCores[] | null>(null);
  const [selecionadas, setSelecionadas] = useState<Set<string>>(new Set());
  // Cor escolhida à mão para função que o padrão não cobre (funcaoId -> cor).
  const [escolhas, setEscolhas] = useState<Record<string, number>>({});
  const [erro, setErro] = useState<string | null>(null);
  const [aplicando, setAplicando] = useState(false);

  const [tentativa, setTentativa] = useState(0);

  // O estado já começa "carregando" (linhas nulas); só a nova tentativa precisa zerá-lo, e isso é feito no clique.
  useEffect(() => {
    let ativo = true;
    api.funcoes
      .listarSugestoesCorCapacete()
      .then((sugestoes) => {
        if (!ativo) return;
        const montadas = montarLinhasPadraoCores(Array.isArray(sugestoes) ? sugestoes : []);
        setLinhas(montadas);
        setSelecionadas(selecaoInicialPadraoCores(montadas));
      })
      .catch((e: unknown) => {
        if (!ativo) return;
        setErro(e instanceof Error ? e.message : 'Não foi possível carregar a sugestão de cores.');
        setLinhas([]);
      });
    return () => {
      ativo = false;
    };
  }, [tentativa]);

  function tentarDeNovo() {
    setErro(null);
    setLinhas(null);
    setTentativa((n) => n + 1);
  }

  const itens = useMemo(() => (linhas ? itensParaAplicar(linhas, selecionadas, escolhas) : []), [linhas, selecionadas, escolhas]);

  function alternar(id: string) {
    setSelecionadas((atual) => {
      const novo = new Set(atual);
      if (novo.has(id)) novo.delete(id);
      else novo.add(id);
      return novo;
    });
  }

  // Escolher uma cor marca a linha para aplicar; voltar para "Sem padrão" desmarca.
  function escolherCor(id: string, cor: number) {
    setEscolhas((atual) => ({ ...atual, [id]: cor }));
    setSelecionadas((atual) => {
      const novo = new Set(atual);
      if (cor > 0) novo.add(id);
      else novo.delete(id);
      return novo;
    });
  }

  async function aplicar() {
    if (itens.length === 0 || aplicando) return;
    setAplicando(true);
    setErro(null);
    try {
      await api.funcoes.aplicarCorCapacete(itens);
      await aoAplicado(itens.length);
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Não foi possível aplicar o padrão de cores.');
    } finally {
      setAplicando(false);
    }
  }

  function caixa(l: LinhaPadraoCores) {
    const podeMarcar = l.situacao === 'muda' || (l.situacao === 'semSugestao' && (escolhas[l.funcaoId] ?? 0) > 0);
    return (
      <Checkbox
        aria-label={
          podeMarcar
            ? `Atualizar a cor da função ${l.nome}`
            : l.situacao === 'jaNoPadrao'
              ? `${l.nome}: já está no padrão`
              : `${l.nome}: escolha uma cor para atualizar`
        }
        checked={podeMarcar && selecionadas.has(l.funcaoId)}
        disabled={!podeMarcar || aplicando}
        onChange={() => alternar(l.funcaoId)}
      />
    );
  }

  function sugestaoDaLinha(l: LinhaPadraoCores) {
    return l.situacao === 'semSugestao' ? (
      <div>
        <div className={estilos.apoio}>O padrão não cobre esta função</div>
        {seletorEscolha(l)}
      </div>
    ) : (
      <PilulaCorCapacete cor={l.corSugerida} />
    );
  }

  // Função sem sugestão: o usuário escolhe a cor (ou deixa como está).
  function seletorEscolha(l: LinhaPadraoCores) {
    return (
      <Select
        aria-label={`Cor do capacete para ${l.nome}`}
        value={String(escolhas[l.funcaoId] ?? 0)}
        disabled={aplicando}
        onChange={(_, d) => escolherCor(l.funcaoId, Number(d.value))}
      >
        {CORES_CAPACETE_OPCOES.map((c) => (
          <option key={c} value={c}>
            {c === 0 ? 'Escolher a cor (fica como está)' : corCapaceteNome[c]}
          </option>
        ))}
      </Select>
    );
  }

  function situacaoDaLinha(l: LinhaPadraoCores) {
    if (l.situacao === 'jaNoPadrao') return <StatusChip tom="ok">Já está no padrão</StatusChip>;
    if (l.situacao === 'semSugestao')
      return (escolhas[l.funcaoId] ?? 0) > 0 ? (
        <span className={estilos.apoio}>Escolhida por você</span>
      ) : (
        <StatusChip tom="neutro">Fica como está</StatusChip>
      );
    return <span className={estilos.apoio}>{l.grupo}</span>;
  }

  const colunas: Coluna<LinhaPadraoCores>[] = [
    { chave: 'marcar', rotulo: <span className={estilos.leitor}>Atualizar</span>, largura: '44px', render: caixa },
    { chave: 'nome', rotulo: 'Função', render: (l) => <strong>{l.nome}</strong> },
    { chave: 'corAtual', rotulo: 'Cor hoje', render: (l) => <PilulaCorCapacete cor={l.corAtual} /> },
    { chave: 'corSugerida', rotulo: 'Sugestão do padrão', render: sugestaoDaLinha },
    { chave: 'grupo', rotulo: 'Grupo do padrão', render: situacaoDaLinha },
  ];

  return (
    <div>
      <p className={`${tipo.corpo} ${estilos.apoio} ${estilos.intro}`}>
        Confira a sugestão para cada função. As linhas marcadas serão atualizadas quando você clicar em "Aplicar".
        Nada é salvo antes disso.
      </p>
      {erro && (
        <FeedbackInline
          tom="erro"
          aoFechar={() => setErro(null)}
          acao={linhas && linhas.length === 0 ? { rotulo: 'Tentar de novo', aoClicar: tentarDeNovo } : undefined}
        >
          {erro}
        </FeedbackInline>
      )}
      {linhas === null && <Carregando variante="lista" linhas={5} />}
      {linhas && linhas.length === 0 && !erro && (
        <EstadoVazio titulo="Nenhuma função para conferir" descricao="Cadastre funções para aplicar o padrão de cores." />
      )}
      {linhas && linhas.length > 0 && (
        <>
          <div className={estilos.soDesktop}>
            <DataTable
              aria-label="Padrão de cores por função"
              colunas={colunas}
              linhas={linhas}
              chaveLinha={(l) => l.funcaoId}
              densidade="compacta"
            />
          </div>
          <div className={estilos.soMovel}>
            {linhas.map((l) => (
              <div key={l.funcaoId} className={estilos.itemMovel}>
                {caixa(l)}
                <div>
                  <div className={estilos.nomeMovel}>{l.nome}</div>
                  <div className={estilos.trocaMovel}>
                    {l.situacao === 'semSugestao' ? (
                      <div>
                        <div className={estilos.apoio}>O padrão não cobre esta função</div>
                        {seletorEscolha(l)}
                      </div>
                    ) : l.situacao === 'jaNoPadrao' ? (
                      <StatusChip tom="ok">Já está no padrão</StatusChip>
                    ) : (
                      <>
                        <PilulaCorCapacete cor={l.corAtual} />
                        <span className={estilos.seta} aria-hidden="true">
                          →
                        </span>
                        <PilulaCorCapacete cor={l.corSugerida} />
                      </>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        </>
      )}
      <FormRodape info={linhas && linhas.length > 0 ? `${resumoPadraoCores(linhas, selecionadas, escolhas)} Você pode mudar qualquer cor depois, em cada função.` : undefined}>
        <Button onClick={aoCancelar} disabled={aplicando}>
          Cancelar
        </Button>
        <Button appearance="primary" onClick={() => void aplicar()} disabled={itens.length === 0 || aplicando}>
          {aplicando ? 'Aplicando...' : rotuloBotaoAplicar(itens.length)}
        </Button>
      </FormRodape>
    </div>
  );
}
