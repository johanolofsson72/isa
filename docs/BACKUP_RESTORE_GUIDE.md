# BACKUP & RESTORE — Guide

Senast uppdaterad: 2025-09-11

## Omfattning (ADR-0040)
- Databaser: Postgres (långtidsminne, pluginstate), SQLite (korttidsminne).
- Filer: `/mnt/nfs/uploads/`, `/mnt/nfs/archives/`, `/mnt/nfs/reports/`, `/mnt/nfs/peppol/`.
- Konfiguration: env/KeyVault (hemligheter exkluderas från repo).

## Backup
- Frekvens: dagligen via Quartz `BackupSchedulerService`.
- Kryptering: AES-256.
- Integritet: SHA-256 checksumma per fil.
- Lagring: `/mnt/nfs/backups/` (7 dagar), sedan `/mnt/nfs/cold-archive/`.

## Restore — steg
1. Identifiera backup-set (datum + checksumma).
2. Stoppa skrivande jobb (Quartz pause).
3. Återställ DB → Postgres dump/restore, kopiera SQLite-filer.
4. Återställ filer → motsvarande kataloger (bevara rättigheter).
5. Kör integritetskontroll → checksummor mot `ArchiveRecord`.
6. Starta jobb + smoke-tests (ADR-0041).
7. Dokumentera i `JobHistory` + notifiera systemansvarig.

## Tester
- Kvartalsvis **restore-test** i testmiljö.
- Rapporter sparas i `/mnt/nfs/reports/backup-restore/`.

## Roller & åtkomst
- Endast systemansvarig får initiera restore.
- Åtkomster loggas i AuditLogService (ADR-0013, ADR-0038).
