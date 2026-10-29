using AAHBRANT.SST.Domain.Common;

namespace AAHBRANT.SST.Domain.Entidades;

// Planejamento flexível aprovado pelo usuário em 29/10/2026 (fora da base original de SST).
// O mês é um agregado: o JSON contém datas civis e cópias dos temas escolhidos no catálogo.
// Não se relaciona com DDS executados, atividades, presenças ou temas obrigatórios de ocorrências.
public class CronogramaMensalDds : AuditableEntity
{
    public Guid ObraId { get; set; }
    public Obra? Obra { get; set; }
    public int Ano { get; set; }
    public int Mes { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public int Versao { get; set; } = 1;
    public string ProgramacaoJson { get; set; } = "[]";
    public Guid ResponsavelUsuarioId { get; set; }
    public Usuario? ResponsavelUsuario { get; set; }
}

public class CronogramaMensalDdsRevisao : AuditableEntity
{
    public Guid CronogramaId { get; set; }
    public CronogramaMensalDds? Cronograma { get; set; }
    public int Numero { get; set; }
    public int VersaoOrigem { get; set; }
    public string HashPlanejamento { get; set; } = string.Empty;
    public string ConteudoJson { get; set; } = string.Empty;
    public Guid? DocumentoAssinaturaId { get; set; }
    public DocumentoAssinatura? DocumentoAssinatura { get; set; }
}
