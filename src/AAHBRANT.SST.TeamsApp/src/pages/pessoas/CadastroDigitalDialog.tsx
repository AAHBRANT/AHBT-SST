import { useEffect, useState } from 'react';
import {
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Text,
} from '@fluentui/react-components';
import { Fingerprint24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { capturarDigitalBrutaLocal, estaAgenteLocalDisponivel } from '../../lib/agenteBiometricoLocal';
import { usePageStyles } from '../pageStyles';

interface CadastroDigitalDialogProps {
  trabalhadorId: string | null;
  trabalhadorNome?: string;
  aoFechar: () => void;
  aoConcluir?: () => void;
}

function extrairMensagemErro(e: unknown, fallback: string): string {
  if (!(e instanceof Error)) return fallback;
  const trechoJson = e.message.match(/\{.*\}$/);
  if (trechoJson) {
    try {
      const corpo = JSON.parse(trechoJson[0]) as { erro?: string };
      if (typeof corpo.erro === 'string') return corpo.erro;
    } catch {
      // corpo não era JSON — cai no fallback abaixo
    }
  }
  return e.message || fallback;
}

// Cadastro de digital (leitor local Futronic FS80H) logo após criar o trabalhador — pedido do
// usuário (30/08): a digital deve ser cadastrada no ato do cadastro, mas se o leitor não estiver
// disponível na máquina (ou o operador precisar adiar), o trabalhador fica salvo mesmo assim,
// só marcado como "Digital pendente" na lista (ver TrabalhadoresTab) até completar depois.
//
// O backend exige Termo de Aceite (MP 2.200-2/2001) e consentimento LGPD de biometria já
// registrados antes de aceitar o template (ver CadastrarTemplateBiometricoCommand) — e nenhuma
// tela do sistema ainda pergunta isso ao operador (mesma observação já registrada em
// AssinaturaTab.tsx). O texto abaixo é PROVISÓRIO até o jurídico revisar a redação oficial —
// sinalizado visualmente para não ser confundido com um termo já validado.
export function CadastroDigitalDialog({
  trabalhadorId,
  trabalhadorNome,
  aoFechar,
  aoConcluir,
}: CadastroDigitalDialogProps) {
  const estilos = usePageStyles();
  const [aceiteTermo, setAceiteTermo] = useState(false);
  const [aceiteBiometria, setAceiteBiometria] = useState(false);
  const [consentimentosSalvos, setConsentimentosSalvos] = useState(false);
  const [salvandoConsentimentos, setSalvandoConsentimentos] = useState(false);

  const [agenteDisponivel, setAgenteDisponivel] = useState<boolean | null>(null);
  const [capturando, setCapturando] = useState(false);
  const [cadastrada, setCadastrada] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const aberto = trabalhadorId !== null;

  useEffect(() => {
    if (!aberto) return;
    setAceiteTermo(false);
    setAceiteBiometria(false);
    setConsentimentosSalvos(false);
    setAgenteDisponivel(null);
    setCapturando(false);
    setCadastrada(false);
    setErro(null);
  }, [aberto, trabalhadorId]);

  useEffect(() => {
    if (!consentimentosSalvos) return;
    estaAgenteLocalDisponivel().then(setAgenteDisponivel);
  }, [consentimentosSalvos]);

  async function confirmarConsentimentos() {
    if (!trabalhadorId) return;
    try {
      setSalvandoConsentimentos(true);
      setErro(null);
      await api.trabalhadores.registrarTermoAceiteAssinatura(trabalhadorId);
      await api.trabalhadores.registrarConsentimentoBiometria(trabalhadorId);
      setConsentimentosSalvos(true);
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao registrar os termos de aceite.'));
    } finally {
      setSalvandoConsentimentos(false);
    }
  }

  async function capturarDigital() {
    if (!trabalhadorId) return;
    try {
      setCapturando(true);
      setErro(null);
      const templateBase64 = await capturarDigitalBrutaLocal();
      await api.trabalhadores.cadastrarBiometriaLocal(trabalhadorId, templateBase64);
      setCadastrada(true);
      aoConcluir?.();
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao cadastrar a digital.'));
    } finally {
      setCapturando(false);
    }
  }

  return (
    <Dialog open={aberto} onOpenChange={(_, d) => !d.open && aoFechar()}>
      <DialogSurface>
        <DialogBody>
          <DialogTitle>Cadastrar digital{trabalhadorNome ? ` — ${trabalhadorNome}` : ''}</DialogTitle>
          <DialogContent style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
            {erro && <Text className={estilos.erro}>{erro}</Text>}

            {!consentimentosSalvos ? (
              <>
                <Text style={{ display: 'block', color: 'var(--colorPaletteYellowForeground1, #9a6b04)' }}>
                  ⚠️ Texto provisório, pendente de validação jurídica — ainda não existe uma tela oficial
                  para estas confirmações no sistema.
                </Text>
                <Checkbox
                  checked={aceiteTermo}
                  onChange={(_, d) => setAceiteTermo(!!d.checked)}
                  label="Confirmo que o trabalhador foi informado e aceita o uso de assinatura eletrônica (MP 2.200-2/2001)."
                />
                <Checkbox
                  checked={aceiteBiometria}
                  onChange={(_, d) => setAceiteBiometria(!!d.checked)}
                  label="Confirmo que o trabalhador consentiu com o uso de dado biométrico (digital), conforme LGPD."
                />
              </>
            ) : (
              <>
                {agenteDisponivel === null && <Text>Verificando o leitor local…</Text>}
                {agenteDisponivel === false && (
                  <Text style={{ display: 'block', color: 'var(--colorPaletteRedForeground1)' }}>
                    Leitor Futronic não encontrado nesta máquina. Verifique se o leitor está conectado e se
                    o Agente Biométrico está em execução, depois tente novamente.
                  </Text>
                )}
                {agenteDisponivel && !cadastrada && (
                  <Text>Posicione o dedo do trabalhador no leitor e clique em "Capturar digital".</Text>
                )}
                {cadastrada && <Text>Digital cadastrada com sucesso.</Text>}
              </>
            )}
          </DialogContent>
          <DialogActions>
            <Button appearance="secondary" onClick={aoFechar}>
              {cadastrada ? 'Concluir' : 'Fazer depois'}
            </Button>
            {!consentimentosSalvos && (
              <Button
                appearance="primary"
                onClick={confirmarConsentimentos}
                disabled={!aceiteTermo || !aceiteBiometria || salvandoConsentimentos}
              >
                Continuar
              </Button>
            )}
            {consentimentosSalvos && !cadastrada && (
              <Button
                appearance="primary"
                icon={<Fingerprint24Regular />}
                onClick={capturarDigital}
                disabled={!agenteDisponivel || capturando}
              >
                Capturar digital
              </Button>
            )}
            {consentimentosSalvos && agenteDisponivel === false && (
              <Button
                appearance="secondary"
                onClick={() => estaAgenteLocalDisponivel().then(setAgenteDisponivel)}
              >
                Tentar novamente
              </Button>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  );
}
