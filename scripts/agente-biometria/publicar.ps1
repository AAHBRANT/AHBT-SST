<#
.SYNOPSIS
  Gera o Setup do Agente de Biometria (um .exe so) e o manifesto assinado da atualizacao automatica.
.DESCRIPTION
  Saida em dist\agente-biometria:
    Setup-AgenteBiometria-<versao>.exe  instala num posto novo (duplo clique) ou atualiza um existente
    manifesto.json                      o que os agentes ja instalados consultam para se atualizar
  Para publicar uma versao: suba os DOIS arquivos para o endereco de -UrlBase (HTTPS). Em ate algumas horas
  cada posto baixa, confere a assinatura e se atualiza sozinho, sempre com o quiosque parado.

  O leitor Futronic so funciona com a ftrScanAPI.dll de 32 bits, por isso o agente e publicado em win-x86.
  A DLL nao e versionada no git: copie-a do pacote oficial da Futronic para
  src\AAHBRANT.SST.AgenteBiometria\Native\ (ou informe -SdkDll).

  Primeira vez: rode com -GerarChave. A chave PRIVADA fica so com quem publica (nunca no git, nunca nos
  postos; guarde num cofre). Se ela for perdida, os postos param de aceitar atualizacoes e precisam receber
  o Setup novo manualmente.
.EXAMPLE
  .\scripts\agente-biometria\publicar.ps1 -GerarChave -ChavePrivada C:\Cofre\agente-atualizacao.chave-privada.pem
.EXAMPLE
  .\scripts\agente-biometria\publicar.ps1 -Versao 1.1.0 -UrlBase https://<armazenamento>/agente-biometria -ChavePrivada C:\Cofre\agente-atualizacao.chave-privada.pem
#>
param(
  [string]$Versao,
  [string]$UrlBase,
  [string]$ChavePrivada,
  [switch]$GerarChave,
  [string]$Saida = "dist\agente-biometria",
  [string]$SdkDll
)
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$projetoAgente = Join-Path $raiz 'src\AAHBRANT.SST.AgenteBiometria\AAHBRANT.SST.AgenteBiometria.csproj'
$projetoSetup = Join-Path $raiz 'src\AAHBRANT.SST.AgenteBiometria.Instalador\AAHBRANT.SST.AgenteBiometria.Instalador.csproj'
$chavePublica = Join-Path $raiz 'src\AAHBRANT.SST.AgenteBiometria\Atualizacao\chave-publica-atualizacao.pem'
$destino = if ([IO.Path]::IsPathRooted($Saida)) { $Saida } else { Join-Path $raiz $Saida }

function Publicar-Setup([string]$pasta, [string[]]$extras) {
  dotnet publish $projetoSetup -c Release -r win-x86 --self-contained -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o $pasta @extras | Out-Host
  if ($LASTEXITCODE -ne 0) { throw 'dotnet publish do Setup falhou.' }
  return Join-Path $pasta 'Setup-AgenteBiometria.exe'
}

if ($GerarChave) {
  if (-not $ChavePrivada) { throw 'Informe -ChavePrivada (onde gravar a chave privada, FORA do repositorio).' }
  if ([IO.Path]::GetFullPath($ChavePrivada).StartsWith([string]$raiz, [StringComparison]::OrdinalIgnoreCase)) { throw 'A chave privada nao pode ficar dentro do repositorio.' }
  $exe = Publicar-Setup (Join-Path $env:TEMP 'aahbrant-setup-chave') @()
  $p = Start-Process $exe -ArgumentList "--gerar-chave `"$ChavePrivada`" `"$chavePublica`"" -Wait -PassThru
  if ($p.ExitCode -ne 0) { throw 'Falha ao gerar a chave (ja existe uma? veja %LOCALAPPDATA%\AAHBRANT\instalador-agente.log).' }
  Write-Host "Chave privada: $ChavePrivada (guarde num cofre)" -ForegroundColor Yellow
  Write-Host "Chave publica: $chavePublica (commite este arquivo)" -ForegroundColor Green
  return
}

if (-not $Versao) { throw 'Informe -Versao (ex.: 1.1.0). Tem que ser maior que a dos postos para eles atualizarem.' }
if (-not $UrlBase -or -not $UrlBase.StartsWith('https://')) { throw 'Informe -UrlBase com https:// (onde Setup e manifesto vao ficar).' }
$UrlBase = $UrlBase.TrimEnd('/')
if (-not (Test-Path $chavePublica)) { Write-Warning 'Sem chave publica: este agente sai com a atualizacao automatica DESLIGADA. Rode -GerarChave antes.' }
if (-not $SdkDll) { $SdkDll = Join-Path $raiz 'src\AAHBRANT.SST.AgenteBiometria\Native\ftrScanAPI.dll' }
if (-not (Test-Path $SdkDll)) { throw "ftrScanAPI.dll nao encontrada em '$SdkDll'. Copie-a do pacote oficial da Futronic (ftrScanApiEx_v4.5.zip) ou informe -SdkDll." }

if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }
$pacote = Join-Path $destino 'pacote'

# 1) Agente: pasta com o .NET embutido, a DLL do leitor e o diagnostico.
dotnet publish $projetoAgente -c Release -r win-x86 --self-contained "-p:Version=$Versao" -o $pacote
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish do agente falhou.' }
Copy-Item $SdkDll $pacote -Force
Copy-Item (Join-Path $PSScriptRoot 'diagnostico.ps1') $pacote -Force
$zip = Join-Path $destino 'pacote-agente.zip'
Compress-Archive -Path (Join-Path $pacote '*') -DestinationPath $zip

# 2) Setup com o pacote embutido.
$exe = Publicar-Setup (Join-Path $destino 'setup') @("-p:Version=$Versao", "-p:PacoteAgente=$zip", "-p:UrlManifestoAtualizacao=$UrlBase/manifesto.json")
$nomeSetup = "Setup-AgenteBiometria-$Versao.exe"
$setupFinal = Join-Path $destino $nomeSetup
Move-Item $exe $setupFinal -Force

# 3) Manifesto assinado.
if ($ChavePrivada) {
  $manifesto = Join-Path $destino 'manifesto.json'
  $p = Start-Process $setupFinal -ArgumentList "--assinar `"$ChavePrivada`" $Versao `"$UrlBase/$nomeSetup`" `"$setupFinal`" `"$manifesto`"" -Wait -PassThru
  if ($p.ExitCode -ne 0) { throw 'Falha ao assinar o manifesto (veja %LOCALAPPDATA%\AAHBRANT\instalador-agente.log).' }
  Write-Host "Suba para $UrlBase : $nomeSetup e manifesto.json" -ForegroundColor Green
} else {
  Write-Warning 'Sem -ChavePrivada: Setup gerado, mas sem manifesto. Serve para instalar a mao, nao atualiza os postos.'
}
Write-Host "Setup pronto: $setupFinal" -ForegroundColor Green
