/*
  NeuralNetGoal web - authentication tables (run ONCE, safe to re-run: every step checks first).
  Run it in SSMS (or the SmarterASP.NET SQL manager) against the EXISTING database.
  No Entity Framework migrations are used: the service reads these tables with Dapper.

  Everything lives in its own schema [auth], so it cannot collide with the legacy dbo objects.

  The Admin user is created WITHOUT a password (PasswordHash is NULL). The password is never in this script:
  on its first start the service sets it from the secret  Auth:AdminInitialPassword  (user-secrets / environment),
  hashes it with PBKDF2, and forces a password change at the first login. See the plan, step 6.
*/

SET QUOTED_IDENTIFIER ON;
GO

IF SCHEMA_ID(N'auth') IS NULL EXEC(N'CREATE SCHEMA auth');
GO

/* ---------- Roles ---------- */
IF OBJECT_ID(N'auth.Roles', N'U') IS NULL
CREATE TABLE auth.Roles
(
    RoleId      int IDENTITY(1,1) NOT NULL CONSTRAINT PK_auth_Roles PRIMARY KEY,
    Name        nvarchar(50)  NOT NULL,
    Description nvarchar(200) NULL,
    CONSTRAINT UQ_auth_Roles_Name UNIQUE (Name)
);
GO

/* ---------- Users ---------- */
IF OBJECT_ID(N'auth.Users', N'U') IS NULL
CREATE TABLE auth.Users
(
    UserId             int IDENTITY(1,1) NOT NULL CONSTRAINT PK_auth_Users PRIMARY KEY,
    UserName           nvarchar(100) NOT NULL,                 -- login name; unique, case-insensitive (database collation)
    Email              nvarchar(256) NULL,
    DisplayName        nvarchar(100) NULL,
    PasswordHash       nvarchar(500) NULL,                     -- PBKDF2 string "v1.<iterations>.<salt>.<hash>"; NULL = cannot log in yet
    PasswordUpdatedUtc datetime2(0)  NULL,
    MustChangePassword bit           NOT NULL CONSTRAINT DF_auth_Users_MustChange DEFAULT (1),
    IsActive           bit           NOT NULL CONSTRAINT DF_auth_Users_IsActive   DEFAULT (1),
    FailedLoginCount   int           NOT NULL CONSTRAINT DF_auth_Users_Failed     DEFAULT (0),
    LockoutEndUtc      datetime2(0)  NULL,                     -- login refused until this moment
    LastLoginUtc       datetime2(0)  NULL,
    SecurityStamp      uniqueidentifier NOT NULL CONSTRAINT DF_auth_Users_Stamp   DEFAULT (NEWID()), -- change it to sign a user out everywhere
    CreatedUtc         datetime2(0)  NOT NULL CONSTRAINT DF_auth_Users_Created    DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_auth_Users_UserName UNIQUE (UserName)
);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_auth_Users_Email' AND object_id = OBJECT_ID(N'auth.Users'))
    CREATE UNIQUE INDEX UX_auth_Users_Email ON auth.Users (Email) WHERE Email IS NOT NULL;
GO

/* ---------- User <-> Role ---------- */
IF OBJECT_ID(N'auth.UserRoles', N'U') IS NULL
CREATE TABLE auth.UserRoles
(
    UserId int NOT NULL CONSTRAINT FK_auth_UserRoles_User REFERENCES auth.Users (UserId) ON DELETE CASCADE,
    RoleId int NOT NULL CONSTRAINT FK_auth_UserRoles_Role REFERENCES auth.Roles (RoleId) ON DELETE CASCADE,
    CONSTRAINT PK_auth_UserRoles PRIMARY KEY (UserId, RoleId)
);
GO

/* ---------- Login audit (who tried to sign in, from where, did it work) ---------- */
IF OBJECT_ID(N'auth.LoginAudit', N'U') IS NULL
BEGIN
    CREATE TABLE auth.LoginAudit
    (
        LoginAuditId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_auth_LoginAudit PRIMARY KEY,
        UserName     nvarchar(100) NOT NULL,
        UserId       int           NULL,                       -- NULL when the name does not exist
        Succeeded    bit           NOT NULL,
        Reason       nvarchar(100) NULL,                       -- e.g. BadPassword, Locked, Inactive, NoPasswordSet
        IpAddress    nvarchar(64)  NULL,
        UserAgent    nvarchar(300) NULL,
        CreatedUtc   datetime2(0)  NOT NULL CONSTRAINT DF_auth_LoginAudit_Created DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_auth_LoginAudit_Created ON auth.LoginAudit (CreatedUtc DESC);
END
GO

/* ---------- Seed: roles ---------- */
INSERT INTO auth.Roles (Name, Description)
SELECT v.Name, v.Description
FROM (VALUES
        (N'Admin',    N'Everything, including user management'),
        (N'Operator', N'Run results / tables / fixtures and export Excel'),
        (N'Viewer',   N'Read-only: look at leagues, results, fixtures')
     ) AS v (Name, Description)
WHERE NOT EXISTS (SELECT 1 FROM auth.Roles r WHERE r.Name = v.Name);
GO

/* ---------- Seed: the Admin user (no password yet, see the header) ---------- */
IF NOT EXISTS (SELECT 1 FROM auth.Users WHERE UserName = N'Admin')
    INSERT INTO auth.Users (UserName, Email, DisplayName, PasswordHash, MustChangePassword, IsActive)
    VALUES (N'Admin', N'admin@neuralnetgoal.cloud', N'Administrator', NULL, 1, 1);   -- change the e-mail if you wish
GO

INSERT INTO auth.UserRoles (UserId, RoleId)
SELECT u.UserId, r.RoleId
FROM auth.Users u
JOIN auth.Roles r ON r.Name = N'Admin'
WHERE u.UserName = N'Admin'
  AND NOT EXISTS (SELECT 1 FROM auth.UserRoles ur WHERE ur.UserId = u.UserId AND ur.RoleId = r.RoleId);
GO

/* ---------- Check ---------- */
SELECT u.UserId, u.UserName, u.Email, HasPassword = CASE WHEN u.PasswordHash IS NULL THEN 0 ELSE 1 END,
       Roles = STRING_AGG(r.Name, N', ')
FROM auth.Users u
LEFT JOIN auth.UserRoles ur ON ur.UserId = u.UserId
LEFT JOIN auth.Roles r      ON r.RoleId = ur.RoleId
GROUP BY u.UserId, u.UserName, u.Email, u.PasswordHash;
GO
