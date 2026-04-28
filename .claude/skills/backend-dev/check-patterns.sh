#!/bin/bash
set -e
ROOT="${1:-src}"

echo "Checking entities inherit TenantEntity..."
# any class extending DbContext'd entity that doesn't reference TenantEntity? best-effort heuristic
if grep -rn "public class .* : .*Entity" "$ROOT" --include="*.cs" \
   | grep -v "TenantEntity" | grep -v "BaseEntity"; then
  echo "FAIL: entity not inheriting TenantEntity"
  exit 1
fi

echo "Checking for raw HttpClient between services..."
if grep -rn "new HttpClient" "$ROOT" --include="*.cs" \
   | grep -v "LMS.Gateway" | grep -v "ILlmClient"; then
  echo "FAIL: direct HttpClient — use MassTransit events"
  exit 1
fi

echo "Checking for inline event records (must live in LMS.Contracts)..."
if grep -rn "public record .*Event" "$ROOT" --include="*.cs" \
   | grep -v "LMS.Contracts/"; then
  echo "FAIL: event record outside LMS.Contracts"
  exit 1
fi

echo "Checking for raw throw new Exception..."
if grep -rn "throw new Exception(" "$ROOT" --include="*.cs"; then
  echo "FAIL: use DomainException or Result<T>"
  exit 1
fi

echo "Checking for direct Anthropic SDK usage..."
if grep -rn "using Anthropic" "$ROOT" --include="*.cs" \
   | grep -v "ILlmClient implementation"; then
  echo "FAIL: route LLM calls through ILlmClient"
  exit 1
fi

echo "All backend pattern checks passed."
