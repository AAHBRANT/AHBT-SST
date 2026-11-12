# Agente de Câmeras (SST-APP)

Serviço do Windows que fica num PC da obra, lê câmeras IP por RTSP (via `ffmpeg.exe`), detecta movimento e envia
quadros JPEG para a API do SST-APP. Não usa navegador nem usuário logado. Substitui a ponte provisória em Python.

## Antes de instalar

1. **ffmpeg (aviso de licença):** o agente NÃO traz o ffmpeg. Coloque manualmente o `ffmpeg.exe` (build **LGPL**) em
   `C:\Program Files\AAHBRANT\AgenteCameras\ffmpeg\ffmpeg.exe` (ou informe outro caminho em `CaminhoFfmpeg`).
   A licença de redistribuição ainda está **pendente de validação jurídica**: não distribua o binário junto do
   agente até essa validação. Sem o arquivo, o serviço sobe, registra o erro e tenta de novo a cada minuto.
2. **Cadastro no SST-APP:** cadastre o agente (a tela mostra o AgenteId e o Segredo uma única vez), o equipamento
   (host, porta, usuário e senha da câmera) e as câmeras. **As câmeras não precisam mais ser escritas neste PC:** o
   agente as lê do cadastro (veja "Câmeras vêm do cadastro" abaixo).
3. SDK do .NET 8 na máquina que roda `instalar.ps1` (o script publica o projeto).

## Instalar

PowerShell **como Administrador**, a partir da raiz do repositório:

```powershell
.\scripts\agente-cameras\instalar.ps1
```

O script publica (self-contained, win-x64), copia para `C:\Program Files\AAHBRANT\AgenteCameras`, cria o serviço
`AAHBRANT.SST.AgenteCameras` (início automático atrasado, reinício automático em falha, conta LocalService) e cria
`C:\ProgramData\AAHBRANT\AgenteCameras\appsettings.local.json` a partir do exemplo. O serviço fica **parado**.

Preencha o `appsettings.local.json` (seção `AgenteCameras`) com **só três valores**: `ApiBaseUrl`, `AgenteId` e
`Segredo`. Depois:

```powershell
Start-Service AAHBRANT.SST.AgenteCameras
```

- `Segredo` e `Senha` podem ser escritos em texto na primeira vez: na partida o serviço os regrava protegidos
  (DPAPI, escopo da máquina, prefixo `dpapi:`). O valor protegido só funciona **neste** PC; ao trocar de máquina,
  escreva o valor em texto de novo.
- `AgenteId` deve ser o **GUID** exibido no SST-APP. Valor inválido impede a partida.
- `ApiBaseUrl` pode ter caminho (ex.: `https://host/sst`, com ou sem barra final); o caminho é sempre preservado.
- O arquivo é lido na partida: depois de editar `ApiBaseUrl`, `AgenteId` ou `Segredo`, reinicie o serviço
  (`Restart-Service`). Mudanças no cadastro de câmeras **não** exigem reiniciar (veja abaixo).
- URL RTSP padrão (câmera Xiongmai/iCSee testada):
  `rtsp://{host}:{porta}/user={usuario}&password={senha}&channel={canal}&stream={stream}.sdp?real_stream`.
  Outro formato: `ModeloUrl` com os mesmos marcadores (`{host} {porta} {usuario} {senha} {canal} {stream}`).
- Limites da fila offline (opcionais): `FilaIdadeMaximaHoras` (24) e `FilaTamanhoMaximoMb` (500).

## Câmeras vêm do cadastro

A cada `intervaloConsultaSegundos` (15 s, definido pelo servidor) o agente consulta
`GET /api/agentes-camera/{AgenteId}/configuracao`. Essa consulta é também o **sinal de vida**: a tela mostra
"Agente sem contato" se passar de 3 min sem ela. A resposta traz as câmeras do agente e os pedidos de
"Testar conexão" feitos na tela.

- **Câmera nova** no cadastro (ou ativada) começa a ser lida em até 1 minuto. **Câmera removida ou desligada** para
  de ser lida. Mudou host, porta, usuário, senha, canal, stream ou intervalo: só aquela câmera é reiniciada. Câmera
  sem mudança não é reiniciada, e nenhuma delas interrompe as outras nem o envio da fila offline. Mudar só o nome não
  reinicia (o nome novo aparece no log na próxima religação).
