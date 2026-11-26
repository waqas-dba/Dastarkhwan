DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
        CREATE SCHEMA identity;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS identity."__IdentityMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___IdentityMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "Identity_Permissions" (
    "Id" uuid NOT NULL,
    "Name" character varying(150) NOT NULL,
    "Description" character varying(500),
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Identity_Permissions" PRIMARY KEY ("Id")
);

CREATE TABLE "Identity_Roles" (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "NormalizedName" character varying(100) NOT NULL,
    "Description" character varying(500),
    "IsActive" boolean NOT NULL,
    CONSTRAINT "PK_Identity_Roles" PRIMARY KEY ("Id")
);

CREATE TABLE "Identity_Users" (
    "Id" uuid NOT NULL,
    "Email" character varying(320) NOT NULL,
    "NormalizedEmail" character varying(320) NOT NULL,
    "PasswordHash" character varying(500) NOT NULL,
    "DisplayName" character varying(200),
    "IsActive" boolean NOT NULL,
    "EmailConfirmed" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_Identity_Users" PRIMARY KEY ("Id")
);

CREATE TABLE "Identity_RolePermissions" (
    "RoleId" uuid NOT NULL,
    "PermissionId" uuid NOT NULL,
    CONSTRAINT "PK_Identity_RolePermissions" PRIMARY KEY ("RoleId", "PermissionId"),
    CONSTRAINT "FK_Identity_RolePermissions_Identity_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Identity_Permissions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Identity_RolePermissions_Identity_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Identity_Roles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Idenity_EmailVerificationTokens" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "NormalizedEmail" character varying(320) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "UsedAt" timestamp with time zone,
    CONSTRAINT "PK_Idenity_EmailVerificationTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Idenity_EmailVerificationTokens_Identity_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Identity_Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Identity_RefreshTokens" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TokenHash" character varying(128) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Identity_RefreshTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Identity_RefreshTokens_Identity_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Identity_Users" ("Id") ON DELETE CASCADE
);

CREATE TABLE "Identity_UserRoles" (
    "UserId" uuid NOT NULL,
    "RoleId" uuid NOT NULL,
    CONSTRAINT "PK_Identity_UserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_Identity_UserRoles_Identity_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Identity_Roles" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Identity_UserRoles_Identity_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Identity_Users" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_Idenity_EmailVerificationTokens_ExpiresAt" ON "Idenity_EmailVerificationTokens" ("ExpiresAt");

CREATE INDEX "IX_Idenity_EmailVerificationTokens_UserId" ON "Idenity_EmailVerificationTokens" ("UserId");

CREATE UNIQUE INDEX "UX_Identity_EmailVerificationTokens_TokenHash" ON "Idenity_EmailVerificationTokens" ("TokenHash");

CREATE UNIQUE INDEX "UX_Identity_Permissions_Name" ON "Identity_Permissions" ("Name");

CREATE INDEX "IX_Identity_RefreshTokens_ExpiresAt" ON "Identity_RefreshTokens" ("ExpiresAt");

CREATE INDEX "IX_Identity_RefreshTokens_UserId" ON "Identity_RefreshTokens" ("UserId");

CREATE UNIQUE INDEX "UX_Identity_RefreshTokens_TokenHash" ON "Identity_RefreshTokens" ("TokenHash");

CREATE INDEX "IX_Identity_RolePermissions_PermissionId" ON "Identity_RolePermissions" ("PermissionId");

CREATE UNIQUE INDEX "UX_Identity_Roles_NormalizedName" ON "Identity_Roles" ("NormalizedName");

CREATE INDEX "IX_Identity_UserRoles_RoleId" ON "Identity_UserRoles" ("RoleId");

CREATE UNIQUE INDEX "UX_Identity_Users_NormalizedEmail" ON "Identity_Users" ("NormalizedEmail");

INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108103959_InitialIdentity', '10.0.13');

COMMIT;

START TRANSACTION;
INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108104239_InitialIdentity_v1', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "Identity_Users" ADD "AccessFailedCount" integer NOT NULL DEFAULT 0;

ALTER TABLE "Identity_Users" ADD "LockoutEnd" timestamp with time zone;

INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110063614_AddIdentityLockoutColumns', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "Identity_Users" ADD "SecurityVersion" integer NOT NULL DEFAULT 0;

ALTER TABLE "Identity_RefreshTokens" ADD "FamilyId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

ALTER TABLE "Identity_RefreshTokens" ADD "RevokedReason" text;

INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110174117_DK_Core_Security_03A', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "Identity_Users" ADD "Mobile" character varying(50);

ALTER TABLE "Identity_Users" ADD "NormalizedMobile" character varying(50);

CREATE UNIQUE INDEX "UX_Identity_Users_NormalizedMobile" ON "Identity_Users" ("NormalizedMobile") WHERE "NormalizedMobile" IS NOT NULL;

INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261113000000_AddMobileColumns', '10.0.13');

COMMIT;

START TRANSACTION;
DROP TABLE "Identity_RolePermissions";

DROP TABLE "Identity_UserRoles";

DROP TABLE "Identity_Permissions";

DROP TABLE "Identity_Roles";

INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261114113050_RemoveIdentityRbacTables', '10.0.13');

COMMIT;

START TRANSACTION;
CREATE TABLE "Identity_OutboxMessages" (
    "EventId" uuid NOT NULL,
    "EventType" character varying(128) NOT NULL,
    "SchemaVersion" integer NOT NULL,
    "SubjectUserId" uuid,
    "PayloadJson" text NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "Status" integer NOT NULL,
    "AttemptCount" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "LeaseOwner" character varying(128),
    "LeaseExpiresAt" timestamp with time zone,
    "LastAttemptAt" timestamp with time zone,
    "DeliveredAt" timestamp with time zone,
    "DeadLetteredAt" timestamp with time zone,
    "LastError" character varying(512),
    CONSTRAINT "PK_Identity_OutboxMessages" PRIMARY KEY ("EventId")
);

CREATE INDEX "IX_Identity_OutboxMessages_CreatedAt" ON "Identity_OutboxMessages" ("CreatedAt");

CREATE INDEX "IX_Identity_OutboxMessages_Status_NextAttemptAt" ON "Identity_OutboxMessages" ("Status", "NextAttemptAt");

CREATE INDEX "IX_Identity_OutboxMessages_SubjectUserId" ON "Identity_OutboxMessages" ("SubjectUserId");

INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122145210_AddIdentityOutboxMessages', '10.0.13');

COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'identity') THEN
        CREATE SCHEMA identity;
    END IF;
END $EF$;

ALTER TABLE "Identity_Users" SET SCHEMA identity;

ALTER TABLE "Identity_RefreshTokens" SET SCHEMA identity;

ALTER TABLE "Identity_OutboxMessages" SET SCHEMA identity;

ALTER TABLE "Idenity_EmailVerificationTokens" SET SCHEMA identity;

INSERT INTO identity."__IdentityMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122175653_AddSchemasForSingleDb', '10.0.13');

COMMIT;

