import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Spinner,
} from '@fluentui/react-components';
import { Button, Text, FeedbackInline, designTokens } from '@ui';
import { Warning24Filled } from '@fluentui/react-icons';
import { api, type CatalogoEpi, type CursoTreinamento } from '../../lib/api';

interface RequisitosFuncaoDialogProps {
  funcaoId: string | null;
  trabalhadorNome?: string;
  funcaoNome?: string;
  aoFechar: () => void;
}

// Popup chamativo logo após cadastrar o trabalhador (pedido do usuário, 02/09): hoje o sistema não
// tem nenhum alerta automático avisando que um trabalhador recém-cadastrado precisa dos EPIs e
// treinamentos obrigatórios da função dele (o Motor de Alertas só monitora vencimento de EPI/
// treinamento JÁ entregue/realizado — ver AlertaEngineService). Em vez de um alerta em segundo
// plano (que pode passar despercebido numa lista), a matriz da função é mostrada na hora, num
// diálogo modalType="alert" (sem fechar clicando fora/Esc, só pelo botão) — rígido de propósito.
//
// Vive em components/ (não em pages/), mesmo padrão já usado pelos diálogos de assinatura em
// components/assinatura/: é um Dialog modal bespoke, forma que a camada ui/ não padroniza (só
// ConfirmDialog/useConfirmar para confirmação simples e PainelLateral para painel lateral) — spec §3
// não lista um "Dialog" genérico no inventário. Migrar o conteúdo interno para @ui (Text, Button,
// FeedbackInline) mesmo assim, só a casca do Dialog do Fluent continua direta.
export function RequisitosFuncaoDialog({
  funcaoId,
  trabalhadorNome,
  funcaoNome,
  aoFechar,
}: RequisitosFuncaoDialogProps) {
  const aberto = funcaoId !== null;
  const [carregando, setCarregando] = useState(false);
  const [epis, setEpis] = useState<CatalogoEpi[]>([]);
  const [cursos, setCursos] = useState<CursoTreinamento[]>([]);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!funcaoId) return;
    setCarregando(true);
    setErro(null);
    Promise.all([api.funcoes.listarEpis(funcaoId), api.funcoes.listarTreinamentosObrigatorios(funcaoId)])
      .then(([listaEpis, listaCursos]) => {
        setEpis(listaEpis);
        setCursos(listaCursos);
      })
      .catch((e) => setErro(e instanceof Error ? e.message : 'Falha ao carregar os requisitos da função.'))
      .finally(() => setCarregando(false));
  }, [funcaoId]);

  return (
    <Dialog open={aberto} modalType="alert">
      <DialogSurface>
        <DialogBody>
          <DialogTitle style={{ display: 'flex', alignItems: 'center', gap: 10, color: designTokens.colorAlert }}>
            <Warning24Filled />
            Itens obrigatórios de {trabalhadorNome ?? 'funcionário'}
          </DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}
            {carregando && <Spinner label="Carregando requisitos da função..." />}
            {!carregando && !erro && (
              <>
                <Text>
                  Antes de liberar {trabalhadorNome ?? 'o funcionário'} para o trabalho, providencie os itens
                  obrigatórios da função{funcaoNome ? ` "${funcaoNome}"` : ''} abaixo.
                </Text>

                <div>
                  <Text weight="semibold" block style={{ marginBottom: 6 }}>
                    EPIs obrigatórios ({epis.length})
                  </Text>
                  {epis.length === 0 ? (
                    <Text size={200}>Nenhum EPI obrigatório cadastrado para esta função.</Text>
                  ) : (
                    <ul style={{ margin: 0, paddingLeft: 20 }}>
                      {epis.map((epi) => (
                        <li key={epi.id}>
                          <Text>{epi.fabricante ? `${epi.nome} (${epi.fabricante})` : epi.nome}</Text>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                <div>
                  <Text weight="semibold" block style={{ marginBottom: 6 }}>
                    Treinamentos obrigatórios ({cursos.length})
                  </Text>
                  {cursos.length === 0 ? (
                    <Text size={200}>Nenhum treinamento obrigatório cadastrado para esta função.</Text>
                  ) : (
                    <ul style={{ margin: 0, paddingLeft: 20 }}>
                      {cursos.map((curso) => (
                        <li key={curso.id}>
                          <Text>
                            {curso.normaReferencia ? `${curso.nome} (${curso.normaReferencia})` : curso.nome}
                          </Text>
                        </li>
                      ))}
                    </ul>
                  )}
                </div>

                {epis.length === 0 && cursos.length === 0 && (
                  <Text size={200} style={{ color: designTokens.colorNeutralMedium }}>
                    Nenhum item obrigatório cadastrado na matriz desta função ainda — cadastre em Operação → EPI
                    e em Treinamentos → Matriz de Treinamento por Função.
                  </Text>
                )}
              </>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="primary" onClick={aoFechar} disabled={carregando}>
              Ciente, entendi
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
