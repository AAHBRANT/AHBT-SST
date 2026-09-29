# Piloto do leitor de digital (Futronic) — roteiro de implantação

Versão 1 · 29/09/2026 · Escopo: **um PC de obra**, antes de espalhar para as demais.

Este roteiro cobre o que falta depois do código pronto: colocar em homologação (hml), registrar o agente,
instalar no PC e validar o caminho **site HTTPS → agente local**, que é o principal risco ainda não testado.

## 0. Pré-requisitos

| Item | Detalhe |
|---|---|
| Leitor | Futronic FS80H (USB). Conferir o modelo na etiqueta. |
| PC da obra | Windows 10/11, com o leitor conectado. Não precisa instalar .NET (o agente leva o runtime embutido). |
| Driver | `ftrDriverSetup_win8_whql_3471.zip` (versão 10.0.0.1, Windows 8/10/11) — site da Futronic, área de downloads do FS80H. |
| SDK | `ftrScanAPI.dll` de **32 bits** (pacote `ftrScanApiEx_v4.5.zip`, mesmo site). Não é versionada no git. |
| Chave de biometria | Valor de `Lgpd:ChaveCriptografiaBiometriaBase64` configurado na API de hml. Peça a quem administra os segredos (Azure). O agente precisa da **mesma** chave. |

## 1. Colocar em hml (feito por quem faz o deploy)

1. Push e PR das alterações. **Atenção às migrations** desta entrega:
   - `HabilitarMetodosAssinaturaTodasObras`: liga digital + facial em **todas** as obras (decisão do usuário).
   - `AdicionarFotosCadastroFacial`: tabela das fotos usadas no cadastro facial.
   - `AdicionarSelecaoFuncionariosDds`: seleção de funcionários do DDS.
2. Deploy da API e do app web (hml), como já é feito hoje.
3. Conferir em hml: **Administração → Leitores de digital** aparece e lista vazia.

## 2. Registrar o leitor da obra

1. Em **Administração → Leitores de digital**, escolher a obra e dar um nome ao posto (ex.: "PC da portaria").
2. Clicar **Registrar leitor**. O diálogo mostra **Id**, **segredo** e o **comando de instalação**.
   O segredo **não aparece de novo**. Se perder, revogue o leitor e registre outro.
3. Copiar o comando de instalação (já vem com a URL da API e a origem do app preenchidas).

## 3. Gerar o pacote do agente (uma vez)

No repositório, com a `ftrScanAPI.dll` (32 bits) em `src\AAHBRANT.SST.AgenteBiometria\Native\`:

```powershell
.\scripts\agente-biometria\publicar.ps1
```

Gera `dist\agente-biometria.zip` (~76 MB, com o .NET embutido e o `instalar.ps1`).

## 4. Instalar no PC da obra

1. Instalar o driver Futronic (10.0.0.1) e conectar o leitor. No Gerenciador de Dispositivos deve aparecer
   "Futronic Fingerprint Scanner" sem alerta.
2. Copiar o `.zip` para o PC e extrair.
3. No PowerShell, na pasta extraída, colar o comando copiado no passo 2 (ele pede a **chave de biometria**):

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\instalar.ps1 -DispositivoId <id> -SegredoDispositivo '<segredo>' -BackendBaseUrl '<url da API>' -OrigemPermitida '<url do app>'
   ```

4. O instalador copia o agente para `%LOCALAPPDATA%\AAHBRANT\AgenteBiometria`, grava a configuração (só o usuário
   atual lê o arquivo), cria o início automático com o Windows e inicia o agente.
5. Em até 2 minutos o leitor aparece como **Conectado** em **Administração → Leitores de digital**.

Para remover: `.\instalar.ps1 -Desinstalar`.

## 5. Validar (o ponto crítico)

Marque cada item no PC do piloto. Se algum falhar, anote a mensagem exata.

| # | Teste | Esperado |
|---|---|---|
| 1 | Abrir o app **em hml (HTTPS)** no Chrome/Edge e cadastrar a digital de um trabalhador de teste (Perfil → Cofre de Assinaturas → Capturar digital). O cadastro pede **duas leituras do mesmo dedo** (1ª: apoiar; 2ª: tirar o dedo e apoiar de novo). | Botão habilitado; as etapas aparecem na tela; ao fim, "Digital cadastrada com sucesso". |
| 1b | No mesmo cadastro, usar **dedos diferentes** nas duas leituras. | Recusado: "As duas leituras não coincidiram…" (nada é gravado). |
| 1c | No mesmo cadastro, **não tirar o dedo** entre as leituras. | Aviso "O dedo não foi retirado do leitor…". |
| 2 | Repetir **dentro do Teams** (aba do app). | Igual ao item 1. |
| 3 | Assinar um documento pelo quiosque com o dedo cadastrado (ex.: DDS, PT ou EPI). | Assinatura registrada e **bipe**. |
| 4 | Tentar assinar com **outro dedo/pessoa**. | Recusado ("abaixo do limiar"). |
| 5 | Sem apoiar o dedo por 15 s. | Mensagem "Nenhum dedo detectado…" (sem erro técnico). |
| 6 | Cadastrar a facial (guia com OK antes da câmera) e assinar por facial. | Foto guardada no perfil; assinatura registrada e bipe. |
| 7 | No DDS, confirmar presença por digital e por facial em funcionários diferentes. | Presenças registradas nos dois métodos. |

### Se o item 1 ou 2 falhar (site HTTPS não fala com o agente)

O navegador trata a chamada de uma página pública para `127.0.0.1` como acesso à rede privada. O agente já
responde ao pré-teste do navegador (`Access-Control-Allow-Private-Network`) **apenas para a origem configurada**.
Verifique, nesta ordem:

1. `OrigemPermitida` do agente é **exatamente** a URL em que o app abre (sem barra final, mesmo `https`/domínio).
2. O navegador não pediu ou negou a permissão de "rede local"/"dispositivos na sua rede" para o site.
3. Abra `http://127.0.0.1:5251/api/dispositivo` no próprio PC: deve responder um JSON com o Id do dispositivo.
4. O agente está rodando (ícone na bandeja do Windows; processo `AAHBRANT.SST.AgenteBiometria`).
5. Se só falhar **no Teams**, registre como pendência: pode exigir outra estratégia (ex.: abrir o quiosque no navegador).

## 6. Critérios para sair do piloto

- Testes 1 a 7 (incluindo 1b e 1c) aprovados por, no mínimo, 5 pessoas diferentes (vários dedos), sem falso aceite no teste 4.
- Consentimento LGPD revisado pelo jurídico (ver `docs/juridico/consentimento-biometria-rascunho.md`).
- Resposta da Futronic sobre o SDK oficial de comparação avaliada (hoje o reconhecimento usa a biblioteca
  gratuita SourceAFIS).

## 7. Operação

- **PC perdido ou trocado:** revogar o leitor em Administração → Leitores de digital e registrar um novo.
- **Novo trabalhador com digital:** o agente busca as digitais novas sozinho a cada 2 minutos.
- **Situação do leitor:** "Conectado" (sincronizou nos últimos 10 min), "Sem sincronizar" (agente parado ou sem
  internet) ou "Nunca conectou" (instalação não concluída).
