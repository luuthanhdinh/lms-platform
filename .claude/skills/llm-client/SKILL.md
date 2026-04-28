---
name: llm-client
description: >
  ILlmClient abstraction usage: prompt caching, model pinning,
  retries, token-budget guard, per-tenant quota.
allowed-tools: Read, Write, Edit, Bash
user-invocable: false
---

## Rules

- All LLM calls go through `ILlmClient`. NO `using Anthropic;` outside
  `LMS.Infrastructure.Llm.AnthropicLlmClient`
- Model name comes from `LlmOptions` config, not hardcoded
- Default model: `claude-sonnet-4-6`. Use `claude-haiku-4-5` for
  classification/cheap tasks. Reserve `claude-opus-4-7` for
  generation that needs strong reasoning (assessment grading rubrics)

## Prompt caching

- System prompt + few-shot examples → `cache_control: { type: "ephemeral" }`
  on the trailing block of the static prefix
- Cache hit rate is an SLO; surface it in logs and OTEL metric
  `llm.cache.read_input_tokens`

## Retries + budget

- Retry only on 429, 500, 503 with exponential backoff (max 3)
- Per-call `max_tokens` set per use case in config; never unbounded
- Per-tenant monthly token quota enforced via Redis counter:
  `t:{tenantId}:llm:tokens:{yyyyMM}` — over quota → 429 to caller

## Logging

- Log `model`, `input_tokens`, `output_tokens`, `cache_read`,
  `cache_creation` at info
- Log full prompts at DEBUG only, with PII redacted
- Never log API keys
