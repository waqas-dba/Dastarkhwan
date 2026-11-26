DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'access') THEN
        CREATE SCHEMA access;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS access."__AccessMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___AccessMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "ACCESS_AuditEntries" (
    "Id" uuid NOT NULL,
    "ScopeType" character varying(30) NOT NULL,
    "ScopeId" uuid NOT NULL,
    "Action" character varying(100) NOT NULL,
    "ActorUserId" uuid,
    "Details" character varying(2000),
    "OccurredAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCESS_AuditEntries" PRIMARY KEY ("Id")
);

CREATE TABLE "ACCESS_Roles" (
    "Id" uuid NOT NULL,
    "ScopeType" character varying(30) NOT NULL,
    "ScopeId" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "NormalizedName" character varying(100) NOT NULL,
    "Description" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "ConcurrencyStamp" uuid NOT NULL,
    CONSTRAINT "PK_ACCESS_Roles" PRIMARY KEY ("Id")
);

CREATE TABLE "ACCESS_Assignments" (
    "RoleId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ScopeType" character varying(30) NOT NULL,
    "ScopeId" uuid NOT NULL,
    "AssignedAt" timestamp with time zone NOT NULL,
    "AssignedBy" uuid,
    CONSTRAINT "PK_ACCESS_Assignments" PRIMARY KEY ("RoleId", "UserId"),
    CONSTRAINT "FK_ACCESS_Assignments_ACCESS_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "ACCESS_Roles" ("Id") ON DELETE CASCADE
);

