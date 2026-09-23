import { useEffect, useState } from 'react';
import {
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
} from '@fluentui/react-components';
import { Button, Checkbox, Text, FeedbackInline } from '@ui';
import { Fingerprint24Regular } from '@fluentui/react-icons';
import { api } from '../../lib/api';
import { capturarDigitalBrutaLocal, estaAgenteLocalDisponivel } from '../../lib/agenteBiometricoLocal';

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
// registrados antes de aceitar qualquer biometria. Esta tela confirma administrativamente que os
// termos físicos do pacote admissional/funcional foram assinados e deixa explícito que o
// consentimento cobre biometria digital e reconhecimento facial, sem trocar a revisão jurídica do
// documento físico por um texto improvisado no sistema.
//
// Vive em components/ (não em pages/), mesmo padrão de components/assinatura/ e de
// RequisitosFuncaoDialog.tsx nesta mesma pasta: Dialog modal bespoke sem equivalente em ui/ (spec §3
// só padroniza ConfirmDialog/useConfirmar e PainelLateral). Conteúdo interno migrado para @ui.
export function CadastroDigitalDialog({
  trabalhadorId,
  trabalhadorNome,
  aoFechar,
  aoConcluir,
}: CadastroDigitalDialogProps) {
  const [termoFisicoConfirmado, setTermoFisicoConfirmado] = useState(false);
  const [consentimentosSalvos, setConsentimentosSalvos] = useState(false);
  const [salvandoConsentimentos, setSalvandoConsentimentos] = useState(false);

  const [agenteDisponivel, setAgenteDisponivel] = useState<boolean | null>(null);
  const [capturando, setCapturando] = useState(false);
  const [cadastrada, setCadastrada] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const aberto = trabalhadorId !== null;
  const nomeFuncionario = trabalhadorNome ?? 'funcionário';

  // Todo caminho de fechar/reabrir este diálogo limpa o estado do fluxo anterior — inclusive quando
  // reaberto para outro trabalhador (chave trabalhadorId), senão o passo/erro de uma tentativa
  // anterior vazaria para o próximo cadastro.
  useEffect(() => {
    if (!aberto) return;
    setTermoFisicoConfirmado(false);
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
            {erro && <FeedbackInline tom="erro" aoFechar={() => setErro(null)}>{erro}</FeedbackInline>}

            {!consentimentosSalvos ? (
              <>
                <Text>
                  Antes de cadastrar biometria, confirme que {nomeFuncionario} assinou os termos
                  formais exigidos para assinatura eletrônica e tratamento de dado biométrico sensível.
                </Text>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
                  <Text weight="semibold">Termos obrigatórios</Text>
                  <Text>
                    1. Termo de Aceite de Assinatura Eletrônica: autoriza o uso de assinatura eletrônica
                    nos documentos de SST, com registro de data, hora, documento, método de autenticação e
                    trilha de auditoria.
                  </Text>
                  <Text>
                    2. Termo de Consentimento LGPD para Biometria: autoriza o tratamento de dado
                    biométrico sensível para identificação e autenticação, incluindo digital em leitor
                    local e reconhecimento facial via Azure Face API quando habilitado para a obra.
                  </Text>
                  <Text>
                    O funcionário deve ter sido informado sobre finalidade, segurança, acesso restrito,
                    possibilidade de método alternativo e direitos previstos na LGPD.
                  </Text>
                </div>
                <Text>
                  Ao continuar, o sistema registrará a data de aceite desses dois termos no cadastro do
                  trabalhador.
                </Text>
                <Checkbox
                  checked={termoFisicoConfirmado}
                  onChange={(_, d) => setTermoFisicoConfirmado(!!d.checked)}
                  label="Confirmo que os termos físicos de assinatura eletrônica e consentimento biométrico foram assinados pelo funcionário."
                />
              </>
            ) : (
              <>
                {agenteDisponivel === null && <Text>Verificando o leitor local…</Text>}
                {agenteDisponivel === false && (
                  <FeedbackInline tom="erro">
                    Leitor Futronic não encontrado nesta máquina. Verifique se o leitor está conectado e se
                    o Agente Biométrico está em execução, depois tente novamente.
                  </FeedbackInline>
                )}
                {agenteDisponivel && !cadastrada && (
                  <Text>Posicione o dedo do funcionário no leitor e clique em "Capturar digital".</Text>
                )}
                {cadastrada && (
                  <FeedbackInline tom="sucesso">Digital cadastrada com sucesso.</FeedbackInline>
                )}
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
                disabled={!termoFisicoConfirmado || salvandoConsentimentos}
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
