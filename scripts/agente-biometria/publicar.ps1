<#
.SYNOPSIS
  Gera o pacote do Agente de Biometria (32 bits, .NET embutido) pronto para instalar nos PCs das obras.
.DESCRIPTION
  O leitor Futronic so funciona com a ftrScanAPI.dll de 32 bits, por isso o agente e publicado em win-x86.
  A DLL nao e versionada no git: copie-a do pacote oficial da Futronic para
  src\AAHBRANT.SST.AgenteBiometria\Native\ (ou informe -SdkDll).
.EXAMPLE
  .\scripts\agente-biometria\publicar.ps1
#>
param(
  [string]$Saida = "dist\agente-biometria",
  [string]$SdkDll
)
$ErrorActionPreference = 'Stop'
$raiz = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$projeto = Join-Path $raiz 'src\AAHBRANT.SST.AgenteBiometria\AAHBRANT.SST.AgenteBiometria.csproj'
if (-not $SdkDll) { $SdkDll = Join-Path $raiz 'src\AAHBRANT.SST.AgenteBiometria\Native\ftrScanAPI.dll' }
if (-not (Test-Path $SdkDll)) { throw "ftrScanAPI.dll nao encontrada em '$SdkDll'. Copie-a do pacote oficial da Futronic (ftrScanApiEx_v4.5.zip) ou informe -SdkDll." }

$destino = if ([IO.Path]::IsPathRooted($Saida)) { $Saida } else { Join-Path $raiz $Saida }
if (Test-Path $destino) { Remove-Item $destino -Recurse -Force }

dotnet publish $projeto -c Release -r win-x86 --self-contained -o $destino
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish falhou.' }

Copy-Item $SdkDll $destino -Force
Copy-Item (Join-Path $PSScriptRoot 'instalar.ps1') $destino -Force

$zip = "$destino.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $destino '*') -DestinationPath $zip
Write-Host "Pacote pronto: $zip" -ForegroundColor Green
