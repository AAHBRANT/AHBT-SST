using AAHBRANT.SST.Domain.Entidades;
using AAHBRANT.SST.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AAHBRANT.SST.Infrastructure.Persistencia.Seed;

// Seeder idempotente dos 5 checklists de Veículos v1 (um por TipoVeiculo). Itens transcritos
// literalmente da planilha "CHECK LIST - AT CUIA.xlsx" (abas "CHECK LIST DIÁRIO - ..."), fornecida
// pelo usuário em 02/10/2026. A planilha tem legenda C / NC / NA (= StatusItemChecklist) e uma
// única coluna por dia; o SST usa inspeção avulsa (sem grade diária), por decisão do usuário.
// ExigeFotografia = false em todos os itens de propósito: a foto dos Veículos é obrigatória só
// quando o item é marcado Não Conforme — regra tratada em EncerrarInspecaoCommandHandler (por
// tipo de inspeção), não por flag de item.
// Roda por tipo de veículo: só cria o checklist se NENHUM ChecklistModelo daquele TipoVeiculo
// existir (inclusive inativo). Se o time já criou nova versão ou excluiu pela tela, nada volta.
public static class ChecklistVeiculoSeeder
{
    private static readonly (TipoVeiculo Tipo, string Nome, string[] Itens)[] Checklists =
    {
        (TipoVeiculo.CaminhaoBasculante, "Checklist de Caminhão Basculante", new[]
        {
            "Sistema luminoso ( farois , ré , pisca alerta , etc ) funcionando corretamente ?",
            "Buzina e alarme de ré ?",
            "Apresenta vazamento no equipamento ?",
            "Pneus e estepe em boas condições de uso ?",
            "Conservação do equipamento ?",
            "Condições do para - brisa ?",
            "Circuito eletrico em boas condições ?",
            "Condições do basculante ?",
            "Freios estacionario e de serviço funcionando corretamente ?",
            "Extintor de incendio ?",
            "Bancos em boas condições ?",
            "Possui triangulo , macaco e chave de roda?",
            "Possui tacografo e funcionando corretamente ?",
            "Possui faixas refletivas em seu lado externo ?",
        }),
        (TipoVeiculo.Retroescavadeira, "Checklist de Retroescavadeira", new[]
        {
            "Espelhos retrovisores estão em perfeitas condições ?",
            "Para-brisa em boas condições ? Limpador de para-brisa e esguincho de água funcionando corretamente?",
            "Possui luz e alarme sonoro de marcha ré, bem como luz de freio, funcionando corretamente ?",
            "Possui extintor de incêndio e está dentro do prazo de validade?",
            "Possui cinto de segurança em perfeita condições de uso ?",
            "As partes rotativas motoras estão totalmente protegidas ?",
            "Sistema de bloqueio da máquina ( chave geral ) funcionado ?",
            "As trancas das portas estão em condições de uso ?",
            "Possui buzina e está funcionando ?",
            "O equipamento possui manual de instrução / operação em lingua patria e o mesmo está disponivel para consulta ?",
            "Os bancos e assentos estão adequadamente fixos e em boas condições ?",
            "Possui fitas refletivas em seus lados externos ?",
            "Pneus em boas condições de uso , dianteiro e traseiro ?",
            "Corrimão da maquina em boas condições ?",
        }),
        (TipoVeiculo.EscavadeiraHidraulica, "Checklist de Escavadeira Hidráulica", new[]
        {
            "Espelhos retrovisores estão em perfeitas condições ?",
            "Para-brisa em boas condições ? Limpador de para-brisa e esguincho de água funcionando corretamente ?",
            "Possui luz e alarme sonoro de marcha ré, bem como luz de freio, funcionando corretamente ?",
            "Possui extintor de incêndio e está dentro do prazo de validade?",
            "Possui cinto de segurança em perfeita condições de uso ?",
            "As partes rotativas motoras estão totalmente protegidas ?",
            "Sistema de bloqueio da máquina ( chave geral ) funcionado ?",
            "As trancas das portas estão em condições de uso ?",
            "Possui buzina e está funcionando ?",
            "O equipamento possui manual de instrução / operação em lingua patria e o mesmo está disponivel para consulta ?",
            "Os bancos e assentos estão adequadamente fixos e em boas condições ?",
            "Possui fitas refletivas em seus lados externos ?",
            "Tensão da esteira, adequadamente ?",
            "Corrimão da maquina em boas condições ?",
        }),
        (TipoVeiculo.CaminhaoCarroceria, "Checklist de Caminhão Carroceria", new[]
        {
            "Espelhos retrovisores estão em perfeitas condições ?",
            "Para-brisa em boas condições? Limpador de para-brisa e esguicho de água funcionando corretamente ?",
            "Sistema elétrico funcionando corretamente (luz de freio, luz de ré, setas, pisca alerta, faróis alto e baixo, faroletes, painél de controle, etc) ?",
            "Possui alarme sonoro de marcha-a-ré funcionando corretamente ?",
            "Os freios estacionário e de serviço, estão em boas condições ?",
            "A buzina está funcionando corretamente ?",
            "Possui extintor de incêndio e esta dentro do prazo de validade ?",
            "Possui cinto de segurança para motorista e passageiro ?",
            "Condições das maçanetas das portas (externas e internas) ?",
            "Possui tacógrafo e está funcionando corretamente? Está com disco? Está com horario ajustado ?",
            "Todas as lâmpadas do painél estão em funcionamento ?",
            "Pára-choques dianteiro e traseiro em boas condições?",
            "Os bancos (assentos) estão adequadamente fixos e em boas condições ?",
            "Condições dos pneus e estepe (TWI) ?",
            "Possui triangulo, chave de roda e macaco? Estão em boas condições ?",
            "Possui fitas refletivas em seus lados externos ?",
            "A carroceria de madeira está em bom estado de conservação ?",
        }),
        (TipoVeiculo.CaminhaoMunck, "Checklist de Caminhão Munck", new[]
        {
            "Sistema elétrico funcionando corretamente (setas, luz de ré, pisca alerta , etc) ?",
            "Buzina e alarme sonoro de ré estão funcionando?",
            "Não apresenta vazamentos no sistema hidráulico, mangueiras, conexões e pistões?",
            "Pneus e estepe em boas condições de uso ?",
            "Caminhão / equipamento em perfeito estado de conservação ?",
            "Sitema operacional ( lança principal, lança auxiliar, elevação de cargas, giro, alavancas de comando) em perfeitas condições de uso ?",
            "Sistema de patolamento em perfeito estado de funcionamento ( existencia de calços) ?",
            "Existe trava de segurança do gancho e está em boas condições ?",
            "Freio de roda e estacionamento funcionando corretamente ?",
            "Possui extintor de incêndio ?",
            "Todas as lâmpadas do painél estão em funcionamento ?",
            "Pára-choques dianteiro e traseiro em boas condições?",
            "Os bancos (assentos) estão adequadamente fixos e em boas condições ?",
            "Possui triangulo, chave de roda e macaco? Estão em boas condições ?",
            "Possui fitas refletivas em seus lados externos ?",
            "A carroceria está em bom estado de conservação ?",
        }),
    };

    public static async Task ExecutarAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SstDbContext>();

        foreach (var (tipo, nome, itens) in Checklists)
        {
            var jaExiste = await db.ChecklistModelos
                .IgnoreQueryFilters()
                .AnyAsync(c => c.TipoInspecao == TipoInspecao.Veiculo && c.TipoVeiculo == tipo, ct);
            if (jaExiste) continue;

            var checklist = new ChecklistModelo
            {
                Nome = nome,
                TipoInspecao = TipoInspecao.Veiculo,
                TipoVeiculo = tipo,
                Versao = 1,
            };

            var ordem = 1;
            foreach (var descricao in itens)
            {
                checklist.Itens.Add(new ChecklistModeloItem
                {
                    Ordem = ordem++,
                    Descricao = descricao,
                    ExigeFotografia = false,
                    ExigeResponsavel = false,
                    ExigePrazo = false,
                });
            }

            db.ChecklistModelos.Add(checklist);
        }

        await db.SaveChangesAsync(ct);
    }
}
