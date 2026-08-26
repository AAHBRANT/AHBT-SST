import { useState } from 'react';
import { Button, Input, Text } from '@fluentui/react-components';
import { Fingerprint24Regular, Phone24Regular } from '@fluentui/react-icons';
import { api, TipoAutenticadorWebAuthn } from '../../lib/api';
import { criarCredencialWebAuthn, estaWebAuthnDisponivel } from '../../lib/webauthn';
import { usePageStyles } from '../pageStyles';

interface AssinaturaTabProps {
  trabalhadorId: string;
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

// Aba de configuração do Motor de Assinatura Eletrônica para este trabalhador (docs/Motor-Assinatura-
// Eletronica.md §3/§5). O Termo de Aceite (MP 2.200-2/2001) e o consentimento LGPD de biometria ainda
// não têm tela própria — o texto jurídico depende de revisão do jurídico e não deve ser aproximado por
// um texto provisório aqui. Até lá, essas duas confirmações são registradas via Swagger
// (POST /api/trabalhadores/{id}/assinatura/termo-aceite e .../consentimento-biometria) e o cadastro de
// biometria abaixo vai simplesmente devolver o erro real do backend se elas ainda não existirem.
export function AssinaturaTab({ trabalhadorId }: AssinaturaTabProps) {
  const estilos = usePageStyles();
  const webAuthnDisponivel = estaWebAuthnDisponivel();

  const [pin, setPin] = useState('');
  const [confirmarPin, setConfirmarPin] = useState('');
  const [salvandoPin, setSalvandoPin] = useState(false);
  const [pinSalvo, setPinSalvo] = useState(false);
  const [erroPin, setErroPin] = useState<string | null>(null);

  const [cadastrando, setCadastrando] = useState<'obra' | 'celular' | null>(null);
  const [erroWebAuthn, setErroWebAuthn] = useState<string | null>(null);
  const [webAuthnCadastrado, setWebAuthnCadastrado] = useState<'obra' | 'celular' | null>(null);

  async function salvarPin() {
    try {
      setSalvandoPin(true);
      setErroPin(null);
      setPinSalvo(false);
      await api.trabalhadores.definirPinAssinatura(trabalhadorId, pin, confirmarPin);
      setPinSalvo(true);
      setPin('');
      setConfirmarPin('');
    } catch (e) {
      setErroPin(extrairMensagemErro(e, 'Falha ao definir o PIN.'));
    } finally {
      setSalvandoPin(false);
    }
  }

  async function cadastrarWebAuthn(tipo: 'obra' | 'celular') {
    try {
      setCadastrando(tipo);
      setErroWebAuthn(null);
      setWebAuthnCadastrado(null);
      const tipoEnum =
        tipo === 'obra' ? TipoAutenticadorWebAuthn.LeitorObra : TipoAutenticadorWebAuthn.CelularProprio;
      const opcoesJson = await api.trabalhadores.iniciarCadastroWebAuthn(trabalhadorId, tipoEnum);
      const respostaJson = await criarCredencialWebAuthn(opcoesJson);
      await api.trabalhadores.confirmarCadastroWebAuthn(trabalhadorId, tipoEnum, opcoesJson, respostaJson);
      setWebAuthnCadastrado(tipo);
    } catch (e) {
      setErroWebAuthn(extrairMensagemErro(e, 'Falha ao cadastrar a credencial biométrica.'));
    } finally {
      setCadastrando(null);
    }
  }

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
      <div className={estilos.card} style={{ maxWidth: 480 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>
          PIN de assinatura (crachá/QR)
        </Text>
        <Text style={{ display: 'block', marginBottom: 12, color: 'var(--colorNeutralForeground3)' }}>
          Usado como método de reserva no quiosque quando a biometria não está disponível.
        </Text>
        {erroPin && <Text className={estilos.erro}>{erroPin}</Text>}
        {pinSalvo && <Text style={{ display: 'block', marginBottom: 8 }}>PIN definido com sucesso.</Text>}
        <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', alignItems: 'flex-end' }}>
          <Input
            type="password"
            placeholder="Novo PIN (4 a 6 dígitos)"
            value={pin}
            onChange={(_, d) => setPin(d.value)}
            disabled={salvandoPin}
          />
          <Input
            type="password"
            placeholder="Confirmar PIN"
            value={confirmarPin}
            onChange={(_, d) => setConfirmarPin(d.value)}
            disabled={salvandoPin}
          />
          <Button appearance="primary" onClick={salvarPin} disabled={salvandoPin || !pin || !confirmarPin}>
            Salvar PIN
          </Button>
        </div>
      </div>

      <div className={estilos.card} style={{ maxWidth: 480 }}>
        <Text weight="semibold" style={{ display: 'block', marginBottom: 4 }}>
          Credencial biométrica (WebAuthn/FIDO2)
        </Text>
        <Text style={{ display: 'block', marginBottom: 12, color: 'var(--colorNeutralForeground3)' }}>
          Exige Termo de Aceite e consentimento de uso de biometria já registrados para este trabalhador.
        </Text>
        {!webAuthnDisponivel && (
          <Text style={{ display: 'block', marginBottom: 12 }}>
            Este navegador/dispositivo não suporta WebAuthn.
          </Text>
        )}
        {erroWebAuthn && <Text className={estilos.erro}>{erroWebAuthn}</Text>}
        {webAuthnCadastrado && (
          <Text style={{ display: 'block', marginBottom: 8 }}>
            Credencial ({webAuthnCadastrado === 'obra' ? 'leitor da obra' : 'celular próprio'}) cadastrada com
            sucesso.
          </Text>
        )}
        {webAuthnDisponivel && (
          <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap' }}>
            <Button
              icon={<Fingerprint24Regular />}
              onClick={() => cadastrarWebAuthn('obra')}
              disabled={cadastrando !== null}
            >
              Cadastrar leitor da obra
            </Button>
            <Button
              icon={<Phone24Regular />}
              onClick={() => cadastrarWebAuthn('celular')}
              disabled={cadastrando !== null}
            >
              Cadastrar celular próprio
            </Button>
          </div>
        )}
      </div>
    </div>
  );
}
