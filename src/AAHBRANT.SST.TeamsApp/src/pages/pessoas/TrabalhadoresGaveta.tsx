import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Avatar,
  Badge,
  Button,
  DrawerBody,
  DrawerHeader,
  DrawerHeaderTitle,
  Input,
  OverlayDrawer,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { Dismiss24Regular, Search24Regular } from '@fluentui/react-icons';
import {
  api,
  resultadoAsoLabel,
  ResultadoAso,
  type Aso,
  type Funcao,
  type Obra,
  type Trabalhador,
} from '../../lib/api';

const useEstilos = makeStyles({
  drawer: {
    width: '400px',
    maxWidth: '92vw',
  },
  busca: {
    width: '100%',
    marginBottom: '12px',
  },
  lista: {
    display: 'flex',
    flexDirection: 'column',
    gap: '2px',
  },
  item: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    padding: '10px 8px',
    borderRadius: '8px',
    cursor: 'pointer',
    border: 'none',
    background: 'transparent',
    width: '100%',
    textAlign: 'left',
    font: 'inherit',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground2,
    },
  },
  info: {
    flex: 1,
    minWidth: 0,
  },
  nome: {
    fontWeight: 600,
    fontSize: '13px',
    color: tokens.colorNeutralForeground1,
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
  },
  meta: {
    fontSize: '12px',
    color: tokens.colorNeutralForeground3,
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    marginTop: '1px',
  },
  lado: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-end',
    gap: '5px',
    flexShrink: 0,
  },
  matricula: {
    fontSize: '11px',
    color: tokens.colorNeutralForeground3,
  },
  vazio: {
    padding: '32px 8px',
    textAlign: 'center',
    color: tokens.colorNeutralForeground3,
    fontSize: '13px',
  },
  contador: {
    color: tokens.colorNeutralForeground3,
    marginTop: '2px',
  },
});

// Situação clínica do ASO mais recente do trabalhador — mesma leitura direta do
// campo Aso.resultadoStatus usada em PessoasDashboardTab (sem reclassificar por
// prazo de validade, que é regra própria daquele dashboard).
function corBadgeAso(resultadoStatus: number | undefined): 'success' | 'warning' | 'danger' | 'informative' {
  if (resultadoStatus === ResultadoAso.Apto) return 'success';
  if (resultadoStatus === ResultadoAso.AptoComRestricao) return 'warning';
  if (resultadoStatus === ResultadoAso.Inapto) return 'danger';
  return 'informative'; // Pendente ou sem ASO registrado ainda
}

