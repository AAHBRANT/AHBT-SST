import { useEffect, useRef, useState } from 'react';
import {
  Badge,
  Button,
  Input,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  tokens,
} from '@fluentui/react-components';
import { Checkmark24Filled, Fingerprint24Regular } from '@fluentui/react-icons';
import { api, metodoAutenticacaoAssinaturaLabel, type DocumentoAssinatura } from '../../lib/api';
import { estaWebAuthnDisponivel, obterAssercaoWebAuthn } from '../../lib/webauthn';
import { usePageStyles } from '../../pages/pageStyles';

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

export interface AssinaturaQuiosqueProps {
  entidadeTipo: string;
  entidadeId: string;
}

// Bloco de quiosque do Motor de Assinatura Eletrônica (docs/Motor-Assinatura-Eletronica.md §5,
// etapa 14 — extraído de AssinarDdsPage.tsx para permitir reuso em Treinamento/EPI/APR/PT/Inspeções
// sem duplicar a lógica de autenticação). O backend já é genérico desde a etapa 6
// (EntidadeTipo/EntidadeId); esta extração torna o frontend igualmente plugável — para usar em um
// novo módulo, basta renderizar <AssinaturaQuiosque entidadeTipo="..." entidadeId={id} /> dentro da
// página de detalhe do módulo. Etapa 13 acrescentou a biometria WebAuthn/FIDO2 (leitor da obra) como
// método principal — exibida quando o navegador suporta WebAuthn — com o crachá/QR + PIN sempre visível
// logo abaixo como reserva manual (não há como o quiosque saber de antemão se o leitor físico vai
// responder, então não escondemos a alternativa). A troca de método não exige mudança no backend, pois
// ambos os fluxos convergem em IRegistradorAssinaturaService.
export function AssinaturaQuiosque({ entidadeTipo, entidadeId }: AssinaturaQuiosqueProps) {
  const estilos = usePageStyles();
  const [documento, setDocumento] = useState<DocumentoAssinatura | null>(null);
  const [uid, setUid] = useState('');
  const [pin, setPin] = useState('');
  const [processando, setProcessando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [ultimoAssinante, setUltimoAssinante] = useState<string | null>(null);
  const uidInputRef = useRef<HTMLInputElement>(null);
  const pinInputRef = useRef<HTMLInputElement>(null);
  const webAuthnDisponivel = estaWebAuthnDisponivel();

  async function carregar() {
    try {
      setErro(null);
      await api.assinatura.criar(entidadeTipo, entidadeId);
      const doc = await api.assinatura.obter(entidadeTipo, entidadeId);
      setDocumento(doc);
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao carregar a tela de assinatura.'));
    }
  }

  useEffect(() => {
    carregar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [entidadeTipo, entidadeId]);

  useEffect(() => {
    uidInputRef.current?.focus();
  }, [documento]);

  async function assinar() {
    if (!documento || !uid || !pin) return;
    try {
      setProcessando(true);
      setErro(null);
      setUltimoAssinante(null);
      const signatario = await api.assinatura.assinar(documento.id, uid, pin);
      setUltimoAssinante(signatario.trabalhadorNome);
      setUid('');
      setPin('');
      const doc = await api.assinatura.obter(entidadeTipo, entidadeId);
      setDocumento(doc);
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha ao assinar.'));
    } finally {
      setProcessando(false);
      uidInputRef.current?.focus();
    }
  }

  async function assinarComBiometria() {
    if (!documento) return;
    try {
      setProcessando(true);
      setErro(null);
      setUltimoAssinante(null);
      // trabalhadorId omitido: leitor compartilhado da obra — a credencial "discoverable" resolve a
      // identidade só depois da resposta do autenticador.
      const opcoesJson = await api.assinatura.iniciarAssinaturaWebAuthn();
      const respostaJson = await obterAssercaoWebAuthn(opcoesJson);
      const signatario = await api.assinatura.confirmarAssinaturaWebAuthn(documento.id, opcoesJson, respostaJson);
      setUltimoAssinante(signatario.trabalhadorNome);
      const doc = await api.assinatura.obter(entidadeTipo, entidadeId);
      setDocumento(doc);
    } catch (e) {
      setErro(extrairMensagemErro(e, 'Falha na autenticação biométrica. Use o crachá/QR + PIN abaixo.'));
    } finally {
      setProcessando(false);
    }
  }

  return (
    <div>
      {erro && <Text className={estilos.erro}>{erro}</Text>}

      {webAuthnDisponivel && (
        <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
          <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
            Autenticação biométrica (leitor da obra)
          </Text>
          <Button
            appearance="primary"
            size="large"
            icon={<Fingerprint24Regular />}
            onClick={assinarComBiometria}
            disabled={processando || !documento}
          >
            Autenticar com biometria
          </Button>
        </div>
      )}

      <div className={estilos.card} style={{ marginBottom: 16, maxWidth: 480 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 12 }}>
          {webAuthnDisponivel
            ? 'Ou aproxime o crachá/QR do leitor e digite o PIN'
            : 'Aproxime o crachá/QR do leitor e digite o PIN'}
        </Text>
        <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
          <Input
            ref={uidInputRef}
            placeholder="Crachá / QR"
            value={uid}
            onChange={(_, d) => setUid(d.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') pinInputRef.current?.focus();
            }}
            disabled={processando}
            size="large"
          />
          <Input
            ref={pinInputRef}
            type="password"
            placeholder="PIN"
            value={pin}
            onChange={(_, d) => setPin(d.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') assinar();
            }}
            disabled={processando}
            size="large"
          />
          <Button appearance="primary" size="large" onClick={assinar} disabled={processando || !uid || !pin}>
            Assinar
          </Button>
          {ultimoAssinante && (
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Checkmark24Filled style={{ color: tokens.colorPaletteGreenForeground1 }} />
              <Text>Assinatura registrada: {ultimoAssinante}</Text>
            </div>
          )}
        </div>
      </div>

      <div className={estilos.card}>
        <div className={estilos.toolbar}>
          <Text weight="semibold">Assinaturas registradas ({documento?.signatarios.length ?? 0})</Text>
        </div>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Nome</TableHeaderCell>
              <TableHeaderCell>Método</TableHeaderCell>
              <TableHeaderCell>Horário</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {documento?.signatarios.map((signatario) => (
              <TableRow key={signatario.trabalhadorId}>
                <TableCell>{signatario.trabalhadorNome}</TableCell>
                <TableCell>
                  <Badge appearance="tint">{metodoAutenticacaoAssinaturaLabel[signatario.metodoAutenticacao]}</Badge>
                </TableCell>
                <TableCell>{new Date(signatario.assinadoEm).toLocaleTimeString('pt-BR')}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
