import { useState } from 'react';
import { Badge, Button, Text } from '@fluentui/react-components';
import { Link24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { usePageStyles } from '../pageStyles';

interface TelegramTabProps {
  trabalhadorId: string;
  telegramVinculado: boolean;
  telegramCodigoVinculo?: string | null;
  aoAtualizar: () => void;
}

export function TelegramTab({ trabalhadorId, telegramVinculado, telegramCodigoVinculo, aoAtualizar }: TelegramTabProps) {
  const estilos = usePageStyles();
  const [erro, setErro] = useState<string | null>(null);
  const [gerando, setGerando] = useState(false);
  const [link, setLink] = useState<string | null>(null);

  async function gerarVinculo() {
    try {
      setGerando(true);
      setErro(null);
      const resultado = await api.trabalhadores.gerarVinculoTelegram(trabalhadorId);
      setLink(resultado.linkTelegram);
      aoAtualizar();
    } catch (e) {
      setErro(e instanceof Error ? e.message : 'Falha ao gerar vínculo de Telegram.');
    } finally {
      setGerando(false);
    }
  }

  return (
    <div className={estilos.card}>
      <div className={estilos.toolbar}>
        <Text weight="semibold">Vínculo de Telegram</Text>
      </div>

      {erro && <Text className={estilos.erro}>{erro}</Text>}

      <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: 12 }}>
        <Text>Status:</Text>
        {telegramVinculado ? (
          <Badge color="success" appearance="tint">
            Vinculado
          </Badge>
        ) : (
          <Badge color="danger" appearance="tint">
            Não vinculado
          </Badge>
        )}
      </div>

      {!telegramVinculado && (
        <>
          <Text style={{ display: 'block', marginBottom: 12 }}>
            Gere um código de vínculo e peça para o funcionário abrir o link no Telegram e enviar a mensagem
            inicial. O bot não pode iniciar a conversa — o funcionário precisa mandar a primeira mensagem.
          </Text>
          <div className={estilos.formActions} style={{ marginBottom: 12 }}>
            <Button appearance="primary" icon={<Link24Regular />} onClick={gerarVinculo} disabled={gerando}>
              Gerar código de vínculo
            </Button>
          </div>
          {(link || telegramCodigoVinculo) && (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
              <Text>
                Código: <strong>{telegramCodigoVinculo ?? '—'}</strong>
              </Text>
              {link && (
                <Text>
                  Link: <a href={link} target="_blank" rel="noreferrer">{link}</a>
                </Text>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
}
