import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Avatar,
  Badge,
  Drawer,
  DrawerBody,
  DrawerHeader,
  DrawerHeaderTitle,
  Button,
  Input,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components';
import { Dismiss24Regular, Search24Regular } from '@fluentui/react-icons';
import { api, resultadoAsoLabel, ResultadoAso, type Aso, type Trabalhador } from '../../lib/api';

const useStyles = makeStyles({
  drawer: {
    width: '380px',
  },
  busca: {
    marginBottom: '12px',
  },
  lista: {
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
  },
  linha: {
    display: 'flex',
    alignItems: 'center',
    gap: '12px',
    padding: '10px 8px',
    borderRadius: '8px',
    cursor: 'pointer',
    ':hover': {
      backgroundColor: tokens.colorNeutralBackground2,
    },
  },
  info: {
    display: 'flex',
    flexDirection: 'column',
    flexGrow: 1,
    minWidth: 0,
  },
  nome: {
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
});

function corBadgeAso(status: number | undefined): 'success' | 'warning' | 'danger' | 'informative' {
  switch (status) {
    case ResultadoAso.Apto:
      return 'success';
    case ResultadoAso.AptoComRestricao:
      return 'warning';
    case ResultadoAso.Inapto:
      return 'danger';
    default:
      return 'informative';
  }
}

function ultimoAso(asos: Aso[], trabalhadorId: string): Aso | undefined {
  return asos
    .filter((a) => a.trabalhadorId === trabalhadorId)
    .sort((a, b) => new Date(b.dataExame).getTime() - new Date(a.dataExame).getTime())[0];
}

export function TrabalhadoresGaveta({
  aberta,
  aoFechar,
  trabalhadores,
}: {
  aberta: boolean;
  aoFechar: () => void;
  trabalhadores: Trabalhador[];
}) {
  const estilos = useStyles();
  const navigate = useNavigate();
  const [busca, setBusca] = useState('');
  const [asos, setAsos] = useState<Aso[]>([]);

  useEffect(() => {
    if (!aberta) return;
    api.asos
      .listar()
      .then(setAsos)
      .catch(() => setAsos([]));
  }, [aberta]);

  const filtrados = useMemo(() => {
    const termo = busca.trim().toLowerCase();
    if (!termo) return trabalhadores;
    return trabalhadores.filter(
      (t) => t.nome.toLowerCase().includes(termo) || t.matricula.toLowerCase().includes(termo),
    );
  }, [busca, trabalhadores]);

  return (
    <Drawer
      className={estilos.drawer}
      separator
      open={aberta}
      onOpenChange={(_, data) => !data.open && aoFechar()}
      position="end"
    >
      <DrawerHeader>
        <DrawerHeaderTitle
          action={
            <Button appearance="subtle" icon={<Dismiss24Regular />} onClick={aoFechar} aria-label="Fechar" />
          }
        >
          Trabalhadores
        </DrawerHeaderTitle>
      </DrawerHeader>
      <DrawerBody>
        <Input
          className={estilos.busca}
          contentBefore={<Search24Regular />}
          placeholder="Buscar por nome ou matrícula"
          value={busca}
          onChange={(_, d) => setBusca(d.value)}
        />
        <div className={estilos.lista}>
          {filtrados.map((trabalhador) => {
            const aso = ultimoAso(asos, trabalhador.id);
            return (
              <div
                key={trabalhador.id}
                className={estilos.linha}
                onClick={() => {
                  aoFechar();
                  navigate(`/pessoas/${trabalhador.id}`);
                }}
              >
                <Avatar name={trabalhador.nome} color="colorful" size={40} />
                <div className={estilos.info}>
                  <Text weight="semibold" className={estilos.nome}>
                    {trabalhador.nome}
                  </Text>
                  <Text size={200}>{trabalhador.matricula}</Text>
                </div>
                {aso && (
                  <Badge color={corBadgeAso(aso.resultadoStatus)} appearance="tint">
                    {resultadoAsoLabel[aso.resultadoStatus]}
                  </Badge>
                )}
              </div>
            );
          })}
          {filtrados.length === 0 && <Text>Nenhum trabalhador encontrado.</Text>}
        </div>
      </DrawerBody>
    </Drawer>
  );
}
