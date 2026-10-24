<#
.SYNOPSIS
  Diagnostico do Agente de Biometria neste PC: driver, agente, configuracao, CORS, rede privada e API.
.DESCRIPTION
  Roda so leituras (nao altera nada) e imprime um relatorio APROVADO/FALHOU/ATENCAO com a causa provavel de cada
  falha. Nunca mostra o segredo nem a chave de criptografia. Mande o relatorio inteiro para quem estiver
  acompanhando o piloto.
  Com -Capturar, pede uma leitura real do leitor (apoie o dedo quando o script avisar).
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\diagnostico.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\diagnostico.ps1 -Capturar
#>
param(
  [string]$Destino = (Join-Path $env:LOCALAPPDATA 'AAHBRANT\AgenteBiometria'),
  [switch]$Capturar
)
$ErrorActionPreference = 'Continue'
$nomeExe = 'AAHBRANT.SST.AgenteBiometria'
$porta = 5251
$resultados = New-Object System.Collections.Generic.List[object]

function Registrar($teste, $situacao, $detalhe, $dica) {
  $resultados.Add([pscustomobject]@{ Teste = $teste; Situacao = $situacao; Detalhe = $detalhe; Dica = $dica })
}

function Chamar($metodo, $url, $cabecalhos) {
  # Devolve status + cabecalhos mesmo quando a resposta e um erro HTTP (PowerShell 5.1 lanca excecao).
  try {
    $r = Invoke-WebRequest -Uri $url -Method $metodo -Headers $cabecalhos -UseBasicParsing -TimeoutSec 20
    return [pscustomobject]@{ Status = [int]$r.StatusCode; Cabecalhos = $r.Headers; Corpo = $r.Content; Erro = $null }
  } catch {
    $resp = $_.Exception.Response
    if ($resp) {
      $corpo = ''
      try { $corpo = (New-Object IO.StreamReader($resp.GetResponseStream())).ReadToEnd() } catch {}
      $cab = @{}
      foreach ($k in $resp.Headers.AllKeys) { $cab[$k] = $resp.Headers[$k] }
      return [pscustomobject]@{ Status = [int]$resp.StatusCode; Cabecalhos = $cab; Corpo = $corpo; Erro = $null }
    }
    return [pscustomobject]@{ Status = 0; Cabecalhos = @{}; Corpo = ''; Erro = $_.Exception.Message }
  }
}

# 1. Sistema
$so = (Get-CimInstance Win32_OperatingSystem -ErrorAction SilentlyContinue)
Registrar 'Sistema' 'INFO' ("{0} ({1})" -f $so.Caption, $so.OSArchitecture) ''

# 2. Leitor (driver)
$leitor = Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match 'VID_1491' }
if (-not $leitor) {
  Registrar 'Leitor Futronic (USB)' 'FALHOU' 'Nenhum dispositivo VID_1491 encontrado.' 'Conecte o leitor na USB e instale o driver Futronic (versao 10.0.0.1, Windows 8/10/11). Teste outra porta USB.'
} else {
  $ok = @($leitor | Where-Object { $_.Status -ne 'OK' }).Count -eq 0
  $nome = ($leitor | Select-Object -First 1).FriendlyName
  if ($ok) { Registrar 'Leitor Futronic (USB)' 'APROVADO' $nome '' }
  else { Registrar 'Leitor Futronic (USB)' 'FALHOU' "$nome com status diferente de OK." 'Abra o Gerenciador de Dispositivos: se houver alerta amarelo, reinstale o driver.' }
}

# 3. Instalacao e configuracao
$exe = Join-Path $Destino "$nomeExe.exe"
$dll = Join-Path $Destino 'ftrScanAPI.dll'
$cfgArq = Join-Path $Destino 'appsettings.Production.json'
if (Test-Path $exe) { Registrar 'Agente instalado' 'APROVADO' $Destino '' }
else { Registrar 'Agente instalado' 'FALHOU' "Nao encontrado em $Destino." 'Rode o instalar.ps1 do pacote (ou informe -Destino se instalou em outra pasta).' }
if (Test-Path $dll) { Registrar 'SDK do leitor (ftrScanAPI.dll)' 'APROVADO' '' '' }
else { Registrar 'SDK do leitor (ftrScanAPI.dll)' 'FALHOU' 'DLL ausente ao lado do agente.' 'Use o pacote gerado por publicar.ps1 (ele inclui a DLL).' }

