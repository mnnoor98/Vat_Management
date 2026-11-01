CREATE OR REPLACE FUNCTION inventory.validate_stock_cost_entry() RETURNS trigger LANGUAGE plpgsql AS $$
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
  IF NEW."Kind" IN ('ProductionIssue','ProductionReturn','ProductionReceipt','ProductionReceiptReversal') THEN
    SELECT EXISTS(SELECT 1 FROM inventory.production_stock_movements m WHERE m."TenantId"=NEW."TenantId" AND m."CompanyId"=NEW."CompanyId" AND m."Id"=NEW."SourceId"
      AND NEW."Kind"='Production'||m."Kind" AND m."WarehouseId"=NEW."WarehouseId" AND m."ProductId"=NEW."ProductId" AND m."BusinessDate"=NEW."BusinessDate" AND m."Quantity"=NEW."Quantity" AND m."Value"=NEW."Value"
      AND m."OriginalIssueId" IS NOT DISTINCT FROM NEW."OriginalSourceId") INTO valid_source;
  ELSIF NEW."Kind"='Opening' THEN
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
