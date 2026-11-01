CREATE FUNCTION inventory.freeze_production_session() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP='INSERT' THEN
    IF NEW."Version"<>1 OR NEW."State"<>'Open' OR NEW."WipValue"<>0 OR NEW."ReceivedQuantity"<>0 OR
      (NEW."SourceJson"->>'tenantId')::uuid IS DISTINCT FROM NEW."TenantId" OR (NEW."SourceJson"->>'companyId')::uuid IS DISTINCT FROM NEW."CompanyId" OR (NEW."SourceJson"->>'orderId')::uuid IS DISTINCT FROM NEW."OrderId" OR
      (NEW."SourceJson"->>'state') IS DISTINCT FROM 'Approved' OR (NEW."SourceJson"->>'version')::integer IS DISTINCT FROM 3 OR
      NOT EXISTS(SELECT 1 FROM inventory.cost_ledger_heads h WHERE h."TenantId"=NEW."TenantId" AND h."CompanyId"=NEW."CompanyId") THEN RAISE EXCEPTION 'Production requires an approved scoped source and active cost ledger'; END IF;
  ELSIF OLD."State"<>'Open' OR NEW."Version"<>OLD."Version"+1 OR
    (to_jsonb(NEW)-ARRAY['State','Version','WipValue','ReceivedQuantity']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','WipValue','ReceivedQuantity']) THEN
    RAISE EXCEPTION 'Only sequenced execution changes are permitted; approved source is frozen';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER freeze_production_session BEFORE INSERT OR UPDATE ON inventory.production_cost_sessions FOR EACH ROW EXECUTE FUNCTION inventory.freeze_production_session();
CREATE FUNCTION inventory.freeze_production_operation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP='INSERT' THEN
    IF NEW."State"<>'Draft' THEN RAISE EXCEPTION 'Production operation must start as draft'; END IF;
    IF jsonb_typeof(NEW."Lines")<>'array' OR jsonb_array_length(NEW."Lines")>100 OR
      (NEW."Kind" IN ('Issue','Return') AND (jsonb_array_length(NEW."Lines")=0 OR NEW."Amount"<>0 OR NEW."OutputQuantity"<>0 OR NEW."Labor"+NEW."Utility"+NEW."Overhead"<>0)) OR
      (NEW."Kind" NOT IN ('Issue','Return') AND jsonb_array_length(NEW."Lines")<>0) OR
      (NEW."Kind"='Conversion' AND (NEW."Amount"<>0 OR NEW."OutputQuantity"<>0 OR NEW."Labor"+NEW."Utility"+NEW."Overhead"<=0)) OR
      (NEW."Kind"='Receipt' AND (NEW."OutputQuantity"<=0 OR NEW."Labor"+NEW."Utility"+NEW."Overhead"<>0)) OR
      (NEW."Kind"='Loss' AND (NEW."Amount"<=0 OR NEW."OutputQuantity"<>0 OR NEW."Labor"+NEW."Utility"+NEW."Overhead"<>0)) OR
      (NEW."Kind"='Close' AND (NEW."Amount"<>0 OR NEW."OutputQuantity"<>0 OR NEW."Labor"+NEW."Utility"+NEW."Overhead"<>0)) THEN RAISE EXCEPTION 'Invalid production operation source shape'; END IF;
  ELSIF OLD."State"<>'Draft' OR NEW."State" NOT IN ('Posted','Cancelled') OR
    (to_jsonb(NEW)-ARRAY['State','Version','ApprovedBy','PostedAt','PostedValue']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version','ApprovedBy','PostedAt','PostedValue']) THEN
    RAISE EXCEPTION 'Only approval or cancellation of an unchanged production draft is permitted';
  END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER freeze_production_operation BEFORE INSERT OR UPDATE ON inventory.production_cost_operations FOR EACH ROW EXECUTE FUNCTION inventory.freeze_production_operation();
CREATE TRIGGER immutable_production_session_delete BEFORE DELETE ON inventory.production_cost_sessions FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_production_session_truncate BEFORE TRUNCATE ON inventory.production_cost_sessions FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_production_operation_delete BEFORE DELETE ON inventory.production_cost_operations FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_production_operation_truncate BEFORE TRUNCATE ON inventory.production_cost_operations FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_production_stock BEFORE UPDATE OR DELETE ON inventory.production_stock_movements FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_production_stock_truncate BEFORE TRUNCATE ON inventory.production_stock_movements FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_production_wip BEFORE UPDATE OR DELETE ON inventory.production_wip_entries FOR EACH ROW EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();
CREATE TRIGGER immutable_production_wip_truncate BEFORE TRUNCATE ON inventory.production_wip_entries FOR EACH STATEMENT EXECUTE FUNCTION inventory.reject_costing_evidence_mutation();

CREATE FUNCTION inventory.check_production_session(t uuid,c uuid,s uuid) RETURNS void LANGUAGE plpgsql AS $$
DECLARE h inventory.production_cost_sessions%ROWTYPE; value numeric; qty numeric; n bigint; last_version integer;
BEGIN
  SELECT * INTO h FROM inventory.production_cost_sessions WHERE "TenantId"=t AND "CompanyId"=c AND "Id"=s;
  SELECT coalesce(sum(e."Value"),0),count(*),coalesce(max(e."SessionVersion"),1) INTO value,n,last_version FROM inventory.production_wip_entries e WHERE e."TenantId"=t AND e."CompanyId"=c AND e."SessionId"=s;
  SELECT coalesce(sum(m."Quantity"),0) INTO qty FROM inventory.production_stock_movements m WHERE m."TenantId"=t AND m."CompanyId"=c AND m."SessionId"=s AND m."Kind"='Receipt';
  IF h."WipValue"<>value OR h."ReceivedQuantity"<>qty OR h."Version"<>n+1 OR h."Version"<>last_version OR value<0 OR
    qty>(h."SourceJson"->'evidence'->>'plannedQuantity')::numeric OR (h."State"='Closed' AND value<>0) THEN RAISE EXCEPTION 'Production WIP/output/version does not reconcile'; END IF;
END $$;
CREATE FUNCTION inventory.validate_production_session() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  PERFORM inventory.check_production_session(NEW."TenantId",NEW."CompanyId",NEW."Id");RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER validate_production_session AFTER INSERT OR UPDATE ON inventory.production_cost_sessions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_production_session();

CREATE FUNCTION inventory.validate_production_wip_entry() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE o inventory.production_cost_operations%ROWTYPE; expected numeric; movement_value numeric; movement_count integer;
BEGIN
  SELECT * INTO o FROM inventory.production_cost_operations WHERE "TenantId"=NEW."TenantId" AND "CompanyId"=NEW."CompanyId" AND "Id"=NEW."OperationId";
  IF o."State"<>'Posted' OR o."SessionId"<>NEW."SessionId" OR o."Kind"<>NEW."Kind" OR o."BusinessDate"<>NEW."BusinessDate" OR NEW."SessionVersion"<>o."SessionVersion"+1 THEN RAISE EXCEPTION 'WIP entry must match its posted operation'; END IF;
  SELECT coalesce(sum(m."Value"),0),count(*) INTO movement_value,movement_count FROM inventory.production_stock_movements m WHERE m."TenantId"=NEW."TenantId" AND m."CompanyId"=NEW."CompanyId" AND m."OperationId"=NEW."OperationId";
  expected:=CASE WHEN o."Kind" IN ('Issue','Return','Receipt') THEN -movement_value WHEN o."Kind"='Conversion' THEN o."Labor"+o."Utility"+o."Overhead" WHEN o."Kind"='Loss' THEN -o."Amount" ELSE 0 END;
  IF NEW."Value"<>expected OR o."PostedValue"<>abs(expected) OR
    (o."Kind" IN ('Issue','Return') AND movement_count<>jsonb_array_length(o."Lines")) OR (o."Kind"='Receipt' AND movement_count<>1) OR
    (o."Kind" IN ('Conversion','Loss','Close') AND movement_count<>0) THEN RAISE EXCEPTION 'Production source/stock/WIP amounts do not conserve value'; END IF;
  IF o."Kind"='Close' AND (NOT EXISTS(SELECT 1 FROM inventory.production_cost_sessions h WHERE h."TenantId"=NEW."TenantId" AND h."CompanyId"=NEW."CompanyId" AND h."Id"=NEW."SessionId" AND h."State"='Closed') OR
    EXISTS(SELECT 1 FROM inventory.production_cost_operations p WHERE p."TenantId"=NEW."TenantId" AND p."CompanyId"=NEW."CompanyId" AND p."SessionId"=NEW."SessionId" AND p."State"='Draft')) THEN RAISE EXCEPTION 'Execution close requires no pending drafts'; END IF;
  PERFORM inventory.check_production_session(NEW."TenantId",NEW."CompanyId",NEW."SessionId");RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER validate_production_wip_entry AFTER INSERT ON inventory.production_wip_entries DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_production_wip_entry();

CREATE FUNCTION inventory.validate_production_stock_movement() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE o inventory.production_cost_operations%ROWTYPE; source jsonb;
BEGIN
  SELECT * INTO o FROM inventory.production_cost_operations WHERE "TenantId"=NEW."TenantId" AND "CompanyId"=NEW."CompanyId" AND "Id"=NEW."OperationId";
  SELECT h."SourceJson" INTO source FROM inventory.production_cost_sessions h WHERE h."TenantId"=NEW."TenantId" AND h."CompanyId"=NEW."CompanyId" AND h."Id"=NEW."SessionId";
  IF o."State"<>'Posted' OR o."SessionId"<>NEW."SessionId" OR o."Kind"<>NEW."Kind" OR o."BusinessDate"<>NEW."BusinessDate" OR NEW."BusinessDate"<(source->'evidence'->>'businessDate')::date THEN RAISE EXCEPTION 'Production stock requires a matching posted operation'; END IF;
  IF NEW."Kind" IN ('Issue','Return') THEN
    IF NOT EXISTS(SELECT 1 FROM jsonb_array_elements(o."Lines") x WHERE (x->>'id')::uuid=NEW."Id" AND (x->>'warehouseId')::uuid=NEW."WarehouseId" AND (x->>'productId')::uuid=NEW."ProductId"
      AND (x->>'quantity')::numeric=abs(NEW."Quantity") AND (x->>'originalIssueId')::uuid IS NOT DISTINCT FROM NEW."OriginalIssueId") OR
      NOT EXISTS(SELECT 1 FROM jsonb_array_elements(source->'evidence'->'materials') x WHERE (x->'product'->>'id')::uuid=NEW."ProductId") THEN RAISE EXCEPTION 'Material movement is outside approved order/source lines'; END IF;
    IF NEW."Kind"='Return' AND NOT EXISTS(SELECT 1 FROM inventory.production_stock_movements i WHERE i."TenantId"=NEW."TenantId" AND i."CompanyId"=NEW."CompanyId" AND i."SessionId"=NEW."SessionId" AND i."Id"=NEW."OriginalIssueId" AND i."Kind"='Issue'
      AND i."WarehouseId"=NEW."WarehouseId" AND i."ProductId"=NEW."ProductId" AND i."BusinessDate"<=NEW."BusinessDate"
      AND -i."Quantity">=(SELECT sum(r."Quantity") FROM inventory.production_stock_movements r WHERE r."TenantId"=i."TenantId" AND r."CompanyId"=i."CompanyId" AND r."OriginalIssueId"=i."Id" AND r."Kind"='Return')
      AND -i."Value">=(SELECT sum(r."Value") FROM inventory.production_stock_movements r WHERE r."TenantId"=i."TenantId" AND r."CompanyId"=i."CompanyId" AND r."OriginalIssueId"=i."Id" AND r."Kind"='Return')) THEN RAISE EXCEPTION 'Material return exceeds original issue'; END IF;
  ELSIF NEW."ProductId"<>(source->'evidence'->'finishedProduct'->>'id')::uuid OR NEW."WarehouseId"<>(source->'evidence'->>'warehouseId')::uuid OR NEW."Quantity"<>o."OutputQuantity" OR NEW."Value"<>o."Amount" THEN RAISE EXCEPTION 'Finished receipt must match its approved WIP allocation'; END IF;
  IF NOT EXISTS(SELECT 1 FROM inventory.cost_entries e WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId" AND e."SourceId"=NEW."Id" AND e."Kind"='Production'||NEW."Kind" AND e."Quantity"=NEW."Quantity" AND e."Value"=NEW."Value") THEN RAISE EXCEPTION 'Production stock requires atomic cost evidence'; END IF;
  PERFORM inventory.check_production_session(NEW."TenantId",NEW."CompanyId",NEW."SessionId");RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER validate_production_stock_movement AFTER INSERT ON inventory.production_stock_movements DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.validate_production_stock_movement();

CREATE FUNCTION inventory.require_production_operation_journal() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NEW."State"='Posted' AND NOT EXISTS(SELECT 1 FROM inventory.production_wip_entries e WHERE e."TenantId"=NEW."TenantId" AND e."CompanyId"=NEW."CompanyId" AND e."OperationId"=NEW."Id") THEN RAISE EXCEPTION 'Posted operation requires an immutable WIP entry'; END IF;
  RETURN NULL;
END $$;
CREATE CONSTRAINT TRIGGER require_production_operation_journal AFTER UPDATE ON inventory.production_cost_operations DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION inventory.require_production_operation_journal();
