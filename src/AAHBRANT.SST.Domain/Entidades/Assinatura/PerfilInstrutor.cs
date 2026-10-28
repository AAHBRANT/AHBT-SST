using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Perfil de instrutor de treinamento (pedido do usuário, 28/10): quem pode assinar como instrutor nos
// certificados das turmas e os dados profissionais que saem impressos no certificado. Antes o
// instrutor era só o texto livre SessaoTreinamento.InstituicaoInstrutor, e a assinatura de "instrutor"
// ficava com quem estivesse logado ao encerrar a turma (ex.: o técnico da obra, não o instrutor).
//
// Um perfil por Trabalhador. O instrutor pode ser de outra obra: PodeAssinarEmOutrasObras só vale
// com o aceite registrado (data e usuário), porque libera o uso da digital dele nos leitores das
// outras obras (dado biométrico sensível, LGPD) — ver SincronizarTemplatesQuery.
public class PerfilInstrutor : AuditableEntity
{
    public Guid TrabalhadorId { get; set; }
    public Trabalhador? Trabalhador { get; set; }

    public FormacaoInstrutor Formacao { get; set; } = FormacaoInstrutor.TecnicoSegurancaTrabalho;

    // Registro profissional impresso no certificado (ex.: MTE 12345/PB). Opcional: sem número, o
    // certificado sai sem a linha de registro em vez de inventar o dado.
    public ConselhoInstrutor? Conselho { get; set; }
    public string? NumeroRegistro { get; set; }
    public string? UfRegistro { get; set; }

    public bool PodeAssinarEmOutrasObras { get; set; }
    public DateTime? AutorizadoOutrasObrasEm { get; set; }
    public Guid? AutorizadoOutrasObrasPorUsuarioId { get; set; }
}
