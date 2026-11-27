# Cadastra (ou troca) as 2 credenciais do cofre do Engenheiro da assinatura.
# Rodar pelo responsável, no próprio computador, com `az login` e `ant auth login` feitos.
# Nenhum valor secreto é impresso: a chave vem direto do Azure e o token do GitHub da área de transferência.
#
#   1. AGENTE_CHAVE: secret "agente-assinatura-chave" do sst-api-hml, injetado só no cabeçalho e só para a API hml.
#   2. GitHub: token fine-grained SÓ LEITURA (Contents: Read-only) do repositório AAHBRANT/AHBT-SST,
#      usado pelo servidor MCP do GitHub.
#
# Para trocar uma credencial depois, apague a antiga no Console e rode de novo só a parte necessária
# (-SoChave ou -SoGitHub).
param([switch]$SoChave, [switch]$SoGitHub)

$ErrorActionPreference = 'Stop'
$ant   = 'C:\Users\wellington.lourenco\tools\ant-2.14.0\bin\ant.exe'
$vault = 'vlt_011ChTeT3EsxDCxnwYs3qVcg'
$hostApi = 'sst-api-hml.kindground-7a44c4f0.brazilsouth.azurecontainerapps.io'

if (-not $SoGitHub) {
    Write-Host '1/2 Lendo a chave do agente no Azure (sst-api-hml)...'
    $chave = az containerapp secret show --name sst-api-hml --resource-group rg-gnezis-hub-staging `
        --secret-name agente-assinatura-chave --query value -o tsv
    if (-not $chave -or $chave.Length -lt 32) { throw 'Não consegui ler a chave no Azure (faça az login).' }

    & $ant beta:vaults:credentials create --vault-id $vault `
        --display-name 'Chave da rota interna do G-SST (hml)' `
        --auth.type environment_variable `
        --auth.secret-name AGENTE_CHAVE `
        --auth.secret-value $chave `
        --auth.networking "{type: limited, allowed_hosts: [$hostApi]}" `
        --auth.injection-location '{header: true, body: false}'
    $chave = $null
    Write-Host '   Chave do agente cadastrada.'
}

if (-not $SoChave) {
    Write-Host ''
    # O token vem da área de transferência (copie no GitHub antes de rodar) e ela é limpa em seguida:
    # colar num terminal errado faz o token virar comando e ir para o histórico do PowerShell.
    Write-Host '2/2 Lendo o token do GitHub da área de transferência...'
    $token = (Get-Clipboard -Raw)
    if ($token) { $token = $token.Trim() }
    if (-not $token -or $token -notmatch '^(github_pat_|ghp_)\S+$') {
        throw 'A área de transferência não tem um token do GitHub. Copie o token (botão de copiar no GitHub) e rode de novo.'
    }
    Set-Clipboard -Value ' '

    & $ant beta:vaults:credentials create --vault-id $vault `
        --display-name 'GitHub AHBT-SST (só leitura)' `
        --auth.type static_bearer `
        --auth.mcp-server-url 'https://api.githubcopilot.com/mcp/' `
        --auth.token $token
    $token = $null
    Write-Host '   Token do GitHub cadastrado.'
}

Write-Host ''
Write-Host 'Pronto. Credenciais no cofre:'
& $ant beta:vaults:credentials list --vault-id $vault