$cfg = $null
if (Test-Path $cfgArq) {
  try { $cfg = (Get-Content $cfgArq -Raw | ConvertFrom-Json).Agente } catch { $cfg = $null }
}
if ($null -eq $cfg) {
  Registrar 'Configuracao do agente' 'FALHOU' 'appsettings.Production.json ausente ou invalido.' 'Rode o instalar.ps1 de novo com os dados do leitor.'
} else {
  $faltando = @()
  if (-not $cfg.DispositivoId -or $cfg.DispositivoId -eq '00000000-0000-0000-0000-000000000000') { $faltando += 'DispositivoId' }
  if (-not $cfg.SegredoDispositivo) { $faltando += 'SegredoDispositivo' }
  if (-not $cfg.ChaveCriptografiaBiometriaBase64) { $faltando += 'ChaveCriptografiaBiometriaBase64' }
  if (-not $cfg.BackendBaseUrl) { $faltando += 'BackendBaseUrl' }
  if (-not $cfg.OrigemPermitida) { $faltando += 'OrigemPermitida' }
  if ($cfg.Leitor -ne 'Futronic') { $faltando += 'Leitor (deveria ser Futronic)' }
  if ($faltando.Count -eq 0) {
    Registrar 'Configuracao do agente' 'APROVADO' ("Leitor={0}; Api={1}; Origem={2}; DetectarDedoFalso={3}; TempoDedo={4}s" -f $cfg.Leitor, $cfg.BackendBaseUrl, $cfg.OrigemPermitida, [bool]$cfg.DetectarDedoFalso, $(if ($cfg.TempoLimiteDedoSegundos) { $cfg.TempoLimiteDedoSegundos } else { 15 })) ''
  } else {
    Registrar 'Configuracao do agente' 'FALHOU' ('Campos vazios/invalidos: ' + ($faltando -join ', ')) 'Rode o instalar.ps1 de novo informando os dados do leitor.'
  }
  if ($cfg.OrigemPermitida -and $cfg.OrigemPermitida -match '/$') {
    Registrar 'Origem permitida' 'ATENCAO' 'Termina com "/".' 'O agente ignora a barra final, mas mantenha exatamente a URL em que o app abre.'
  }
  if ($cfg.OrigemPermitida -and $cfg.OrigemPermitida -notmatch '^https://' -and $cfg.OrigemPermitida -notmatch '^http://localhost') {
    Registrar 'Origem permitida' 'ATENCAO' $cfg.OrigemPermitida 'Em hml/producao o app abre em HTTPS; confira se a origem e a URL exata do app.'
  }
}

# 4. Processo e porta
$proc = Get-Process $nomeExe -ErrorAction SilentlyContinue
if ($proc) { Registrar 'Agente em execucao' 'APROVADO' ("PID {0}" -f ($proc | Select-Object -First 1).Id) '' }
else {
  Registrar 'Agente em execucao' 'FALHOU' 'Processo nao encontrado.' 'Inicie o agente (dois cliques em AAHBRANT.SST.AgenteBiometria.exe) ou reinicie o Windows; confira o inicio automatico abaixo.'
}
$escuta = Get-NetTCPConnection -State Listen -LocalPort $porta -ErrorAction SilentlyContinue
if ($escuta) { Registrar "Porta $porta (somente este PC)" 'APROVADO' ($escuta | Select-Object -First 1).LocalAddress '' }
else { Registrar "Porta $porta" 'FALHOU' 'Ninguem escutando.' 'O agente nao esta rodando ou outra aplicacao usa a porta.' }

$run = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'AAHBRANT Agente Biometria' -ErrorAction SilentlyContinue
if ($run) { Registrar 'Inicio automatico com o Windows' 'APROVADO' '' '' }
else { Registrar 'Inicio automatico com o Windows' 'ATENCAO' 'Nao configurado.' 'Rode o instalar.ps1 sem -SemAutoStart para o agente voltar sozinho apos reiniciar.' }

