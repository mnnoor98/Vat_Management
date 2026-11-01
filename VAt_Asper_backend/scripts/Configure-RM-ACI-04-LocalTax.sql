\set ON_ERROR_STOP on
BEGIN;
LOCK TABLE tax.tax_rule_versions IN SHARE ROW EXCLUSIVE MODE;
DO $$
DECLARE v_rule uuid := '3736d97b-354d-4de3-ad7e-5bfaa26313fd';
BEGIN
 IF NOT EXISTS (SELECT 1 FROM masterdata.products WHERE "TenantId"='00000000-0000-0000-0000-000000000001' AND "CompanyId"='11111111-1111-1111-1111-111111111111' AND "Reference"='RM-ACI-04' AND "TaxCode"='2916.20.00') THEN
  RAISE EXCEPTION 'Expected product/tax-code mapping was not found';
 END IF;
 IF EXISTS (SELECT 1 FROM tax.tax_rule_versions WHERE "TenantId"='00000000-0000-0000-0000-000000000001' AND "CompanyId"='11111111-1111-1111-1111-111111111111' AND "TaxCode"='2916.20.00' AND "Treatment"='Purchase' AND "Id"<>v_rule AND "EffectiveFrom"<'2026-11-01' AND "EffectiveUntil">'2026-10-30') THEN
  RAISE EXCEPTION 'An overlapping rule exists; review it before applying this local configuration';
 END IF;
 IF NOT EXISTS (SELECT 1 FROM tax.tax_rule_versions WHERE "Id"=v_rule) THEN
  INSERT INTO tax.tax_rule_versions ("TenantId","CompanyId","Id","TaxCode","Treatment","RatePercent","RecoverablePercent","EffectiveFrom","EffectiveUntil","LegalBasis","EvidenceReference","CreatedBy","PublishedBy","PublishedAt","Version")
  VALUES ('00000000-0000-0000-0000-000000000001','11111111-1111-1111-1111-111111111111',v_rule,'2916.20.00','Purchase',10,80,'2026-10-30','2026-11-01','User-configured local test rule; no statutory rate assertion.','USER-CONFIG-RM-ACI-04-20261031','user-confirmed-local-config',NULL,NULL,1);
  INSERT INTO platform.audit_entries ("Id","TenantId","CompanyId","ResourceId","ActorId","Action","CorrelationId","DocumentVersion","OccurredAt")
  VALUES (gen_random_uuid(),'00000000-0000-0000-0000-000000000001','11111111-1111-1111-1111-111111111111',v_rule,'user-confirmed-local-config','TaxRuleDrafted','USER-CONFIG-RM-ACI-04-20261031',1,now());
 END IF;
END $$;
SELECT "Id","TaxCode","RatePercent","RecoverablePercent","EffectiveFrom","EffectiveUntil","PublishedAt","Version" FROM tax.tax_rule_versions WHERE "Id"='3736d97b-354d-4de3-ad7e-5bfaa26313fd';
COMMIT;
