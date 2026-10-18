namespace AAHBRANT.SST.Application.Terceirizados.Portal;

public interface IPortalTerceiroConfiguracao
{
    // Endereço do app web (sem barra final), usado para montar o link enviado à empresa:
    // {UrlBase}/#/portal-terceiros/{token}.
    string UrlBase { get; }
}
