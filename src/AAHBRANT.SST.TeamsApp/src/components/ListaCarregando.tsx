import { Skeleton, SkeletonItem } from '@fluentui/react-components';

interface ListaCarregandoProps {
  linhas?: number;
}

// Indicador de carregamento pras telas de listagem (pedido do usuário, 31/08) — antes disso, quase
// nenhuma tela de CRUD mostrava qualquer coisa enquanto a chamada inicial à API estava em voo; a
// lista simplesmente "aparecia" vazia e depois se preenchia, indistinguível de um cadastro vazio de
// verdade. Usar no lugar do conteúdo da lista enquanto o estado de carregamento inicial for true.
export function ListaCarregando({ linhas = 4 }: ListaCarregandoProps) {
  return (
    <Skeleton aria-label="Carregando" style={{ display: 'flex', flexDirection: 'column', gap: 10, padding: '4px 0' }}>
      {Array.from({ length: linhas }).map((_, indice) => (
        <SkeletonItem key={indice} style={{ height: 40, borderRadius: 6 }} />
      ))}
    </Skeleton>
  );
}
