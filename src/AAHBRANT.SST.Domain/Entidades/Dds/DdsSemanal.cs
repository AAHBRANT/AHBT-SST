using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// DDS Semanal (31/08) — reformulação pedida pelo usuário para seguir o modelo em papel "Registro
// Semanal de Diálogo Diário de Segurança - DDS" (Empregados Próprios / Empregados Terceirizados):
// o DDS continua sendo feito e assinado TODO DIA (ver Dds.cs, cada dia é um registro próprio,
// vinculado aqui via DdsSemanalId), mas só é "realmente finalizado" no fim da semana, quando os 5
// dias úteis estão completos e o responsável pela obra/SST encerra o conjunto — gerando o PDF
// consolidado no layout do documento original (cabeçalho + grade Seg-Sex + tabela de presença única
// com uma coluna de rubrica por dia + assinaturas de encerramento no rodapé).
public class DdsSemanal : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }

    public TipoDdsSemanal Tipo { get; set; } = TipoDdsSemanal.Proprios;

    // Só preenchido quando Tipo = Terceirizados — o documento em papel tem "Empresa contratante"
    // (sempre a própria AAHBRANT, resolvida no PDF, não guardada aqui) e "Empresa terceirizada".
    public string? EmpresaTerceirizada { get; set; }

    public string? NumeroDocumento { get; set; }
    public string? LocalFrenteServico { get; set; }

    // "Responsável/Treinador pelo DDS" do documento em papel — decisão do usuário (31/08): é sempre
    // o usuário logado que abriu esta semana, sem cerimônia de assinatura separada (mesmo raciocínio
    // de MetodoAutenticacaoAssinatura.SessaoLogada usado em outros módulos).
    public Guid ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }

    // Segunda a sexta da semana (5 dias úteis, igual ao documento) — DataFimSemana é sempre
    // DataInicioSemana + 4 dias, calculado no handler de criação, não aqui.
    public DateTime DataInicioSemana { get; set; }
    public DateTime DataFimSemana { get; set; }

    public StatusDdsSemanal Status { get; set; } = StatusDdsSemanal.EmAndamento;

    // Preenchidos só no encerramento (ver EncerrarDdsSemanalCommand) — "Responsável da Obra/SST" é,
    // pelo mesmo raciocínio do ResponsavelUsuarioId acima, o usuário logado que encerra a semana.
    // "Responsável da Empresa Terceirizada" normalmente não tem login no sistema (é alguém de fora),
    // por isso fica como texto livre, igual ao papel, e só se aplica quando Tipo = Terceirizados.
    public Guid? ResponsavelObraSstUsuarioId { get; set; }
    public Usuario? ResponsavelObraSstUsuario { get; set; }
    public string? ResponsavelEmpresaTerceirizadaNome { get; set; }
    public string? ResponsavelEmpresaTerceirizadaFuncao { get; set; }
    public DateTime? EncerradaEm { get; set; }

    public ICollection<Dds> RegistrosDiarios { get; set; } = new List<Dds>();
}