- **Servidor fora do ar** (sem internet, 5xx, 429, resposta inválida): o serviço continua com a última configuração
  conhecida e tenta de novo no próximo intervalo; o log avisa na 1ª falha e a cada 10. **401** na configuração segue
  a mesma regra do envio de quadros: envios pausados por 5 min, nada é apagado e nenhuma câmera é desligada; confira o
  `Segredo` e se o agente não foi revogado.
- **Reinício sem internet:** a última lista recebida fica num cache local, `cameras-servidor.dat` na pasta de dados,
  **protegido com DPAPI** (a senha das câmeras nunca fica em texto), e é usada até a primeira consulta bem-sucedida.
  Ele só vale para este `AgenteId` e este PC. Apagar o arquivo é seguro (é refeito na próxima consulta).
- A **senha** que vem do servidor só existe em memória e nesse cache protegido; nunca vai para log, motivo de teste ou
  mensagem de erro (passa pelo mascarador).
- **Câmeras do arquivo local** (`AgenteCameras:Cameras`, formato antigo: `CameraId`, `Nome`, `Host`, `PortaRtsp`,
  `Usuario`, `Senha`, `Canal`, `Stream`, `ModeloUrl`, `IntervaloEnvioSegundos`, `Habilitada`) continuam valendo, juntas
  com as do servidor. Se a mesma `CameraId` vier do servidor, **o servidor manda**; se ela sair do servidor, volta a
  valer a do arquivo.
- **Desligar a leitura do servidor:** `AgenteCameras:LerConfiguracaoDoServidor` = `false` (padrão `true`). Com ela
  desligada o agente não consulta a configuração (e, portanto, não executa testes de conexão nem dá sinal de vida
  pela consulta) e usa só as câmeras do arquivo, como antes.

### Testar conexão

Quando alguém clica em "Testar conexão" na tela, o pedido chega na próxima consulta (até 1 min). O agente abre a
câmera pelo mesmo caminho da leitura normal (ffmpeg, URL RTSP e `-timeout` de sempre), captura **um** quadro, gera uma
miniatura JPEG (até 150 KB) e devolve o resultado ao servidor. O teste usa uma conexão RTSP **própria**: não interrompe
a leitura normal da mesma câmera (algumas câmeras limitam as conexões simultâneas; veja "Problemas conhecidos").

- Tempo limite de captura: 25 s; o servidor expira o pedido em 2 min. No máximo 2 testes ao mesmo tempo. Cada pedido
  é executado uma única vez, mesmo que o servidor o entregue de novo.
- Em falha, a tela mostra o motivo em português simples: "Senha recusada pela câmera", "Câmera não encontrada na
  rede", "A câmera recusou a conexão (confira a porta RTSP)", "A câmera não respondeu a tempo", "A câmera não tem
  esse canal ou stream", "O agente não encontrou o ffmpeg neste computador", entre outros. Motivo e detalhe nunca
  trazem senha nem URL.
- O resultado do teste fica no log (sem a senha): `Teste de conexão <id>: ...`.

## Onde ver o que está acontecendo

- Logs em arquivo (rotação diária): `C:\ProgramData\AAHBRANT\AgenteCameras\logs\agente-cameras-AAAAMMDD.log`
  (uma linha por envio, com câmera, status HTTP e resumo). Senhas, segredo e URL RTSP são mascarados.
- Event Viewer > Windows Logs > Application, fonte `AAHBRANT.SST.AgenteCameras` (avisos e erros).
- Sem internet, os quadros com movimento ficam numa fila em disco (cifrada) e seguem quando a API volta. Depois da
  queda, o primeiro quadro AO VIVO testa a API; se passar, a fila drena intercalada com os quadros novos. Um quadro da
  fila que falha 5 vezes seguidas com erro 5xx enquanto os envios ao vivo passam é descartado (aviso no log).
- **Limpeza da fila:** na partida e a cada 10 min o serviço apaga, em todas as pastas de câmera (mesmo de câmeras que
  saíram da configuração), quadros com mais de `FilaIdadeMaximaHoras` (24 h), arquivos `.tmp` órfãos (mais de 5 min)
  e o excedente de `FilaTamanhoMaximoMb`. Roda também sem câmeras ativas e com os envios pausados.
