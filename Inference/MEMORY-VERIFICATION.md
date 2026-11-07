# Persistent inference thread resource check

The previous ThreadingHTTPServer created a fresh request thread even though Unity allows only one request at a time. After the long integrated run, its process was observed at 3,612,037,120 bytes RSS and 103,650,226,176 bytes private memory. That is a resource failure; native per-thread Torch allocator retention was the working diagnosis, not a separately instrumented allocator proof.

The minimal fix uses standard HTTPServer so all predictions execute on one persistent request OS thread. Model weights, prompts, inference thresholds and response contracts are unchanged. Health waits behind the current inference; the project lifecycle shutdown uses a separate shutdown thread as required by Python's server API.

After scoped shutdown/restart, a coordinated idle-window test sent 200 sequential requests containing the actual lit holdout PNG `00-plastic_bottle_gallon-2.5m-45deg.png`. All 200 returned identical PNG SHA256 and exact numeric detections. Total elapsed time was 26.32 seconds.

| Requests | RSS bytes | Private bytes | Process threads |
|---:|---:|---:|---:|
| 0 | 819,384,320 | 1,511,460,864 | 45 |
| 25 | 823,271,424 | 1,491,542,016 | 45 |
| 200 | 825,933,824 | 1,494,867,968 | 45 |

Maximum sampled RSS was 836,747,264 bytes. There was no monotonic growth across the 25-request samples. A final health request with a one-second timeout passed in22ms. This bounded test supports the fix but is not an unlimited-duration memory guarantee. Raw timings and samples: `memory-benchmark.json`; reproducer: `benchmark_memory.py`. Run only when Unity/player inference is idle, since this test uses the live service.
