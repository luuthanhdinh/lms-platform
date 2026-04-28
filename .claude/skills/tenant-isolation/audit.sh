#!/bin/bash
# Best-effort static audit for cross-tenant leakage patterns.
ROOT="${1:-src}"
FAIL=0

echo "[1/6] IgnoreQueryFilters without justification..."
grep -rn "IgnoreQueryFilters" "$ROOT" --include="*.cs" \
  | grep -v "// tenant-bypass:" && { echo "  FAIL"; FAIL=1; }

echo "[2/6] Raw SQL missing tenant_id..."
grep -rEn "FromSqlRaw|ExecuteSqlRaw|ExecuteSqlInterpolated" "$ROOT" \
  --include="*.cs" | while read -r line; do
    echo "$line" | grep -qi "tenant_id\|@tenant" || \
      { echo "  CHECK: $line"; FAIL=1; }
  done

echo "[3/6] DTOs setting TenantId from request body..."
grep -rEn "TenantId\s*=\s*(req|request|dto|input)\." "$ROOT" \
  --include="*.cs" && { echo "  FAIL: TenantId from request"; FAIL=1; }

echo "[4/6] Redis cache keys missing tenant prefix..."
grep -rEn "(StringSetAsync|HashSetAsync|KeyExistsAsync)\s*\(\s*\"" "$ROOT" \
  --include="*.cs" | grep -v "t:{" | grep -v "tenant" \
  && { echo "  CHECK: cache key without tenant prefix"; FAIL=1; }

echo "[5/6] Entities missing TenantEntity inheritance..."
grep -rEn "public class \w+\s*:\s*\w*Entity\b" "$ROOT" --include="*.cs" \
  | grep -v "TenantEntity" | grep -v "abstract class" \
  && { echo "  FAIL"; FAIL=1; }

echo "[6/6] DbContext without OnModelCreating tenant filter..."
for ctx in $(grep -rl "DbContext" "$ROOT" --include="*.cs" \
              | grep -v Migrations); do
  grep -q "HasQueryFilter" "$ctx" || \
    { echo "  CHECK: $ctx has no HasQueryFilter"; FAIL=1; }
done

[ $FAIL -eq 0 ] && echo "Tenant-isolation audit clean." \
                || { echo "Tenant-isolation audit found issues."; exit 1; }