CREATE TABLE "ACCESS_RolePermissions" (
    "RoleId" uuid NOT NULL,
    "Permission" character varying(100) NOT NULL,
    CONSTRAINT "PK_ACCESS_RolePermissions" PRIMARY KEY ("RoleId", "Permission"),
    CONSTRAINT "FK_ACCESS_RolePermissions_ACCESS_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "ACCESS_Roles" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_ACCESS_Assignments_ScopeType_ScopeId_UserId" ON "ACCESS_Assignments" ("ScopeType", "ScopeId", "UserId");

CREATE INDEX "IX_ACCESS_AuditEntries_ScopeType_ScopeId_OccurredAt" ON "ACCESS_AuditEntries" ("ScopeType", "ScopeId", "OccurredAt");

CREATE UNIQUE INDEX "UX_ACCESS_Roles_Scope_Name" ON "ACCESS_Roles" ("ScopeType", "ScopeId", "NormalizedName");

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108104408_InitialAccess', '10.0.13');

COMMIT;

START TRANSACTION;
INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110063838_AddInitAccess', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "ACCESS_Assignments" DROP CONSTRAINT "FK_ACCESS_Assignments_ACCESS_Roles_RoleId";

ALTER TABLE "ACCESS_Roles" ADD CONSTRAINT "AK_ACCESS_Roles_Id_ScopeType_ScopeId" UNIQUE ("Id", "ScopeType", "ScopeId");

CREATE INDEX "IX_ACCESS_Assignments_RoleId_ScopeType_ScopeId" ON "ACCESS_Assignments" ("RoleId", "ScopeType", "ScopeId");

ALTER TABLE "ACCESS_Assignments" ADD CONSTRAINT "FK_ACCESS_Assignments_ACCESS_Roles_RoleId_ScopeType_ScopeId" FOREIGN KEY ("RoleId", "ScopeType", "ScopeId") REFERENCES "ACCESS_Roles" ("Id", "ScopeType", "ScopeId") ON DELETE CASCADE;

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110090000_EnforceAssignmentRoleScopeConsistency', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "ACCESS_Assignments" ADD "MembershipGeneration" bigint NOT NULL DEFAULT 0;

CREATE TABLE "ACCESS_PlatformMembers" (
    "UserId" uuid NOT NULL,
    "PlatformId" uuid NOT NULL,
    "MembershipGeneration" bigint NOT NULL,
    "JoinedAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    CONSTRAINT "PK_ACCESS_PlatformMembers" PRIMARY KEY ("UserId")
);

CREATE INDEX "IX_ACCESS_PlatformMembers_PlatformId_UserId" ON "ACCESS_PlatformMembers" ("PlatformId", "UserId");

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261113083722_AddPlatformMembershipAndGeneration', '10.0.13');

COMMIT;

START TRANSACTION;
CREATE TABLE "ACCESS_MembershipProjections" (
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Status" integer NOT NULL,
    "MembershipGeneration" bigint NOT NULL,
    "LastAppliedSequence" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCESS_MembershipProjections" PRIMARY KEY ("TenantId", "UserId")
);

CREATE INDEX "IX_ACCESS_MembershipProjections_UserId" ON "ACCESS_MembershipProjections" ("UserId");

CREATE UNIQUE INDEX "UX_ACCESS_MembershipProjections_Tenant_User" ON "ACCESS_MembershipProjections" ("TenantId", "UserId");

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261115151925_AddAccessMembershipProjection', '10.0.13');

COMMIT;

START TRANSACTION;
CREATE TABLE "ACCESS_MembershipStreamCursors" (
    "TenantId" uuid NOT NULL,
    "LastAppliedSequence" bigint NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCESS_MembershipStreamCursors" PRIMARY KEY ("TenantId")
);

CREATE TABLE "ACCESS_MembershipWatermarks" (
    "Id" uuid NOT NULL,
    "AsOf" timestamp with time zone NOT NULL,
    "ReceivedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCESS_MembershipWatermarks" PRIMARY KEY ("Id")
);

CREATE TABLE "ACCESS_ProcessedEvents" (
    "EventId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Sequence" bigint NOT NULL,
    "EventType" character varying(100) NOT NULL,
    "ProcessedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCESS_ProcessedEvents" PRIMARY KEY ("EventId")
);

CREATE TABLE "ACCESS_QuarantinedTenants" (
    "TenantId" uuid NOT NULL,
    "Since" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCESS_QuarantinedTenants" PRIMARY KEY ("TenantId")
);

CREATE TABLE "ACCESS_TenantStates" (
    "TenantId" uuid NOT NULL,
    "Status" character varying(20) NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_ACCESS_TenantStates" PRIMARY KEY ("TenantId")
);

CREATE UNIQUE INDEX "UX_ACCESS_ProcessedEvents_Tenant_Sequence" ON "ACCESS_ProcessedEvents" ("TenantId", "Sequence");

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116043029_AddAccessMembershipStreamAndFreshness', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "ACCESS_AuditEntries" ADD "ActorType" character varying(30);

ALTER TABLE "ACCESS_AuditEntries" ADD "ActorSource" character varying(200);

UPDATE "ACCESS_AuditEntries" SET "ActorType" = 'User' WHERE "ActorUserId" IS NOT NULL;
UPDATE "ACCESS_AuditEntries" SET "ActorType" = 'Unknown' WHERE "ActorUserId" IS NULL;

ALTER TABLE "ACCESS_AuditEntries" ALTER COLUMN "ActorType" SET NOT NULL;

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261121130000_AddAccessAuditActorSchema', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "ACCESS_AuditEntries" ADD CONSTRAINT "CK_ACCESS_AuditEntries_Actor" CHECK (
                    ("ActorType" = 'User' AND "ActorUserId" IS NOT NULL AND CAST("ActorUserId" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND "ActorSource" IS NULL)
                    OR ("ActorType" = 'System' AND "ActorUserId" IS NULL AND "ActorSource" IS NOT NULL AND length(trim("ActorSource", chr(9)||chr(10)||chr(13)||' ')) > 0)
                    OR ("ActorType" = 'Unknown' AND "ActorUserId" IS NULL AND "ActorSource" IS NULL)
                );

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261121160000_AddAccessAuditActorCheckConstraint', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "ACCESS_AuditEntries" DROP CONSTRAINT "CK_ACCESS_AuditEntries_Actor";

ALTER TABLE "ACCESS_AuditEntries" ADD CONSTRAINT "CK_ACCESS_AuditEntries_Actor" CHECK (
                    ("ActorType" = 'User' AND "ActorUserId" IS NOT NULL AND CAST("ActorUserId" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND "ActorSource" IS NULL)
                    OR ("ActorType" = 'System' AND "ActorUserId" IS NULL AND "ActorSource" IS NOT NULL AND length(trim("ActorSource", chr(9)||chr(10)||chr(13)||' ')) > 0)
                    OR ("ActorType" = 'Unknown' AND "ActorUserId" IS NULL AND "ActorSource" IS NULL)
                );

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261121170000_CorrectAccessAuditActorWhitespaceConstraint', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "ACCESS_AuditEntries" DROP CONSTRAINT "CK_ACCESS_AuditEntries_Actor";

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'access') THEN
        CREATE SCHEMA access;
    END IF;
END $EF$;

ALTER TABLE "ACCESS_TenantStates" SET SCHEMA access;

ALTER TABLE "ACCESS_Roles" SET SCHEMA access;

ALTER TABLE "ACCESS_RolePermissions" SET SCHEMA access;

ALTER TABLE "ACCESS_QuarantinedTenants" SET SCHEMA access;

ALTER TABLE "ACCESS_ProcessedEvents" SET SCHEMA access;

ALTER TABLE "ACCESS_PlatformMembers" SET SCHEMA access;

ALTER TABLE "ACCESS_MembershipWatermarks" SET SCHEMA access;

ALTER TABLE "ACCESS_MembershipStreamCursors" SET SCHEMA access;

ALTER TABLE "ACCESS_MembershipProjections" SET SCHEMA access;

ALTER TABLE "ACCESS_AuditEntries" SET SCHEMA access;

ALTER TABLE "ACCESS_Assignments" SET SCHEMA access;

ALTER TABLE access."ACCESS_AuditEntries" ADD CONSTRAINT "CK_ACCESS_AuditEntries_Actor" CHECK (("ActorType" = 'User' AND "ActorUserId" IS NOT NULL AND CAST("ActorUserId" AS VARCHAR(36)) <> '00000000-0000-0000-0000-000000000000' AND "ActorSource" IS NULL)
    OR ("ActorType" = 'System' AND "ActorUserId" IS NULL AND "ActorSource" IS NOT NULL AND length(trim("ActorSource", chr(9)||chr(10)||chr(13)||' ')) > 0)
    OR ("ActorType" = 'Unknown' AND "ActorUserId" IS NULL AND "ActorSource" IS NULL));

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122180039_AddSchemasForSingleDb', '10.0.13');

COMMIT;

START TRANSACTION;
DROP TABLE access."ACCESS_MembershipProjections";

DROP TABLE access."ACCESS_MembershipStreamCursors";

DROP TABLE access."ACCESS_MembershipWatermarks";

DROP TABLE access."ACCESS_ProcessedEvents";

DROP TABLE access."ACCESS_QuarantinedTenants";

DROP TABLE access."ACCESS_TenantStates";

CREATE UNIQUE INDEX "UX_ACCESS_Assignments_Scope_User_Role" ON access."ACCESS_Assignments" ("ScopeType", "ScopeId", "UserId", "RoleId");

INSERT INTO access."__AccessMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261124072916_AccessModelSync', '10.0.13');

COMMIT;

