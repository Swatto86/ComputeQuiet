import { strict as assert } from "node:assert";
import { test } from "node:test";

import type { Recommendation } from "./bridge.ts";
import {
  defaultSelection,
  kindLabel,
  selectedItems,
  summarize,
} from "./scan-select.ts";

function item(
  name: string,
  risk: "low" | "medium",
  targeted = false,
  memory = 100,
): Recommendation {
  return {
    kind: { kind: "process", action: "suspend" },
    name,
    reason: "test",
    risk,
    memory_bytes: memory,
    cpu_percent: 0,
    instances: 1,
    already_targeted: targeted,
  };
}

test("low-risk finds start ticked, medium and already-targeted do not", () => {
  const items = [
    item("OneDrive", "low"),
    item("Discord", "medium"),
    item("Slack", "low", true),
  ];
  assert.deepEqual([...defaultSelection(items)], [0]);
  const picked = selectedItems(items, new Set([0, 1, 2]));
  assert.deepEqual(
    picked.map((p) => p.name),
    ["OneDrive", "Discord"],
    "an already-targeted row cannot be applied even if ticked",
  );
});

test("row labels name the action and the summary counts only what can be added", () => {
  assert.equal(
    kindLabel({ kind: "process", action: "close" }),
    "Close & relaunch",
  );
  assert.equal(kindLabel({ kind: "process", action: "suspend" }), "Suspend");
  assert.equal(kindLabel({ kind: "service" }), "Stop service");
  assert.equal(kindLabel({ kind: "power_plan" }), "Power plan");
  assert.equal(kindLabel({ kind: "memory_purge" }), "Purge cache");

  const summary = summarize([
    item("A", "low", false, 10),
    item("B", "medium", false, 20),
    item("C", "low", true, 40),
    {
      ...item("Cached memory", "low", false, 999),
      kind: { kind: "memory_purge" },
    },
  ]);
  assert.deepEqual(summary, {
    total: 4,
    selectable: 3,
    low: 2,
    medium: 1,
    targeted: 1,
    memoryBytes: 30,
  });
});
