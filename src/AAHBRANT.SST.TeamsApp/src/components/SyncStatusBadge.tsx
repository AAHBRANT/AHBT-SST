import { useEffect, useState } from 'react';
import {
  Badge,
  Button,
  Popover,
  PopoverSurface,
  PopoverTrigger,
  Text,
  makeStyles,
} from '@fluentui/react-components';
import { CloudArrowUp24Regular, Warning24Regular } from '@fluentui/react-icons';
import {
  assinarMudancasSync,
  contarPendentes,
  estaOnline,
  listarConflitosNaoLidos,
  marcarConflitoComoLido,
  sincronizarFilaSaida,
} from '../lib/offline/syncEngine';
import type { ItemConflito } from '../lib/offline/db';

const useStyles = makeStyles({
  raiz: { display: 'flex', alignItems: 'center', gap: '8px' },
  painel: { width: '360px', padding: '16px', display: 'flex', flexDirection: 'column', gap: '12px' },
  conflito: {
    border: '1px solid #e0e0e0',
    borderRadius: '6px',
    padding: '10px',
    display: 'flex',
    flexDirection: 'column',
    gap: '4px',
  },
});

// Piloto de sincronização offline (DDS, Inspeções, Checklists, APRs — 24/08). Mostra: online x
// offline, quantas ações estão na fila local esperando internet, e conflitos que o servidor
// resolveu automaticamente a favor da versão dele (política acordada: "servidor sempre vence, mas
// avisa").
export function SyncStatusBadge() {
  const estilos = useStyles();
  const [online, setOnline] = useState(estaOnline());
  const [pendentes, setPendentes] = useState(0);
  const [conflitos, setConflitos] = useState<ItemConflito[]>([]);

  useEffect(() => {
    const atualizar = () => {
      setOnline(estaOnline());
      void contarPendentes().then(setPendentes);
      void listarConflitosNaoLidos().then(setConflitos);
    };
    atualizar();
    return assinarMudancasSync(atualizar);
  }, []);

  const temAlgoParaMostrar = !online || pendentes > 0 || conflitos.length > 0;
  if (!temAlgoParaMostrar) {
    return (
      <Badge color="success" appearance="tint" icon={<CloudArrowUp24Regular />}>
        Sincronizado
      </Badge>
    );
  }

  return (
    <Popover>
      <PopoverTrigger disableButtonEnhancement>
        <Button
          appearance="subtle"
          className={estilos.raiz}
          icon={conflitos.length > 0 ? <Warning24Regular /> : <CloudArrowUp24Regular />}
        >
          {!online && <Badge color="informative" appearance="tint">Offline</Badge>}
          {pendentes > 0 && (
            <Badge color="warning" appearance="tint">
              {pendentes} pendente{pendentes > 1 ? 's' : ''}
            </Badge>
          )}
          {conflitos.length > 0 && (
            <Badge color="danger" appearance="tint">
              {conflitos.length} conflito{conflitos.length > 1 ? 's' : ''}
            </Badge>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverSurface className={estilos.painel}>
        <Text weight="semibold">{online ? 'Conectado' : 'Sem conexão'}</Text>

        {pendentes > 0 && (
          <div>
            <Text size={200}>
              {pendentes} ação{pendentes > 1 ? 'ões' : ''} aguardando conexão para sincronizar automaticamente.
            </Text>
            {online && (
              <Button appearance="secondary" size="small" onClick={() => void sincronizarFilaSaida()}>
                Sincronizar agora
              </Button>
            )}
          </div>
        )}

        {conflitos.length === 0 && pendentes === 0 && (
          <Text size={200}>Tudo sincronizado com o servidor.</Text>
        )}

        {conflitos.map((conflito) => (
          <div key={conflito.id} className={estilos.conflito}>
            <Text size={200} weight="semibold">
              {conflito.metodo} {conflito.url}
            </Text>
            <Text size={200}>{conflito.mensagem}</Text>
            <Button
              appearance="transparent"
              size="small"
              onClick={() => conflito.id && void marcarConflitoComoLido(conflito.id)}
            >
              Marcar como visto
            </Button>
          </div>
        ))}
      </PopoverSurface>
    </Popover>
  );
}