# 5. Respostas do agente (CORS e rede privada)
$origem = if ($cfg -and $cfg.OrigemPermitida) { $cfg.OrigemPermitida.TrimEnd('/') } else { 'https://origem-nao-configurada.invalid' }
if ($escuta) {
  $r = Chamar 'GET' "http://127.0.0.1:$porta/api/dispositivo" @{ Origin = $origem }
  if ($r.Status -eq 200) {
    $acao = $r.Cabecalhos['Access-Control-Allow-Origin']
    Registrar 'Agente responde ao app' 'APROVADO' 'GET /api/dispositivo = 200' ''
    if ($acao -eq $origem) { Registrar 'CORS da origem do app' 'APROVADO' $acao '' }
    else { Registrar 'CORS da origem do app' 'FALHOU' "Access-Control-Allow-Origin = '$acao' (esperado '$origem')." 'A OrigemPermitida do agente nao e exatamente a URL em que o app abre. Reinstale com a origem correta.' }
  } else {
    Registrar 'Agente responde ao app' 'FALHOU' ("Status {0} {1}" -f $r.Status, $r.Erro) 'O agente esta rodando mas nao respondeu; veja o log na janela/console do agente.'
  }

  $pre = Chamar 'OPTIONS' "http://127.0.0.1:$porta/api/capturar" @{
    Origin = $origem
    'Access-Control-Request-Method' = 'POST'
    'Access-Control-Request-Private-Network' = 'true'
  }
  $redePrivada = $pre.Cabecalhos['Access-Control-Allow-Private-Network']
  if ($redePrivada -eq 'true') { Registrar 'Liberacao de rede privada (Chrome/Edge/Teams)' 'APROVADO' 'Access-Control-Allow-Private-Network: true' '' }
  else { Registrar 'Liberacao de rede privada (Chrome/Edge/Teams)' 'FALHOU' "Preflight status $($pre.Status); cabecalho = '$redePrivada'." 'Sem esse cabecalho o navegador pode bloquear o site HTTPS de falar com o agente. Confira a OrigemPermitida e a versao do agente.' }
}

# 6. API (backend)
if ($cfg -and $cfg.BackendBaseUrl) {
  $api = Chamar 'GET' ($cfg.BackendBaseUrl.TrimEnd('/') + '/') @{}
  if ($api.Status -gt 0) { Registrar 'Alcance da API' 'APROVADO' ("{0} respondeu (HTTP {1})" -f $cfg.BackendBaseUrl, $api.Status) '' }
  else { Registrar 'Alcance da API' 'FALHOU' $api.Erro 'Este PC nao alcanca a API: confira internet, proxy/firewall e a BackendBaseUrl.' }
}

# 7. Leitura real (opcional)
if ($Capturar) {
  if (-not $escuta) { Registrar 'Leitura real do leitor' 'FALHOU' 'Agente nao esta escutando.' 'Suba o agente antes.' }
  else {
    [console]::beep(1000, 400)
    Write-Host "`n>>> APOIE O DEDO NO LEITOR AGORA (ate 40 s)..." -ForegroundColor Yellow
    $cap = Chamar 'POST' "http://127.0.0.1:$porta/api/capturar-bruto" @{ Origin = $origem }
    if ($cap.Status -eq 200) { Registrar 'Leitura real do leitor' 'APROVADO' ("Template gerado ({0} caracteres)." -f $cap.Corpo.Length) '' }
    else {
      $msg = try { ($cap.Corpo | ConvertFrom-Json).erro } catch { $cap.Corpo }
      Registrar 'Leitura real do leitor' 'FALHOU' ("HTTP {0}: {1}" -f $cap.Status, $msg) 'Limpe o vidro e o dedo, apoie o dedo com firmeza; se persistir, veja o driver e o SDK acima.'
    }
  }
}

# Relatorio
Write-Host ''
Write-Host '=== Diagnostico do Agente de Biometria ===' -ForegroundColor Cyan
Write-Host ("PC: {0} | usuario: {1} | {2}" -f $env:COMPUTERNAME, $env:USERNAME, (Get-Date -Format 'dd/MM/yyyy HH:mm'))
foreach ($x in $resultados) {
  $cor = switch ($x.Situacao) { 'APROVADO' { 'Green' } 'FALHOU' { 'Red' } 'ATENCAO' { 'Yellow' } default { 'Gray' } }
  Write-Host ("[{0,-8}] {1}" -f $x.Situacao, $x.Teste) -ForegroundColor $cor -NoNewline
  if ($x.Detalhe) { Write-Host ("  - {0}" -f $x.Detalhe) } else { Write-Host '' }
  if ($x.Situacao -in 'FALHOU', 'ATENCAO' -and $x.Dica) { Write-Host ("           > {0}" -f $x.Dica) -ForegroundColor DarkYellow }
}
$falhas = @($resultados | Where-Object { $_.Situacao -eq 'FALHOU' }).Count
$atencoes = @($resultados | Where-Object { $_.Situacao -eq 'ATENCAO' }).Count
Write-Host ''
if ($falhas -eq 0) { Write-Host ("Resultado: SEM FALHAS ({0} atencao(oes))." -f $atencoes) -ForegroundColor Green }
else { Write-Host ("Resultado: {0} FALHA(S), {1} atencao(oes)." -f $falhas, $atencoes) -ForegroundColor Red }
exit ([int]($falhas -gt 0))
