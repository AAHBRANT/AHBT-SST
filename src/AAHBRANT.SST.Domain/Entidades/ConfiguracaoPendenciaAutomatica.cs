using AAHBRANT.SST.Domain.Common;
using AAHBRANT.SST.Domain.Enums;

namespace AAHBRANT.SST.Domain.Entidades;

// Pop-up de pendências automáticas (pedido do usuário, 03/11): o sistema monta a lista a partir dos
// cadastros, por obra. Esta tabela guarda só o que o Administrador escolhe para cada tipo; tipo sem
// linha vale o padrão (ligada, toda vez que entrar, pode fechar até o próximo login).
public class ConfiguracaoPendenciaAutomatica : AuditableEntity
{
    public TipoPendenciaAutomatica Tipo { get; set; }
    public bool Ligada { get; set; } = true;
    public FrequenciaPendenciaAutomatica Frequencia { get; set; } = FrequenciaPendenciaAutomatica.TodaEntrada;
    public bool PodeFechar { get; set; } = true;
}
