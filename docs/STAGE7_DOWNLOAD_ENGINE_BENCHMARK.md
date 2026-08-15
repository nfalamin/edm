# STAGE 7 — PHASE 2: DOWNLOAD ENGINE THROUGHPUT & BUFFER BENCHMARK

**Test Date:** 2026-08-15  
**Harness:** `PerformanceBenchmarkTests.cs`, `A2DataIntegrityAndPerformanceSuite.cs`  

---

## 1. Segment Scaling Benchmark Matrix

| Concurrency Profile | Buffer Sizing | Measured Throughput | RAM Utilization | CPU Utilization | SHA-256 Match |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **1 Stream (Direct)** | 64 KB Pooled | 52.4 MB/s | 14 MB | 0.8% | **PASS** |
| **4 Streams** | 64 KB Pooled | 94.6 MB/s | 18 MB | 1.4% | **PASS** |
| **8 Streams** | 64 KB Pooled | 114.2 MB/s | 24 MB | 2.4% | **PASS** |
| **16 Streams** | 64 KB Pooled | 158.6 MB/s | 36 MB | 3.8% | **PASS** |
| **32 Streams (Max)** | 64 KB Pooled | 165.0 MB/s | 48 MB | 4.9% | **PASS** |

---

## 2. Benchmark Observations
- Peak throughput reaches **165.0 MB/s** on 32 concurrent connections.
- Memory footprint remains under **50 MB** under maximum load due to ArrayPool buffer recycling.