- **Respostas da API:** 401 pausa todos os envios por 5 min. 404 só significa "câmera não pertence a este agente"
  quando o corpo traz `"codigo":"camera_nao_encontrada"`: a câmera fica **pausada** e tenta de novo a cada 15 min
  (sem apagar a fila e sem encerrar). Qualquer outro 404, 409 e 5xx são tratados como API indisponível (quadro vai
  para a fila, com espera crescente); 400 e 413 descartam o quadro.
- Sem nenhuma câmera ativa o serviço fica em espera e continua limpando a fila. Com `LerConfiguracaoDoServidor`
  ligada ele segue consultando o servidor (a câmera cadastrada depois começa sozinha); desligada, repete um aviso
  a cada hora.
- Câmera removida do cadastro: os quadros dela que já estavam na fila offline seguem a regra de expiração (24 h).
- **Permissão da pasta de dados:** o `instalar.ps1` restringe `C:\ProgramData\AAHBRANT\AgenteCameras` a SYSTEM,
  Administradores e LocalService. Se você mudar `PastaDados`, aplique o mesmo na nova pasta (como administrador):
  `icacls "<pasta>" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F *S-1-5-19:(OI)(CI)M`.
  Ao iniciar, o serviço confere a pasta configurada e, se ela estiver aberta a Usuários/Todos, registra um aviso no
  log com esse comando (o serviço não altera permissões sozinho).

## Rodar em console (desenvolvimento)

Com o serviço parado, sem instalar nada:

```powershell
$env:AAHBRANT_AGENTECAMERAS_CONFIG = 'C:\caminho\appsettings.local.json'   # opcional; padrão: ProgramData
dotnet run --project src\AAHBRANT.SST.AgenteCameras
```

Também dá para sobrescrever campos por variável de ambiente ou argumento, por exemplo
`--AgenteCameras:CaminhoFfmpeg=C:\ffmpeg\bin\ffmpeg.exe`.

Teste local **só com o arquivo** (sem consultar o servidor; `LerConfiguracaoDoServidor=false`), com a câmera do piloto
(192.168.0.8, canal 1, stream 1, usuário admin; a senha definida na câmera):

```powershell
dotnet run --project src\AAHBRANT.SST.AgenteCameras -- `
  --AgenteCameras:ApiBaseUrl=https://URL-DA-API `
  --AgenteCameras:AgenteId=ID-DO-AGENTE `
  --AgenteCameras:Segredo=SEGREDO-DO-AGENTE `
  --AgenteCameras:CaminhoFfmpeg=C:\ffmpeg\bin\ffmpeg.exe `
  --AgenteCameras:PastaDados=C:\Temp\agente-cameras `
  --AgenteCameras:LerConfiguracaoDoServidor=false `
  --AgenteCameras:Cameras:0:CameraId=ID-DA-CAMERA `
  --AgenteCameras:Cameras:0:Nome=Piloto `
  --AgenteCameras:Cameras:0:Host=192.168.0.8 `
  --AgenteCameras:Cameras:0:Usuario=admin `
  --AgenteCameras:Cameras:0:Senha=SENHA-DA-CAMERA `
  --AgenteCameras:Cameras:0:Canal=1 `
  --AgenteCameras:Cameras:0:Stream=1
```

Atenção: a URL RTSP (com a senha) vai na linha de comando do `ffmpeg.exe`, visível a quem lista processos na
máquina. Use uma conta de câmera só de leitura e uma máquina de acesso restrito.

## Problemas conhecidos

- O teste de conexão abre uma segunda conexão RTSP na câmera enquanto a leitura normal segue aberta. Câmeras de entrada
  que aceitam poucas conexões simultâneas podem recusar o teste ("A câmera recusou a conexão") ou, mais raramente,
  derrubar a leitura normal por alguns segundos (ela se religa sozinha). Validar na câmera real.
- O pedido de teste só chega na consulta seguinte (até 15 s) e expira no servidor em 2 min: sobra pouca folga se a
  câmera demorar a responder (a captura desiste em 25 s).

## Desinstalar

```powershell
.\scripts\agente-cameras\desinstalar.ps1               # mantém configuração, logs e fila
.\scripts\agente-cameras\desinstalar.ps1 -ApagarDados  # remove tudo (a fila offline é perdida)
```
