-- Выполнить в DBeaver/psql под владельцем БД (postgres), подключившись к tgk_bd.

ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "AccountType" character varying(20) NOT NULL DEFAULT 'Physical';

CREATE TABLE IF NOT EXISTS "LegalEntityProfiles" (
    "UserId" uuid NOT NULL,
    "CompanyFullName" character varying(500) NOT NULL,
    "CompanyShortName" character varying(160) NOT NULL,
    "Inn" character varying(10) NOT NULL,
    "Ogrn" character varying(13) NOT NULL,
    "Kpp" character varying(9) NOT NULL,
    "DirectorFullName" character varying(200) NOT NULL,
    "DirectorBirthDate" date NOT NULL,
    "DocumentFileName" character varying(255) NOT NULL,
    "DocumentContent" bytea NOT NULL,
    "VerifiedAt" timestamp with time zone NOT NULL DEFAULT NOW(),
    CONSTRAINT "PK_LegalEntityProfiles" PRIMARY KEY ("UserId"),
    CONSTRAINT "FK_LegalEntityProfiles_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE
);
