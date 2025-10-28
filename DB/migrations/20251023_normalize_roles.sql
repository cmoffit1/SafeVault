-- Idempotent migration to add Roles and UserRoles tables and backfill from Users.Roles CSV column.
-- Run on SQL Server. Make a backup before running in production.

BEGIN TRANSACTION;

-- Create Roles table if missing
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Roles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Roles]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Name] NVARCHAR(100) NOT NULL
>        
    );
    CREATE UNIQUE INDEX IX_Roles_Name ON [dbo].[Roles]([Name]);
END

-- Create UserRoles join table if missing
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserRoles]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserRoles]
    (
        [UserId] INT NOT NULL,
        [RoleId] INT NOT NULL,
        CONSTRAINT FK_UserRoles_User FOREIGN KEY([UserId]) REFERENCES [dbo].[Users]([Id]) ON DELETE CASCADE,
        CONSTRAINT FK_UserRoles_Role FOREIGN KEY([RoleId]) REFERENCES [dbo].[Roles]([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IX_UserRoles_User_Role ON [dbo].[UserRoles]([UserId],[RoleId]);
    CREATE INDEX IX_UserRoles_RoleId ON [dbo].[UserRoles]([RoleId]);
END

-- Backfill roles from Users.Roles CSV column if Users table has a Roles column
IF EXISTS (SELECT * FROM sys.columns WHERE Name = N'Roles' AND Object_ID = Object_ID(N'dbo.Users'))
BEGIN
    -- Insert distinct role names
    INSERT INTO [dbo].[Roles]([Name])
    SELECT DISTINCT LTRIM(RTRIM(value))
    FROM [dbo].[Users]
    CROSS APPLY STRING_SPLIT(ISNULL(Roles, ''), ',')
    WHERE LTRIM(RTRIM(value)) <> ''
      AND NOT EXISTS (SELECT 1 FROM [dbo].[Roles] r WHERE r.Name = LTRIM(RTRIM(value)));

    -- Insert mappings into UserRoles
    INSERT INTO [dbo].[UserRoles]([UserId],[RoleId])
    SELECT u.Id, r.Id
    FROM [dbo].[Users] u
    CROSS APPLY STRING_SPLIT(ISNULL(u.Roles, ''), ',') s
    CROSS APPLY (SELECT LTRIM(RTRIM(s.value)) AS RoleName) sn
    JOIN [dbo].[Roles] r ON r.Name = sn.RoleName
    WHERE sn.RoleName <> ''
      AND NOT EXISTS (SELECT 1 FROM [dbo].[UserRoles] ur WHERE ur.UserId = u.Id AND ur.RoleId = r.Id);
END

COMMIT TRANSACTION;

-- Notes:
-- - This script is idempotent and safe to re-run. It does not remove or alter the existing Users.Roles column.
-- - After verifying successful migration, consider removing the Users.Roles column in a follow-up migration.
-- - Ensure you have a backup before running in production. Test this script on a staging copy first.
