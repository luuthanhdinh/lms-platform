#!/usr/bin/env python3
"""
Spawn parallel Claude subagents from .claude/task-graph.json.

Improvements over template:
- Per-task timeout (kills agent that overruns)
- Single retry on transient failure (exit code in RETRYABLE)
- Contract hash lock: SHA-256 of contracts.md captured at start;
  any task that mutates it fails the run
- Worktree pre-flight: skip `git worktree add` if already exists
- Timing report + machine-readable run summary
- Cascade-skip: tasks whose deps failed are marked SKIPPED, not stuck
"""
from __future__ import annotations
import hashlib, json, os, subprocess, sys, time
from pathlib import Path
from datetime import datetime, timezone

CLAUDE_DIR = Path(".claude")
GRAPH = CLAUDE_DIR / "task-graph.json"
CONTRACTS = CLAUDE_DIR / "contracts.md"
SUMMARY = CLAUDE_DIR / "run-summary.json"

RETRYABLE = {124, 137, 143}  # timeout / sigkill / sigterm
DEFAULT_TIMEOUT_MIN = 30

SKILL_MAP = {
    "backend":          "backend-dev",
    "frontend":         "frontend-dev",
    "reviewer":         "code-reviewer",
    "haiku-helper":     "backend-dev",
    "db-migrator":      "dotnet-ef-migrations",
    "events-architect": "masstransit-events",
    "tester":           "xunit-integration",
    "security-auditor": "tenant-isolation",
    "docs-writer":      "task-planner",
    "gateway-ops":      "yarp-gateway",
}

MODEL_MAP = {
    "backend":          "claude-sonnet-4-6",
    "frontend":         "claude-sonnet-4-6",
    "reviewer":         "claude-sonnet-4-6",
    "haiku-helper":     "claude-haiku-4-5",
    "db-migrator":      "claude-sonnet-4-6",
    "events-architect": "claude-sonnet-4-6",
    "tester":           "claude-sonnet-4-6",
    "security-auditor": "claude-sonnet-4-6",
    "docs-writer":      "claude-haiku-4-5",
    "gateway-ops":      "claude-sonnet-4-6",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def load_graph():
    plan = json.loads(GRAPH.read_text())
    return plan, {t["id"]: t for t in plan["tasks"]}


def build_prompt(task: dict, contracts: str) -> str:
    return f"""Task: {task['title']}

Spec:
{task['spec']}

Interface contracts (LOCKED — do not modify .claude/contracts.md):
{contracts}

Files you own:
{chr(10).join('  - ' + f for f in task.get('files', []))}

Acceptance commands (must pass before writing DONE.md):
{chr(10).join('  $ ' + c for c in task.get('acceptance', []))}
"""


def spawn(task: dict, contracts_text: str, attempt: int) -> tuple:
    wt = Path(task["worktree"])
    if not wt.exists():
        subprocess.run(
            ["git", "worktree", "add", str(wt), "-b", task["branch"]],
            check=True,
        )
    log_path = wt / f"agent.attempt{attempt}.log"
    log_fh = log_path.open("w")
    proc = subprocess.Popen(
        [
            "claude",
            "--model", MODEL_MAP.get(task["agent"], "claude-sonnet-4-6"),
            "--skill", SKILL_MAP.get(task["agent"], "backend-dev"),
            "-p", build_prompt(task, contracts_text),
        ],
        cwd=str(wt),
        stdout=log_fh,
        stderr=subprocess.STDOUT,
    )
    log_fh.close()
    deadline = time.time() + 60 * task.get("timeout_minutes", DEFAULT_TIMEOUT_MIN)
    return proc, deadline, log_path


def main() -> int:
    if not GRAPH.exists() or not CONTRACTS.exists():
        print("Missing .claude/task-graph.json or .claude/contracts.md")
        return 2

    plan, tasks = load_graph()
    contracts_text = CONTRACTS.read_text()
    contracts_hash = sha256(CONTRACTS)
    print(f"Contract lock: {contracts_hash[:12]}")

    completed: dict[str, float] = {}
    failed: dict[str, str] = {}
    skipped: set[str] = set()
    running: dict[str, dict] = {}
    attempts: dict[str, int] = {}
    started: dict[str, float] = {}

    def deps_satisfied(t):
        return all(d in completed for d in t["depends_on"])

    def deps_failed(t):
        return any(d in failed or d in skipped for d in t["depends_on"])

    def ready_tasks():
        return [
            t for t in tasks.values()
            if t["id"] not in completed
            and t["id"] not in failed
            and t["id"] not in skipped
            and t["id"] not in running
            and deps_satisfied(t)
        ]

    t0 = time.time()
    while len(completed) + len(failed) + len(skipped) < len(tasks):
        # cascade-skip
        for t in tasks.values():
            if t["id"] in completed or t["id"] in failed \
               or t["id"] in skipped or t["id"] in running:
                continue
            if deps_failed(t):
                print(f"[SKIP] {t['id']} (upstream failed)")
                skipped.add(t["id"])

        for t in ready_tasks():
            attempts[t["id"]] = 1
            print(f"[+] {t['id']} {t['title']} (agent={t['agent']})")
            proc, deadline, log = spawn(t, contracts_text, 1)
            running[t["id"]] = {"task": t, "proc": proc,
                                "deadline": deadline, "log": log}
            started[t["id"]] = time.time()

        for tid, info in list(running.items()):
            proc = info["proc"]
            ret = proc.poll()
            if ret is None and time.time() > info["deadline"]:
                proc.kill()
                ret = 124  # timeout
            if ret is None:
                continue

            t = info["task"]
            elapsed = time.time() - started[tid]
            if ret == 0 and sha256(CONTRACTS) != contracts_hash:
                print(f"[FAIL] {tid} mutated contracts.md — failing")
                failed[tid] = "contract-mutation"
                del running[tid]
                continue
            if ret == 0:
                print(f"[OK]   {tid} ({elapsed:.0f}s)")
                completed[tid] = elapsed
                del running[tid]
                continue

            if ret in RETRYABLE and attempts[tid] < 2:
                attempts[tid] += 1
                print(f"[RETRY] {tid} (exit {ret}, attempt 2)")
                proc2, dl2, log2 = spawn(t, contracts_text, 2)
                running[tid] = {"task": t, "proc": proc2,
                                "deadline": dl2, "log": log2}
                started[tid] = time.time()
            else:
                print(f"[FAIL] {tid} (exit {ret}, log {info['log']})")
                failed[tid] = f"exit-{ret}"
                del running[tid]

        if not running and not ready_tasks():
            unresolvable = [
                tid for tid, t in tasks.items()
                if tid not in completed and tid not in failed and tid not in skipped
            ]
            if unresolvable:
                print(f"[DEADLOCK] Tasks with unsatisfiable deps: {unresolvable}")
                for tid in unresolvable:
                    failed[tid] = "deadlock"
                break

        time.sleep(3)

    total = time.time() - t0
    SUMMARY.write_text(json.dumps({
        "completed_at": datetime.now(timezone.utc).isoformat(),
        "duration_seconds": round(total, 1),
        "completed": completed,
        "failed": failed,
        "skipped": sorted(skipped),
        "contract_hash": contracts_hash,
    }, indent=2))

    print(f"\n=== Run complete in {total:.0f}s ===")
    print(f"  ok:      {len(completed)}")
    print(f"  failed:  {len(failed)}  {list(failed)}")
    print(f"  skipped: {len(skipped)} {sorted(skipped)}")
    print(f"  summary: {SUMMARY}")
    return 1 if failed or skipped else 0


if __name__ == "__main__":
    sys.exit(main())
