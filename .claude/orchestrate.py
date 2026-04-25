#!/usr/bin/env python3
"""Spawn parallel Claude subagents from .claude/task-graph.json."""
import subprocess, json, time, sys
from pathlib import Path

plan = json.loads(Path(".claude/task-graph.json").read_text())
tasks = {t["id"]: t for t in plan["tasks"]}
completed, running, failed = set(), {}, set()

SKILL_MAP = {
    "backend":       "backend-dev",
    "frontend":      "frontend-dev",
    "reviewer":      "code-reviewer",
    "haiku-helper":  "backend-dev",
}

MODEL_MAP = {
    "backend":       "claude-sonnet-4-6",
    "frontend":      "claude-sonnet-4-6",
    "reviewer":      "claude-sonnet-4-6",
    "haiku-helper":  "claude-haiku-4-5",
}


def ready():
    return [t for t in tasks.values()
            if t["id"] not in completed
            and t["id"] not in running
            and t["id"] not in failed
            and all(d in completed for d in t["depends_on"])]


def build_prompt(task):
    contracts = Path(".claude/contracts.md").read_text()
    return f"""Task: {task['title']}

Spec:
{task['spec']}

Interface contracts (read-only — do not deviate):
{contracts}

Files you own: {', '.join(task.get('files', []))}
"""


def spawn(task):
    print(f"[+] Starting {task['id']}: {task['title']}")
    subprocess.run(
        ["git", "worktree", "add", task["worktree"], "-b", task["branch"]],
        check=True,
    )
    log = open(f"{task['worktree']}/agent.log", "w")
    proc = subprocess.Popen(
        [
            "claude",
            "--model", MODEL_MAP.get(task["agent"], "claude-sonnet-4-6"),
            "--skill", SKILL_MAP.get(task["agent"], "backend-dev"),
            "-p", build_prompt(task),
        ],
        cwd=task["worktree"],
        stdout=log,
        stderr=subprocess.STDOUT,
    )
    running[task["id"]] = proc


while len(completed) + len(failed) < len(tasks):
    for task in ready():
        spawn(task)

    for tid, proc in list(running.items()):
        ret = proc.poll()
        if ret is not None:
            if ret == 0:
                print(f"[OK] {tid} complete")
                completed.add(tid)
            else:
                print(f"[FAIL] {tid} exit {ret}")
                failed.add(tid)
            del running[tid]

    time.sleep(5)

print(f"\nDone. {len(completed)} succeeded, {len(failed)} failed.")
if failed:
    print(f"Failed: {failed}")
    sys.exit(1)