export function TrabalhadoresGaveta({
  aberta,
  aoFechar,
  buscaInicial,
}: {
  aberta: boolean;
  aoFechar: () => void;
  /** Ex.: nome da obra, para abrir a gaveta já filtrada a partir de outra tela. */
  buscaInicial?: string;
}) {
  const estilos = useEstilos();
  const navigate = useNavigate();

  const [trabalhadores, setTrabalhadores] = useState<Trabalhador[]>([]);
  const [obras, setObras] = useState<Obra[]>([]);
  const [funcoes, setFuncoes] = useState<Funcao[]>([]);
  const [asos, setAsos] = useState<Aso[]>([]);
  const [carregando, setCarregando] = useState(false);
  const [carregou, setCarregou] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [busca, setBusca] = useState('');
  const [fotoUrls, setFotoUrls] = useState<Record<string, string>>({});

  // Carrega sob demanda, só na primeira vez que a gaveta é aberta.
  useEffect(() => {
    if (!aberta || carregou) return;
    setCarregando(true);
    setErro(null);
    Promise.all([api.trabalhadores.listar(), api.obras.listar(), api.funcoes.listar(), api.asos.listar()])
      .then(([listaTrabalhadores, listaObras, listaFuncoes, listaAsos]) => {
        setTrabalhadores(listaTrabalhadores);
        setObras(listaObras);
        setFuncoes(listaFuncoes);
        setAsos(listaAsos);
        setCarregou(true);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar funcionários.'))
      .finally(() => setCarregando(false));
  }, [aberta, carregou]);

  useEffect(() => {
    if (aberta && buscaInicial) setBusca(buscaInicial);
  }, [aberta, buscaInicial]);

  // Fotos reais são baixadas sob demanda (só para trabalhadores com temFoto) e mantidas como
  // object URL enquanto a gaveta estiver aberta — mesmo padrão de miniatura de logo em
  // ObrasPage.tsx. Sem temFoto, o Avatar cai automaticamente para as iniciais do nome.
  useEffect(() => {
    let cancelado = false;
    (async () => {
      for (const trabalhador of trabalhadores) {
        if (!trabalhador.temFoto || fotoUrls[trabalhador.id]) continue;
        try {
          const blob = await api.trabalhadores.baixarFoto(trabalhador.id);
          if (cancelado) return;
          setFotoUrls((atual) => ({ ...atual, [trabalhador.id]: URL.createObjectURL(blob) }));
        } catch {
          // Falha ao carregar a foto não impede o uso da gaveta; o trabalhador fica com iniciais.
        }
      }
    })();
    return () => {
      cancelado = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadores]);

  useEffect(() => {
    return () => {
      Object.values(fotoUrls).forEach((url) => URL.revokeObjectURL(url));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const asoMaisRecentePorTrabalhador = useMemo(() => {
    const mapa = new Map<string, Aso>();
    for (const aso of asos) {
      const atual = mapa.get(aso.trabalhadorId);
      if (!atual || aso.dataExame > atual.dataExame) mapa.set(aso.trabalhadorId, aso);
    }
    return mapa;
  }, [asos]);

  function nomeObra(id: string) {
    return obras.find((o) => o.id === id)?.nome ?? id;
  }
  function nomeFuncao(id: string) {
    return funcoes.find((f) => f.id === id)?.nome ?? id;
  }

  const filtrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    if (!termo) return trabalhadores;
    return trabalhadores.filter((t) =>
      `${t.nome} ${nomeFuncao(t.funcaoId)} ${nomeObra(t.obraId)} ${t.matricula}`.toLowerCase().includes(termo),
    );
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [trabalhadores, busca, obras, funcoes]);

  function aoSelecionarTrabalhador(id: string) {
    aoFechar();
    navigate(`/pessoas/${id}`);
  }

  return (
    <OverlayDrawer
      position="end"
      open={aberta}
      onOpenChange={(_, dados) => {
        if (!dados.open) aoFechar();
      }}
      className={estilos.drawer}
    >
      <DrawerHeader>
        <DrawerHeaderTitle
          action={
            <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={aoFechar} aria-label="Fechar" />
          }
        >
          Funcionários
        </DrawerHeaderTitle>
        {carregou && (
          <Text size={200} className={estilos.contador}>
            {trabalhadores.length} cadastrados
          </Text>
        )}
        {carregando && !carregou && (
          <Text size={200} className={estilos.contador}>
            Carregando...
          </Text>
        )}
      </DrawerHeader>
      <DrawerBody>
        <Input
          className={estilos.busca}
          contentBefore={<Search24Regular />}
          placeholder="Buscar por nome, função ou obra"
          value={busca}
          onChange={(_, dados) => setBusca(dados.value)}
        />

        {erro && <Text style={{ color: tokens.colorPaletteRedForeground1 }}>{erro}</Text>}
        {carregando && <Spinner label="Carregando funcionários..." />}
        {!carregando && !erro && filtrados.length === 0 && (
          <div className={estilos.vazio}>Nenhum funcionário encontrado.</div>
        )}

        <div className={estilos.lista}>
          {filtrados.map((trabalhador) => {
            const aso = asoMaisRecentePorTrabalhador.get(trabalhador.id);
            return (
              <button
                key={trabalhador.id}
                type="button"
                className={estilos.item}
                onClick={() => aoSelecionarTrabalhador(trabalhador.id)}
              >
                <Avatar
                  name={trabalhador.nome}
                  image={fotoUrls[trabalhador.id] ? { src: fotoUrls[trabalhador.id] } : undefined}
                  color="colorful"
                  size={40}
                />
                <div className={estilos.info}>
                  <div className={estilos.nome}>{trabalhador.nome}</div>
                  <div className={estilos.meta}>
                    {nomeFuncao(trabalhador.funcaoId)} &middot; {nomeObra(trabalhador.obraId)}
                  </div>
                </div>
                <div className={estilos.lado}>
                  <span className={estilos.matricula}>Mat. {trabalhador.matricula}</span>
                  <Badge appearance="tint" color={corBadgeAso(aso?.resultadoStatus)} size="small">
                    {aso ? resultadoAsoLabel[aso.resultadoStatus] : 'Sem ASO'}
                  </Badge>
                </div>
              </button>
            );
          })}
        </div>
      </DrawerBody>
    </OverlayDrawer>
  );
}
