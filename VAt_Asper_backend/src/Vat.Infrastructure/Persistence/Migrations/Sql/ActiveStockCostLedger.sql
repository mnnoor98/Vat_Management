CREATE FUNCTION inventory.freeze_cost_adoption() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP='INSERT' THEN
    IF NEW."State"<>'Draft' THEN RAISE EXCEPTION 'Opening must start as draft'; END IF;
  ELSIF OLD."State"<>'Draft' OR NEW."State"<>'Approved' OR
    (to_jsonb(NEW)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','ApprovedBy','ApprovedAt']) THEN
    RAISE EXCEPTION 'Only independent approval of an unchanged opening is permitted';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER freeze_cost_adoption BEFORE INSERT OR UPDATE ON inventory.cost_adoptions FOR EACH ROW EXECUTE FUNCTION inventory.freeze_cost_adoption();
CREATE TRIGGER immutable_cost_adoption BEFORE DELETE ON inventory.cost_adoptions FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_cost_adoption_truncate BEFORE TRUNCATE ON inventory.cost_adoptions FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_cost_entries BEFORE UPDATE OR DELETE ON inventory.cost_entries FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_cost_entries_truncate BEFORE TRUNCATE ON inventory.cost_entries FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_cost_positions_delete BEFORE DELETE ON inventory.cost_positions FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_cost_positions_truncate BEFORE TRUNCATE ON inventory.cost_positions FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_cost_heads_delete BEFORE DELETE ON inventory.cost_ledger_heads FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_cost_heads_truncate BEFORE TRUNCATE ON inventory.cost_ledger_heads FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();

CREATE FUNCTION inventory.freeze_cost_ledger_head() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF (to_jsonb(NEW)-'Sequence') IS DISTINCT FROM (to_jsonb(OLD)-'Sequence') OR NEW."Sequence"<=OLD."Sequence" THEN
    RAISE EXCEPTION 'Cost ledger opening and basis are frozen; sequence must advance';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER freeze_cost_ledger_head BEFORE UPDATE ON inventory.cost_ledger_heads FOR EACH ROW EXECUTE FUNCTION inventory.freeze_cost_ledger_head();

CREATE FUNCTION inventory.check_cost_position(t uuid,c uuid,w uuid,p uuid) RETURNS void LANGUAGE plpgsql AS $$
DECLARE physical_qty numeric; cost_qty numeric; cost_value numeric; journal_qty numeric; journal_value numeric; layer_qty numeric; layer_value numeric; layers jsonb; method text;
BEGIN
  IF NOT EXISTS(SELECT 1 FROM inventory.cost_ledger_heads h WHERE h."TenantId"=t AND h."CompanyId"=c) THEN RETURN; END IF;
  SELECT s."Quantity" INTO physical_qty FROM inventory.stock_balances s WHERE s."TenantId"=t AND s."CompanyId"=c AND s."WarehouseId"=w AND s."ProductId"=p;
  SELECT s."Quantity",s."Value",s."Layers",s."Method" INTO cost_qty,cost_value,layers,method FROM inventory.cost_positions s
    WHERE s."TenantId"=t AND s."CompanyId"=c AND s."WarehouseId"=w AND s."ProductId"=p;
  SELECT coalesce(sum(e."Quantity"),0),coalesce(sum(e."Value"),0) INTO journal_qty,journal_value FROM inventory.cost_entries e
    WHERE e."TenantId"=t AND e."CompanyId"=c AND e."WarehouseId"=w AND e."ProductId"=p;
  IF coalesce(physical_qty,0)<>coalesce(cost_qty,0) OR coalesce(cost_qty,0)<>journal_qty OR coalesce(cost_value,0)<>journal_value THEN
    RAISE EXCEPTION 'Cost journal, position and physical stock do not reconcile';
  END IF;
  IF layers IS NOT NULL THEN
    IF jsonb_typeof(layers)<>'array' THEN RAISE EXCEPTION 'Cost layers must be an array'; END IF;
    IF EXISTS(SELECT 1 FROM jsonb_array_elements(layers) x WHERE coalesce((x->>'Quantity')::numeric,0)<=0 OR x->>'Value' IS NULL OR
      (x->>'Value')::numeric<0 OR x->>'Id' IS NULL OR x->>'Origin' IS NULL) THEN RAISE EXCEPTION 'Invalid cost layer evidence'; END IF;
    SELECT coalesce(sum((x->>'Quantity')::numeric),0),coalesce(sum((x->>'Value')::numeric),0) INTO layer_qty,layer_value FROM jsonb_array_elements(layers) x;
    IF (method='FIFO' AND (layer_qty<>cost_qty OR layer_value<>cost_value)) OR (method='WEIGHTED_AVERAGE' AND jsonb_array_length(layers)<>0) THEN
      RAISE EXCEPTION 'Cost layer/pool reconciliation failed';
    END IF;
  END IF;
END $$;
CREATE FUNCTION inventory.validate_cost_position() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP='DELETE' THEN PERFORM inventory.check_cost_position(OLD."TenantId",OLD."CompanyId",OLD."WarehouseId",OLD."ProductId");
  ELSE PERFORM inventory.check_cost_position(NEW."TenantId",NEW."CompanyId",NEW."WarehouseId",NEW."ProductId"); END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER reconcile_cost_positions AFTER INSERT OR UPDATE ON inventory.cost_positions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_cost_position();
CREATE CONSTRAINT TRIGGER reconcile_cost_stock AFTER INSERT OR UPDATE OR DELETE ON inventory.stock_balances DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_cost_position();

CREATE FUNCTION inventory.validate_cost_ledger_head() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE previous_seq bigint:=0; n bigint; row record;
BEGIN
  IF TG_OP='UPDATE' THEN previous_seq:=OLD."Sequence"; END IF;
  IF NOT EXISTS (SELECT 1 FROM inventory.cost_adoptions a WHERE a."TenantId"=NEW."TenantId" AND a."CompanyId"=NEW."CompanyId" AND a."Id"=NEW."AdoptionId"
    AND a."State"='Approved' AND a."BusinessDate"=NEW."OpeningDate" AND a."Basis"=NEW."Basis") THEN RAISE EXCEPTION 'Cost ledger requires an approved opening'; END IF;
  SELECT count(*) INTO n FROM inventory.cost_entries e WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId" AND e."Sequence">previous_seq AND e."Sequence"<=NEW."Sequence";
  IF n<>NEW."Sequence"-previous_seq THEN RAISE EXCEPTION 'Cost ledger sequence contains a gap'; END IF;
  IF TG_OP='INSERT' THEN
    FOR row IN SELECT s."WarehouseId",s."ProductId" FROM inventory.stock_balances s WHERE s."TenantId"=NEW."TenantId" AND s."CompanyId"=NEW."CompanyId"
      UNION SELECT p."WarehouseId",p."ProductId" FROM inventory.cost_positions p WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId"
    LOOP PERFORM inventory.check_cost_position(NEW."TenantId",NEW."CompanyId",row."WarehouseId",row."ProductId"); END LOOP;
  END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER validate_cost_ledger_head AFTER INSERT OR UPDATE ON inventory.cost_ledger_heads DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_cost_ledger_head();

CREATE FUNCTION inventory.validate_stock_cost_entry() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE h inventory.cost_ledger_heads%ROWTYPE; valid_source boolean:=false; q numeric; v numeric; resolved_method text;
BEGIN
  SELECT * INTO h FROM inventory.cost_ledger_heads WHERE "TenantId"=NEW."TenantId" AND "CompanyId"=NEW."CompanyId";
  IF h."Sequence" IS NULL OR NEW."Sequence"<1 OR NEW."Sequence">h."Sequence" OR NEW."BusinessDate"<h."OpeningDate" THEN RAISE EXCEPTION 'Cost entry outside approved ledger bounds'; END IF;
  SELECT p."Method" INTO resolved_method FROM inventory.costing_policies p WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId" AND p."State"='Approved'
    AND (p."ProductId" IS NULL OR p."ProductId"=NEW."ProductId") AND p."EffectiveFrom"<=NEW."BusinessDate" AND NEW."BusinessDate"<p."EffectiveUntil"
    ORDER BY (p."ProductId" IS NOT NULL) DESC LIMIT 1;
  IF resolved_method IS DISTINCT FROM NEW."Method" THEN RAISE EXCEPTION 'Cost entry requires its effective approved policy method'; END IF;
  IF jsonb_typeof(NEW."Allocations")<>'array' OR jsonb_array_length(NEW."Allocations")=0 THEN RAISE EXCEPTION 'Cost allocation evidence required'; END IF;
  SELECT sum((x->>'Quantity')::numeric),sum((x->>'Value')::numeric) INTO q,v FROM jsonb_array_elements(NEW."Allocations") x;
  IF q IS DISTINCT FROM abs(NEW."Quantity") OR v IS DISTINCT FROM abs(NEW."Value") OR EXISTS(SELECT 1 FROM jsonb_array_elements(NEW."Allocations") x
    WHERE coalesce((x->>'Quantity')::numeric,0)<=0 OR x->>'Value' IS NULL OR (x->>'Value')::numeric<0 OR x->>'Id' IS NULL OR x->>'Origin' IS NULL
      OR round((x->>'Quantity')::numeric,6)<>(x->>'Quantity')::numeric OR round((x->>'Value')::numeric,4)<>(x->>'Value')::numeric) THEN
    RAISE EXCEPTION 'Cost allocation amounts do not reconcile';
  END IF;
  IF NEW."Kind"='Opening' THEN
    SELECT EXISTS(SELECT 1 FROM inventory.cost_adoptions a CROSS JOIN LATERAL jsonb_array_elements(a."Lines") x
      WHERE a."TenantId"=NEW."TenantId" AND a."CompanyId"=NEW."CompanyId" AND a."Id"=NEW."SourceId" AND a."Id"=h."AdoptionId" AND a."State"='Approved'
        AND (x->>'WarehouseId')::uuid=NEW."WarehouseId" AND (x->>'ProductId')::uuid=NEW."ProductId" AND (x->>'Quantity')::numeric=NEW."Quantity"
        AND (x->>'Value')::numeric=NEW."Value" AND x->>'Method'=NEW."Method" AND a."BusinessDate"=NEW."BusinessDate") INTO valid_source;
  ELSIF NEW."Kind"='Receipt' THEN
    SELECT EXISTS(SELECT 1 FROM inventory.purchase_cost_receipts r WHERE r."TenantId"=NEW."TenantId" AND r."CompanyId"=NEW."CompanyId" AND r."SourceMovementId"=NEW."SourceId"
      AND r."WarehouseId"=NEW."WarehouseId" AND r."ProductId"=NEW."ProductId" AND r."Quantity"=NEW."Quantity" AND r."BusinessDate"=NEW."BusinessDate"
      AND r."Method"=NEW."Method" AND r."Currency"='BDT' AND NEW."Value"=r."NetAmount" + CASE WHEN h."Basis"='PurchaseNetOnly' THEN 0 ELSE r."TaxAmount"-r."RecoverableTaxAmount" END
      + CASE WHEN h."Basis"='NetPlusNonrecoverableTaxAndLanded' THEN (SELECT (x->>'Amount')::numeric FROM inventory.cost_landed_reviews l CROSS JOIN LATERAL jsonb_array_elements(l."Lines") x
        WHERE l."TenantId"=r."TenantId" AND l."CompanyId"=r."CompanyId" AND l."PlanId"=r."PlanId" AND l."State"='Approved' AND (x->>'PurchaseLineId')::uuid=r."PurchaseLineId") ELSE 0 END) INTO valid_source;
  ELSIF NEW."Kind" IN ('Issue','Return') THEN
    SELECT EXISTS(SELECT 1 FROM sales.posting_entries s WHERE s."TenantId"=NEW."TenantId" AND s."CompanyId"=NEW."CompanyId" AND s."Id"=NEW."SourceId"
      AND s."WarehouseId"=NEW."WarehouseId" AND s."ProductId"=NEW."ProductId" AND s."Quantity"=NEW."Quantity" AND s."BusinessDate"=NEW."BusinessDate"
      AND ((NEW."Kind"='Issue' AND s."CreditNoteId" IS NULL AND s."DebitNoteId" IS NULL) OR (NEW."Kind"='Return' AND s."ReversesEntryId"=NEW."OriginalSourceId"))) INTO valid_source;
    IF NEW."Kind"='Return' AND NOT EXISTS(SELECT 1 FROM inventory.cost_entries i WHERE i."TenantId"=NEW."TenantId" AND i."CompanyId"=NEW."CompanyId" AND i."Kind"='Issue' AND i."SourceId"=NEW."OriginalSourceId"
      AND i."WarehouseId"=NEW."WarehouseId" AND i."ProductId"=NEW."ProductId" AND -i."Quantity">=(SELECT sum(r."Quantity") FROM inventory.cost_entries r WHERE r."TenantId"=i."TenantId" AND r."CompanyId"=i."CompanyId" AND r."Kind"='Return' AND r."OriginalSourceId"=i."SourceId")
      AND -i."Value">=(SELECT sum(r."Value") FROM inventory.cost_entries r WHERE r."TenantId"=i."TenantId" AND r."CompanyId"=i."CompanyId" AND r."Kind"='Return' AND r."OriginalSourceId"=i."SourceId")) THEN RAISE EXCEPTION 'Return cost exceeds original issue'; END IF;
  ELSIF NEW."Kind"='Reversal' THEN
    SELECT EXISTS(SELECT 1 FROM inventory.stock_movements m JOIN inventory.cost_entries r ON r."TenantId"=m."TenantId" AND r."CompanyId"=m."CompanyId" AND r."SourceId"=m."ReversesEntryId" AND r."Kind"='Receipt'
      WHERE m."TenantId"=NEW."TenantId" AND m."CompanyId"=NEW."CompanyId" AND m."Id"=NEW."SourceId" AND m."ReversesEntryId"=NEW."OriginalSourceId"
        AND m."WarehouseId"=NEW."WarehouseId" AND m."ProductId"=NEW."ProductId" AND m."Quantity"=NEW."Quantity" AND m."BusinessDate"=NEW."BusinessDate"
        AND NEW."Quantity"=-r."Quantity" AND NEW."Value"=-r."Value") INTO valid_source;
  ELSE
    SELECT EXISTS(SELECT 1 FROM inventory.transfer_journal t WHERE t."TenantId"=NEW."TenantId" AND t."CompanyId"=NEW."CompanyId" AND t."Id"=NEW."SourceId" AND t."ProductId"=NEW."ProductId" AND t."BusinessDate"=NEW."BusinessDate"
      AND ((NEW."Kind"='TransferOut' AND t."FromWarehouseId"=NEW."WarehouseId" AND -t."Quantity"=NEW."Quantity") OR (NEW."Kind"='TransferIn' AND t."ToWarehouseId"=NEW."WarehouseId" AND t."Quantity"=NEW."Quantity"))) INTO valid_source;
    IF (SELECT count(*)<>2 OR sum(e."Quantity")<>0 OR sum(e."Value")<>0 FROM inventory.cost_entries e WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId" AND e."SourceId"=NEW."SourceId" AND e."Kind" IN ('TransferOut','TransferIn')) THEN RAISE EXCEPTION 'Transfer cost legs do not balance'; END IF;
  END IF;
  IF NOT valid_source THEN RAISE EXCEPTION 'Cost entry does not match approved source evidence'; END IF;
  PERFORM inventory.check_cost_position(NEW."TenantId",NEW."CompanyId",NEW."WarehouseId",NEW."ProductId");
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER validate_stock_cost_entry AFTER INSERT ON inventory.cost_entries DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_stock_cost_entry();

CREATE FUNCTION inventory.require_source_cost_coverage() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE kind text;
BEGIN
  IF NOT EXISTS(SELECT 1 FROM inventory.cost_ledger_heads h WHERE h."TenantId"=NEW."TenantId" AND h."CompanyId"=NEW."CompanyId") THEN RETURN NULL; END IF;
  IF TG_TABLE_NAME='stock_movements' THEN kind:=CASE WHEN NEW."Quantity">0 THEN 'Receipt' ELSE 'Reversal' END;
  ELSIF TG_TABLE_SCHEMA='sales' THEN
    IF NEW."Quantity"=0 THEN RETURN NULL; END IF;
    kind:=CASE WHEN NEW."Quantity"<0 THEN 'Issue' ELSE 'Return' END;
  ELSE kind:='TransferOut'; END IF;
  IF NOT EXISTS(SELECT 1 FROM inventory.cost_entries e WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId" AND e."SourceId"=NEW."Id" AND e."Kind"=kind) THEN
    RAISE EXCEPTION 'Active stock movement requires atomic cost evidence';
  END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER require_purchase_cost_coverage AFTER INSERT ON inventory.stock_movements DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.require_source_cost_coverage();
CREATE CONSTRAINT TRIGGER require_sales_cost_coverage AFTER INSERT ON sales.posting_entries DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.require_source_cost_coverage();
CREATE CONSTRAINT TRIGGER require_transfer_cost_coverage AFTER INSERT ON inventory.transfer_journal DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.require_source_cost_coverage();
