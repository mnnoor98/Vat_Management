CREATE FUNCTION inventory.production_uom_stock_quantity(source jsonb, product uuid, input_quantity numeric, input_unit text) RETURNS numeric LANGUAGE plpgsql AS $$
DECLARE stock_unit text; factor numeric; result numeric; matches integer;
BEGIN
  IF product=(source->'evidence'->'finishedProduct'->>'id')::uuid THEN stock_unit:=source->'evidence'->'finishedProduct'->>'unitOfMeasure';
  ELSE SELECT x->'product'->>'unitOfMeasure' INTO stock_unit FROM jsonb_array_elements(source->'evidence'->'materials') x WHERE (x->'product'->>'id')::uuid=product; END IF;
  IF stock_unit IS NULL OR input_quantity<=0 OR input_quantity>=1000000000000 OR round(input_quantity,6)<>input_quantity OR input_unit IS NULL OR length(input_unit) NOT BETWEEN 1 AND 20 OR input_unit<>upper(btrim(input_unit)) THEN RAISE EXCEPTION 'Invalid production input UOM evidence'; END IF;
  IF input_unit=upper(btrim(stock_unit)) THEN factor:=1;
  ELSE
    SELECT count(*),min((x->>'stockUnitsPerInputUnit')::numeric) INTO matches,factor FROM jsonb_array_elements(coalesce(source->'evidence'->'uomConversions','[]'::jsonb)) x WHERE (x->>'productId')::uuid=product AND x->>'inputUnitOfMeasure'=input_unit;
    IF matches<>1 OR factor<=0 OR factor>=1000000000000 OR round(factor,12)<>factor THEN RAISE EXCEPTION 'Production input UOM requires one frozen approved conversion'; END IF;
  END IF;
  result:=input_quantity*factor;
  IF result<=0 OR result>=1000000000000 OR round(result,6)<>result THEN RAISE EXCEPTION 'Production UOM conversion cannot round or overflow stock quantity'; END IF;
  RETURN result;
END $$;

CREATE FUNCTION inventory.validate_production_uom() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE source jsonb; line jsonb; qty numeric; unit text;
BEGIN
  SELECT "SourceJson" INTO source FROM inventory.production_cost_sessions WHERE "TenantId"=NEW."TenantId" AND "CompanyId"=NEW."CompanyId" AND "Id"=NEW."SessionId";
  FOR line IN SELECT x FROM jsonb_array_elements(NEW."Lines") x LOOP
    qty:=(line->>'inputQuantity')::numeric;unit:=line->>'inputUnitOfMeasure';
    IF (qty IS NULL)<>(unit IS NULL) OR (unit IS NOT NULL AND inventory.production_uom_stock_quantity(source,(line->>'productId')::uuid,qty,unit) IS DISTINCT FROM (line->>'quantity')::numeric) THEN RAISE EXCEPTION 'Production input/stock material quantity does not reconcile'; END IF;
  END LOOP;
  IF (NEW."OutputInputQuantity" IS NULL)<>(NEW."OutputUnitOfMeasure" IS NULL) OR (NEW."OutputUnitOfMeasure" IS NOT NULL AND (NEW."Kind"<>'Receipt' OR inventory.production_uom_stock_quantity(source,(source->'evidence'->'finishedProduct'->>'id')::uuid,NEW."OutputInputQuantity",NEW."OutputUnitOfMeasure") IS DISTINCT FROM NEW."OutputQuantity")) THEN RAISE EXCEPTION 'Production input/stock output quantity does not reconcile'; END IF;
  RETURN NEW;
END $$;
CREATE TRIGGER validate_production_uom BEFORE INSERT OR UPDATE ON inventory.production_cost_operations FOR EACH ROW EXECUTE FUNCTION inventory.validate_production_uom();
