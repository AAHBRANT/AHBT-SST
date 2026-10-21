# Agente de Biometria — instalação e atualização automática

## Para quem instala num posto

1. Baixe `Setup-AgenteBiometria-<versão>.exe`.
2. Dê dois cliques nele. Não precisa ser administrador.
3. Na primeira vez, preencha os dados do leitor. Eles aparecem no SST ao registrar o leitor na obra:
   - Id do dispositivo;
   - Segredo;
   - Chave de criptografia;
   - URLs (já vêm preenchidas).
4. Pronto: o agente inicia junto com o Windows e passa a se atualizar sozinho.

Num posto que já tem o agente, inclusive instalado pelo antigo `instalar.ps1`, o mesmo Setup só atualiza, sem pedir nada. A configuração do posto e a chave de assinatura das leituras são mantidas.

O log do instalador fica em `%LOCALAPPDATA%\AAHBRANT\instalador-agente.log`.

## Como a atualização automática funciona

1. A cada 6 horas o agente lê o `manifesto.json` no endereço configurado.
2. Ele só aceita o manifesto se a assinatura bater com a chave pública gravada no próprio agente.
3. Ele só considera uma versão **maior** que a instalada.
4. Ele espera o quiosque ficar 3 minutos sem uso. Nunca troca de versão no meio de uma leitura.
5. Ele baixa o Setup e confere o SHA-256 contra o que está no manifesto.
6. O Setup fecha o agente, guarda a versão atual em `AgenteBiometria.anterior`, instala a nova e confere se ela responde.
   - Se a nova não responder em 30 segundos, ele volta para a anterior.

Quem conseguisse trocar os arquivos no servidor de atualização não conseguiria fazer os postos rodarem outro programa: sem a chave privada, a assinatura não bate.

## Para quem publica uma versão

### Só na primeira vez: gerar a chave

```powershell
.\scripts\agente-biometria\publicar.ps1 -GerarChave -ChavePrivada C:\Cofre\agente-atualizacao.chave-privada.pem
```

- Commite `src/AAHBRANT.SST.AgenteBiometria/Atualizacao/chave-publica-atualizacao.pem`.
- Guarde a chave **privada** num cofre (Key Vault, cofre de senhas). Ela nunca vai para o git nem para os postos.
- Se a chave privada for perdida, os postos param de aceitar atualizações e precisam receber um Setup novo manualmente.

### A cada versão

```powershell
.\scripts\agente-biometria\publicar.ps1 -Versao 1.2.0 -UrlBase https://<endereço>/agente-biometria -ChavePrivada C:\Cofre\agente-atualizacao.chave-privada.pem
```

Depois, suba `Setup-AgenteBiometria-1.2.0.exe` e `manifesto.json` (estão em `dist\agente-biometria`) para o endereço de `-UrlBase`.

A versão precisa ser maior que a anterior.

A `ftrScanAPI.dll` da Futronic não fica no git: copie-a para `src\AAHBRANT.SST.AgenteBiometria\Native\` ou informe `-SdkDll`.

## Limitações conhecidas

- O Setup tem cerca de 140 MB, porque leva o .NET embutido duas vezes: uma vez no agente e outra no próprio Setup.
- O endereço de hospedagem (`-UrlBase`) precisa ser HTTPS e acessível pelos postos. A sugestão é um container público de Azure Blob Storage: os arquivos são públicos, mas assinados.
- O primeiro Setup com a atualização automática precisa ser instalado à mão em cada posto. A partir dele, os postos se atualizam sozinhos.
