<#
.SYNOPSIS
  Instala (ou atualiza) o Agente de Câmeras do SST-APP como Serviço do Windows.

.DESCRIPTION
  Publica o projeto self-contained (win-x64), copia para "C:\Program Files\AAHBRANT\AgenteCameras", cria o serviço
  "AAHBRANT.SST.AgenteCameras" com início automático, recuperação em caso de falha e conta local (LocalService),
  e prepara a pasta de dados/configuração em C:\ProgramData\AAHBRANT\AgenteCameras.

  NÃO baixa nem instala o ffmpeg: coloque o ffmpeg.exe (build LGPL) manualmente em
  <Destino>\ffmpeg\ffmpeg.exe (ou ajuste AgenteCameras:CaminhoFfmpeg). Ver README.md.

  Execute em um PowerShell COMO ADMINISTRADOR.

.PARAMETER Destino
  Pasta de instalação. Padrão: C:\Program Files\AAHBRANT\AgenteCameras

.PARAMETER Iniciar
  Inicia o serviço ao final. Sem este parâmetro o serviço fica instalado e parado: preencha antes o arquivo
  C:\ProgramData\AAHBRANT\AgenteCameras\appsettings.local.json e coloque o ffmpeg.

.PARAMETER PastaPublicada
  Pasta com o agente já publicado (dotnet publish -c Release -r win-x64 --self-contained true). Use quando a conta
  de administrador não consegue compilar (sem acesso aos pacotes NuGet): publique com a conta comum e instale com
  este parâmetro. A pasta informada não é apagada.

.EXAMPLE
  .\instalar.ps1
  .\instalar.ps1 -Iniciar
#>
[CmdletBinding()]
param(
    [string]$Destino = 'C:\Program Files\AAHBRANT\AgenteCameras',
    [switch]$Iniciar,
    [string]$PastaPublicada
)

$ErrorActionPreference = 'Stop'
$NomeServico = 'AAHBRANT.SST.AgenteCameras'
$PastaDados = Join-Path $env:ProgramData 'AAHBRANT\AgenteCameras'
$RaizRepo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$Projeto = Join-Path $RaizRepo 'src\AAHBRANT.SST.AgenteCameras\AAHBRANT.SST.AgenteCameras.csproj'

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Execute este script em um PowerShell como Administrador.'
}
if ($PastaPublicada) {
    if (-not (Test-Path (Join-Path $PastaPublicada 'AAHBRANT.SST.AgenteCameras.exe'))) {
        throw "A pasta publicada '$PastaPublicada' não contém AAHBRANT.SST.AgenteCameras.exe."
    }
    Write-Host "1/6 Usando a publicação pronta em $PastaPublicada ..."
    $publicacao = $PastaPublicada
} else {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'SDK do .NET 8 não encontrado (comando "dotnet"). Instale-o para publicar o agente.'
    }
    Write-Host '1/6 Publicando (self-contained, win-x64)...'
    $publicacao = Join-Path ([IO.Path]::GetTempPath()) ("agente-cameras-" + [Guid]::NewGuid().ToString('N'))
    dotnet publish $Projeto -c Release -r win-x64 --self-contained true -o $publicacao
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar o projeto.' }
}

Write-Host '2/6 Parando o serviço atual (se existir)...'
$existente = Get-Service -Name $NomeServico -ErrorAction SilentlyContinue
if ($existente -and $existente.Status -ne 'Stopped') {
    Stop-Service -Name $NomeServico -Force
    $existente.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

Write-Host "3/6 Copiando arquivos para $Destino ..."
New-Item -ItemType Directory -Force -Path $Destino | Out-Null
# /E sem /MIR: mantém o que foi colocado à mão (ffmpeg\). A configuração do PC fica em ProgramData, fora daqui.
robocopy $publicacao $Destino /E /NFL /NDL /NJH /NJS /NP /XF 'appsettings.local.json' | Out-Null
if ($LASTEXITCODE -ge 8) { throw "Falha ao copiar os arquivos (robocopy $LASTEXITCODE)." }
New-Item -ItemType Directory -Force -Path (Join-Path $Destino 'ffmpeg') | Out-Null
if (-not $PastaPublicada) { Remove-Item -Recurse -Force $publicacao }

Write-Host "4/6 Preparando a pasta de dados e a configuração em $PastaDados ..."
New-Item -ItemType Directory -Force -Path $PastaDados | Out-Null
$local = Join-Path $PastaDados 'appsettings.local.json'
if (-not (Test-Path $local)) {
    Copy-Item (Join-Path $Destino 'appsettings.local.exemplo.json') $local
    Write-Host "    Criado $local a partir do exemplo: PREENCHA antes de iniciar o serviço."
} else {
    Write-Host '    appsettings.local.json já existe: mantido.'
}
# Só SYSTEM, Administradores e a conta do serviço (LocalService) acessam a pasta (segredos e fila de quadros).
icacls $PastaDados /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-19:(OI)(CI)M' | Out-Null

Write-Host '5/6 Criando/atualizando o serviço...'
$exe = Join-Path $Destino 'AAHBRANT.SST.AgenteCameras.exe'
if (-not (Get-Service -Name $NomeServico -ErrorAction SilentlyContinue)) {
    sc.exe create $NomeServico binPath= "`"$exe`"" start= delayed-auto obj= 'NT AUTHORITY\LocalService' DisplayName= 'AAHBRANT SST - Agente de Câmeras' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao criar o serviço.' }
} else {
    sc.exe config $NomeServico binPath= "`"$exe`"" start= delayed-auto obj= 'NT AUTHORITY\LocalService' | Out-Null
}
sc.exe description $NomeServico 'Lê câmeras IP por RTSP e envia quadros com movimento para a API do SST-APP.' | Out-Null
# Recuperação: reinicia após 5 s, 15 s e 60 s; zera o contador de falhas após 1 dia.
# failureflag 1: também reinicia quando o serviço sai com código de erro (ex.: configuração inválida).
sc.exe failure $NomeServico reset= 86400 actions= restart/5000/restart/15000/restart/60000 | Out-Null
sc.exe failureflag $NomeServico 1 | Out-Null

# Fonte do Event Log (só administrador consegue criar; a conta do serviço apenas escreve).
if (-not [System.Diagnostics.EventLog]::SourceExists($NomeServico)) {
    New-EventLog -LogName Application -Source $NomeServico
}

Write-Host '6/6 Pronto.'
if ($Iniciar) {
    Start-Service -Name $NomeServico
    Write-Host 'Serviço iniciado.'
} else {
    Write-Host "Serviço instalado e PARADO. Preencha $local, coloque o ffmpeg em $Destino\ffmpeg\ffmpeg.exe e execute: Start-Service $NomeServico"
}
Write-Host "Logs: $PastaDados\logs"
Write-Host ''
Write-Host 'ATENCAO: se AgenteCameras:PastaDados apontar para OUTRA pasta (fila de imagens e logs), restrinja-a tambem, como administrador:'
Write-Host '  icacls "<pasta>" /inheritance:r /grant:r *S-1-5-18:(OI)(CI)F *S-1-5-32-544:(OI)(CI)F *S-1-5-19:(OI)(CI)M'
Write-Host 'O servico confere a permissao da pasta configurada ao iniciar e registra um aviso no log se ela estiver aberta a usuarios comuns.'
