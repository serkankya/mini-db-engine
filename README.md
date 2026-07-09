# dotnet-kv-store

A minimalistic, strictly disk-based B+ Tree Key-Value storage engine written from scratch in pure C# (.NET 10).

I built this project to deeply understand the low-level mechanics of database storage engines—bypassing ORMs and standard database drivers to work directly with disk I/O, binary page formatting, B-Tree mathematics, and crash recovery systems.

## Core Features

* **Custom Binary File Format:** Stores data in fixed-size (4096-byte) disk pages using a strict Slotted Page architecture.
* **B+ Tree Implementation:** Supports `O(log N)` operations for Insert, Get, and Delete, alongside highly efficient Range Scans utilizing leaf-node linked lists (`RightPointer`).
* **Write-Ahead Log (WAL):** Ensures ACID-like durability. Operations are sequentially appended to a `.wal` file before modifying the complex B-Tree pages.
* **Zero-Dependency Core:** Built using raw `FileStream`, `ReadOnlySpan<byte>`, and `BinaryPrimitives`.

## Architecture & How It Works

### 1. Slotted Page Layout (4KB)
Every node in the B-Tree maps to a physical 4KB page on the disk. To prevent fragmentation and allow `O(log N)` binary search within the page itself, the engine uses a Slotted Page design: pointers grow from the front, and raw data grows from the back.

```text
+----------------------------------------------------------------+
| Header (17B) | Slot 0 | Slot 1 | ...           ---> Free Space |
+----------------------------------------------------------------+
| Free Space <---      ... | Cell 1 (Key/Val) | Cell 0 (Key/Val) |
+----------------------------------------------------------------+