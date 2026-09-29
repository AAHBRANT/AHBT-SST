<#
.SYNOPSIS
  Instala o Agente de Biometria AAHBRANT neste PC (por usuario, sem precisar de administrador).
.DESCRIPTION
  Copia o agente para %LOCALAPPDATA%\AAHBRANT\AgenteBiometria, grava a configuracao, faz o agente
  iniciar junto com o Windows e o inicia agora. Pre-requisito: driver USB da Futronic instalado.
  Rode dentro da pasta do pacote gerado por publicar.ps1.
.PARAMETER Desinstalar
  Para o agente, remove o inicio automatico e apaga a pasta instalada.
#>
param(
  [string]$Destino = (Join-Path $env:LOCALAPPDATA 'AAHBRANT\AgenteBiometria'),
  [string]$DispositivoId,
  [string]$SegredoDispositivo,
  [string]$ChaveCriptografiaBiometriaBase64,
  [string]$BackendBaseUrl,
  [string]$OrigemPermitida,
  [switch]$SemAutoStart,
  [switch]$SemIniciar,
  [switch]$Desinstalar
)
$ErrorActionPreference = 'Stop'
$nomeExe = 'AAHBRANT.SST.AgenteBiometria'
$chaveRun = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'

function Parar-Agente { Get-Process $nomeExe -ErrorAction SilentlyContinue | Stop-Process -Force; Start-Sleep -Milliseconds 500 }

if ($Desinstalar) {
  Parar-Agente
  Remove-ItemProperty -Path $chaveRun -Name 'AAHBRANT Agente Biometria' -ErrorAction SilentlyContinue
  if (Test-Path $Destino) { Remove-Item $Destino -Recurse -Force }
  Write-Host 'Agente removido.' -ForegroundColor Green
  return
}

$origem = $PSScriptRoot
foreach ($arq in "$nomeExe.exe", 'ftrScanAPI.dll') {
  if (-not (Test-Path (Join-Path $origem $arq))) { throw "Arquivo '$arq' nao encontrado ao lado do instalador. Use o pacote gerado por publicar.ps1." }
}

if (-not (Get-PnpDevice -PresentOnly -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match 'VID_1491' })) {
  Write-Warning 'Leitor Futronic nao detectado. Conecte o leitor e instale o driver USB da Futronic (versao 10.0.0.1 para Windows 10/11). Continuando a instalacao.'
}

function Perguntar($valor, $rotulo, [switch]$Secreto) {
  if ($valor) { return $valor }
  if ($Secreto) {
    $s = Read-Host $rotulo -AsSecureString
    return [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($s))
  }
  return Read-Host $rotulo
}
$DispositivoId = Perguntar $DispositivoId 'Id do dispositivo (gerado ao registrar o agente na obra)'
$SegredoDispositivo = Perguntar $SegredoDispositivo 'Segredo do dispositivo' -Secreto
$ChaveCriptografiaBiometriaBase64 = Perguntar $ChaveCriptografiaBiometriaBase64 'Chave de criptografia de biometria (base64)' -Secreto
$BackendBaseUrl = Perguntar $BackendBaseUrl 'URL da API (ex.: https://sst-api-hml...)'
$OrigemPermitida = Perguntar $OrigemPermitida 'URL do app web que abre o quiosque (origem permitida no CORS)'

Parar-Agente
New-Item -ItemType Directory -Path $Destino -Force | Out-Null
Copy-Item (Join-Path $origem '*') $Destino -Recurse -Force -Exclude 'instalar.ps1'

$config = [ordered]@{
  Agente = [ordered]@{
    Leitor = 'Futronic'
    InverterImagem = $true
    DispositivoId = $DispositivoId
    SegredoDispositivo = $SegredoDispositivo
    ChaveCriptografiaBiometriaBase64 = $ChaveCriptografiaBiometriaBase64
    BackendBaseUrl = $BackendBaseUrl
    OrigemPermitida = $OrigemPermitida
  }
}
$arquivoConfig = Join-Path $Destino 'appsettings.Production.json'
$config | ConvertTo-Json -Depth 4 | Set-Content -Path $arquivoConfig -Encoding UTF8
# Segredos em disco: so o usuario atual pode ler.
icacls $arquivoConfig /inheritance:r /grant:r "$($env:USERNAME):(R,W)" | Out-Null

$exe = Join-Path $Destino "$nomeExe.exe"
if (-not $SemAutoStart) {
  Set-ItemProperty -Path $chaveRun -Name 'AAHBRANT Agente Biometria' -Value "`"$exe`""
}
if (-not $SemIniciar) {
  Start-Process -FilePath $exe -WorkingDirectory $Destino
  Start-Sleep -Seconds 4
  try {
    Invoke-RestMethod -Uri 'http://127.0.0.1:5251/api/dispositivo' -Headers @{ Origin = $OrigemPermitida } | Out-Null
    Write-Host 'Agente instalado e respondendo em http://127.0.0.1:5251.' -ForegroundColor Green
  } catch {
    Write-Warning "Agente iniciado, mas nao respondeu ainda: $($_.Exception.Message)"
  }
} else {
  Write-Host "Agente instalado em $Destino (nao iniciado)." -ForegroundColor Green
}
