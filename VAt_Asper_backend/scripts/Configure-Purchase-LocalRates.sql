\set ON_ERROR_STOP on
BEGIN;
LOCK TABLE tax.tax_rule_versions IN SHARE ROW EXCLUSIVE MODE;
DO $$
DECLARE
 r record;
 v_tenant uuid := '00000000-0000-0000-0000-000000000001';
 v_company uuid := '11111111-1111-1111-1111-111111111111';
BEGIN
 FOR r IN SELECT * FROM (VALUES
  ('fdf28423-36e4-4f58-9021-6519da4b0501'::uuid, 'RM-ACI-02', '2501.00.10', 5),
  ('fdf28423-36e4-4f58-9021-6519da4b0502'::uuid, 'PROD-ACI-05', '1101.00.10', 6),
  ('fdf28423-36e4-4f58-9021-6519da4b0503'::uuid, 'PROD-ACI-01', '1514.91.00', 7)
 ) AS configured(id, reference, code, rate)
 LOOP
  IF NOT EXISTS (SELECT 1 FROM masterdata.products p WHERE p."TenantId"=v_tenant AND p."CompanyId"=v_company AND p."Reference"=r.reference AND p."TaxCode"=r.code) THEN
   RAISE EXCEPTION 'Product mapping changed for %', r.reference;
  END IF;
  IF EXISTS (SELECT 1 FROM tax.tax_rule_versions t WHERE t."TenantId"=v_tenant AND t."CompanyId"=v_company AND t."TaxCode"=r.code AND t."Treatment"='Purchase' AND t."Id"<>r.id AND t."EffectiveFrom"<'2026-11-01' AND t."EffectiveUntil">'2026-10-30') THEN
   RAISE EXCEPTION 'Overlapping rule exists for %, review before continuing', r.code;
  END IF;
  IF EXISTS (SELECT 1 FROM tax.tax_rule_versions t WHERE t."Id"=r.id AND (t."TenantId"<>v_tenant OR t."CompanyId"<>v_company OR t."TaxCode"<>r.code OR t."RatePercent"<>r.rate OR t."RecoverablePercent"<>100)) THEN
   RAISE EXCEPTION 'Existing configuration differs for %', r.reference;
  END IF;
  IF NOT EXISTS (SELECT 1 FROM tax.tax_rule_versions t WHERE t."Id"=r.id) THEN
   INSERT INTO tax.tax_rule_versions ("TenantId","CompanyId","Id","TaxCode","Treatment","RatePercent","RecoverablePercent","EffectiveFrom","EffectiveUntil","LegalBasis","EvidenceReference","CreatedBy","PublishedBy","PublishedAt","Version")
   VALUES (v_tenant,v_company,r.id,r.code,'Purchase',r.rate,100,'2026-10-30','2026-11-01','User-configured local test rule; no statutory rate assertion.','USER-CONFIG-'||r.reference||'-20261031','user-confirmed-local-config',NULL,NULL,1);
   INSERT INTO platform.audit_entries ("Id","TenantId","CompanyId","ResourceId","ActorId","Action","CorrelationId","DocumentVersion","OccurredAt")
   VALUES (gen_random_uuid(),v_tenant,v_company,r.id,'user-confirmed-local-config','TaxRuleDrafted','USER-CONFIG-'||r.reference||'-20261031',1,now());
  END IF;
 END LOOP;
END $$;
SELECT "TaxCode","RatePercent","RecoverablePercent","EffectiveFrom","EffectiveUntil","PublishedAt" FROM tax.tax_rule_versions
WHERE "Id" IN ('fdf28423-36e4-4f58-9021-6519da4b0501','fdf28423-36e4-4f58-9021-6519da4b0502','fdf28423-36e4-4f58-9021-6519da4b0503') ORDER BY "RatePercent";
COMMIT;
