DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'tenant') THEN
        CREATE SCHEMA tenant;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS tenant."__TenantMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___TenantMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE TABLE "TENANT_AuditEntries" (
    "Id" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Action" character varying(100) NOT NULL,
    "ActorUserId" uuid,
    "Details" character varying(2000),
    "OccurredAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TENANT_AuditEntries" PRIMARY KEY ("Id")
);

CREATE TABLE "TENANT_Tenants" (
    "Id" uuid NOT NULL,
    "Name" character varying(200) NOT NULL,
    "Slug" character varying(63) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "StatusReason" character varying(500),
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "StatusChangedAt" timestamp with time zone NOT NULL,
    "ConcurrencyStamp" uuid NOT NULL,
    CONSTRAINT "PK_TENANT_Tenants" PRIMARY KEY ("Id")
);

CREATE TABLE "TENANT_Members" (
    "TenantId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "IsOwner" boolean NOT NULL,
    "IsDefault" boolean NOT NULL,
    "JoinedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TENANT_Members" PRIMARY KEY ("TenantId", "UserId"),
    CONSTRAINT "FK_TENANT_Members_TENANT_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "TENANT_Tenants" ("Id") ON DELETE CASCADE
);

CREATE TABLE "TENANT_Settings" (
    "TenantId" uuid NOT NULL,
    "Key" character varying(100) NOT NULL,
    "Value" character varying(4000) NOT NULL,
    "UpdatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_TENANT_Settings" PRIMARY KEY ("TenantId", "Key"),
    CONSTRAINT "FK_TENANT_Settings_TENANT_Tenants_TenantId" FOREIGN KEY ("TenantId") REFERENCES "TENANT_Tenants" ("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_TENANT_AuditEntries_TenantId_OccurredAt" ON "TENANT_AuditEntries" ("TenantId", "OccurredAt");

CREATE INDEX "IX_TENANT_Members_UserId" ON "TENANT_Members" ("UserId");

CREATE INDEX "IX_TENANT_Tenants_Status" ON "TENANT_Tenants" ("Status");

CREATE UNIQUE INDEX "UX_TENANT_Tenants_Slug" ON "TENANT_Tenants" ("Slug");

INSERT INTO tenant."__TenantMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261108104346_InitialTenant', '10.0.13');

COMMIT;

START TRANSACTION;
INSERT INTO tenant."__TenantMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261110064018_AddInitTenant', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "TENANT_Members" ADD "MembershipGeneration" bigint NOT NULL DEFAULT 1;

ALTER TABLE "TENANT_Members" ADD "RemovedAt" timestamp with time zone;

INSERT INTO tenant."__TenantMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261113103350_AddTenantMembershipGeneration', '10.0.13');

COMMIT;

START TRANSACTION;
ALTER TABLE "TENANT_Tenants" ADD "LastEventSequence" bigint NOT NULL DEFAULT 0;

CREATE TABLE "TENANT_OutboxMessages" (
    "EventId" uuid NOT NULL,
    "TenantId" uuid NOT NULL,
    "Sequence" bigint NOT NULL,
    "EventType" character varying(100) NOT NULL,
    "SchemaVersion" integer NOT NULL,
    "SubjectUserId" uuid,
    "MembershipGeneration" bigint,
    "PayloadJson" character varying(4000) NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "Status" character varying(20) NOT NULL,
    "AttemptCount" integer NOT NULL,
    "NextAttemptAt" timestamp with time zone NOT NULL,
    "LeaseOwner" character varying(100),
    "LeaseExpiresAt" timestamp with time zone,
    "LastAttemptAt" timestamp with time zone,
    "DeliveredAt" timestamp with time zone,
    "DeadLetteredAt" timestamp with time zone,
    "LastError" character varying(2000),
    CONSTRAINT "PK_TENANT_OutboxMessages" PRIMARY KEY ("EventId")
);

CREATE INDEX "IX_TENANT_OutboxMessages_Status_NextAttempt" ON "TENANT_OutboxMessages" ("Status", "NextAttemptAt");

CREATE UNIQUE INDEX "UX_TENANT_OutboxMessages_Tenant_Sequence" ON "TENANT_OutboxMessages" ("TenantId", "Sequence");

INSERT INTO tenant."__TenantMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261116100911_AddTenantOutboxSchema', '10.0.13');

COMMIT;

START TRANSACTION;
DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'tenant') THEN
        CREATE SCHEMA tenant;
    END IF;
END $EF$;

ALTER TABLE "TENANT_Tenants" SET SCHEMA tenant;

ALTER TABLE "TENANT_Settings" SET SCHEMA tenant;

ALTER TABLE "TENANT_OutboxMessages" SET SCHEMA tenant;

ALTER TABLE "TENANT_Members" SET SCHEMA tenant;

ALTER TABLE "TENANT_AuditEntries" SET SCHEMA tenant;

INSERT INTO tenant."__TenantMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261122175828_AddSchemasForSingleDb', '10.0.13');

COMMIT;

