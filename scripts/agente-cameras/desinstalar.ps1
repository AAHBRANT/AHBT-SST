<#
.SYNOPSIS
  Remove o serviço do Agente de Câmeras do SST-APP.

.DESCRIPTION
  Para e remove o serviço "AAHBRANT.SST.AgenteCameras" e apaga os arquivos do programa.
  Por padrão MANTÉM a pasta de dados (C:\ProgramData\AAHBRANT\AgenteCameras: configuração, logs e fila de quadros).
  Use -ApagarDados para removê-la também (a fila offline, com quadros ainda não enviados, será perdida).

  Execute em um PowerShell COMO ADMINISTRADOR.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$Destino = 'C:\Program Files\AAHBRANT\AgenteCameras',
    [switch]$ApagarDados
)

$ErrorActionPreference = 'Stop'
$NomeServico = 'AAHBRANT.SST.AgenteCameras'
$PastaDados = Join-Path $env:ProgramData 'AAHBRANT\AgenteCameras'

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este script em um PowerShell como Administrador.'
}

$servico = Get-Service -Name $NomeServico -ErrorAction SilentlyContinue
if ($servico) {
    if ($servico.Status -ne 'Stopped') {
        Stop-Service -Name $NomeServico -Force
        $servico.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete $NomeServico | Out-Null
    Write-Host 'Serviço removido.'
} else {
    Write-Host 'Serviço não estava instalado.'
}

# Garante que não sobrou ffmpeg do agente rodando (o serviço já encerra os filhos ao parar).
Get-Process -Name ffmpeg -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -and $_.Path.StartsWith($Destino, [StringComparison]::OrdinalIgnoreCase) } |
    Stop-Process -Force

if (Test-Path $Destino) {
    Remove-Item -Recurse -Force $Destino
    Write-Host "Arquivos removidos: $Destino"
}

if ($ApagarDados -and (Test-Path $PastaDados)) {
    Remove-Item -Recurse -Force $PastaDados
    Write-Host "Dados removidos: $PastaDados"
} elseif (Test-Path $PastaDados) {
    Write-Host "Dados mantidos em $PastaDados (use -ApagarDados para remover)."
}
