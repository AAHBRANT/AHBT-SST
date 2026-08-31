-- Desbloqueio do 403 causado pela ativação do RBAC real (AzureAd:TenantId configurado) sem a
-- Matriz de Permissões preenchida ainda. Vincula wellington.lourenco@aahbrant.com ao perfil
-- Administrador (escopo Global) e concede TODAS as permissões do catálogo a esse perfil.
-- Idempotente: pode rodar mais de uma vez sem duplicar.

DECLARE @UsuarioId UNIQUEIDENTIFIER = (SELECT Id FROM Usuarios WHERE Email = 'wellington.lourenco@aahbrant.com');
DECLARE @PerfilId UNIQUEIDENTIFIER = (SELECT Id FROM PerfisAcesso WHERE Tipo = 1); -- 1 = Administrador

IF @UsuarioId IS NULL
BEGIN
    RAISERROR('Usuário wellington.lourenco@aahbrant.com não encontrado em Usuarios.', 16, 1);
    RETURN;
END

IF @PerfilId IS NULL
BEGIN
    RAISERROR('Perfil Administrador (Tipo=1) não encontrado em PerfisAcesso.', 16, 1);
    RETURN;
END

-- Vincula o usuário ao perfil Administrador em escopo Global (ObraId nulo)
IF NOT EXISTS (
    SELECT 1 FROM UsuariosPerfilObra
    WHERE UsuarioId = @UsuarioId AND PerfilAcessoId = @PerfilId AND ObraId IS NULL
)
INSERT INTO UsuariosPerfilObra (Id, UsuarioId, PerfilAcessoId, ObraId, CreatedAtUtc, Origem, Ativo)
VALUES (NEWID(), @UsuarioId, @PerfilId, NULL, SYSUTCDATETIME(), 0, 1);

-- Concede todas as permissões do catálogo ao perfil Administrador, em escopo Global
INSERT INTO PerfisAcessoPermissoes (Id, PerfilAcessoId, PermissaoId, Escopo, Permitido, CreatedAtUtc, Origem, Ativo)
SELECT NEWID(), @PerfilId, p.Id, 1, 1, SYSUTCDATETIME(), 0, 1
FROM Permissoes p
WHERE NOT EXISTS (
    SELECT 1 FROM PerfisAcessoPermissoes pp
    WHERE pp.PerfilAcessoId = @PerfilId AND pp.PermissaoId = p.Id
);

-- Conferência
SELECT u.Email, pa.Nome AS Perfil, upo.ObraId
FROM UsuariosPerfilObra upo
JOIN Usuarios u ON u.Id = upo.UsuarioId
JOIN PerfisAcesso pa ON pa.Id = upo.PerfilAcessoId
WHERE u.Email = 'wellington.lourenco@aahbrant.com';

SELECT COUNT(*) AS TotalPermissoesConcedidas
FROM PerfisAcessoPermissoes
WHERE PerfilAcessoId = @PerfilId AND Permitido = 1;
