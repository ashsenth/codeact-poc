#!/usr/bin/env bash
# Wait until the whole bench run finishes (its "bench done" marker) or a cap, then dump the tail.
for i in $(seq 1 150); do
  grep -q 'bench done' /tmp/bench.log 2>/dev/null && break
  sleep 20
done
echo "=== tail bench.log ==="
tail -60 /tmp/bench.log
echo "=== newest comparison json per profile ==="
for p in small medium large; do ls -1t /root/codeact-poc/artifacts/comparisons/$p-*.json 2>/dev/null | head -1; done
