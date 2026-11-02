# Chat Intake Gate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** แนบเอกสารรถที่หน้า quote → เด้งเข้าแชตทันที; ฟิลด์ที่ resolve ไม่ครบ ให้ **intake gate** ฝั่ง frontend ถามเก็บ (chip จาก facet จริง + พิมพ์เองได้) ก่อนค้นแผนแล้วส่งต่อให้ LLM

**Architecture:** LLM **ไม่มี tool ค้นหา** (ADR 0005 — frontend ค้น, AI กรอง `Candidates`) และถ้า `Candidates` ว่าง system prompt จะไล่ผู้ใช้กลับไปหน้าฟอร์ม. ดังนั้น gate ต้องทำงาน **ก่อน** LLM ถูกเรียก: `chatbot.ts` ถือ `intake` state, ถามทีละฟิลด์ (brand→model→year→province) โดย reuse แถว suggestion chips เดิม, คำตอบที่พิมพ์เองส่งไป `GET /Motor/ResolveVehicle` (reuse `VehicleResolver` — ไม่ duplicate fuzzy logic ลง TS). ครบ → `facet.search()` → `seedContext()` → LLM รับช่วง. ดู spec `2026-07-10-chat-intake-gate-design.md` + ADR 0019.

**Tech Stack:** ASP.NET Core net10 (xUnit) · Angular 21 signals (vitest, globals `describe/it/expect`)

**Repo layout (โค้ดอยู่คนละ repo กับแผน):**
- `c:\CTD\ChatBot\insurechat-api\` — backend (.NET) — git repo แยก
- `c:\CTD\ChatBot\insurechat-web\` — frontend (Angular) — git repo แยก
- `c:\CTD\ChatBot\InsureChat\` — แผน/spec/ADR (commit แล้ว)

> ทั้งสอง code repo อยู่บน branch `feature/document-intake` — ทำงานต่อบน branch นี้ (อย่าสร้าง/สลับ branch)

---

## File Structure

**Backend (`insurechat-api/`)**
- Modify `InsureChatBot.Api/Models/Chatbot/ChatbotModels.cs` — `Province` / `ProvinceLabel` ใน context
- Create `InsureChatBot.Api.Tests/Fakes/MotorFakes.cs` — fake ที่ใช้ร่วมกัน (เลิก duplicate)
- Modify `InsureChatBot.Api.Tests/MotorControllerExtractDocumentTests.cs` — ใช้ fake ร่วม
- Modify `InsureChatBot.Api/Controllers/MotorController.cs` — `GET ResolveVehicle`
- Create `InsureChatBot.Api.Tests/MotorControllerResolveVehicleTests.cs`

**Frontend (`insurechat-web/src/app/`)**
- Create `core/motor-popular.ts` (+ `.spec.ts`) — `brandSlug` / `topBrands` / `topProvinces` (ย้ายจาก quote.ts, DRY)
- Create `core/doc-intake-handoff.ts` — one-shot handoff quote→chat
- Create `features/chatbot/intake-gate.ts` (+ `.spec.ts`) — `nextField` / `questionFor` (pure)
- Modify `core/motor-facet.ts` — `resolveVehicle()` + `VehicleResolveResult`
- Modify `features/quote/quote.ts` + `quote.html` — ใช้ util ร่วม; `applyExtraction` → handoff + navigate
- Modify `features/chatbot/chatbot.ts` + `chatbot.html` — wire intake gate

---

## Task 1: Backend — เพิ่ม Province / ProvinceLabel ใน chat context

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/Models/Chatbot/ChatbotModels.cs`

- [ ] **Step 1: เพิ่ม 2 property**

ใน class `ChatbotConversationContext` เพิ่มต่อจากบรรทัด `public decimal? SumInsure { get; set; }`:

```csharp

        // จังหวัดจดทะเบียน (ADR 0005 — เก็บลง proposal, ไม่กรองผลค้น).
        // เพิ่มใน ADR 0019: เดิม quote ส่งมาแต่ context ไม่มีฟิลด์ → ค่าหายตอน round-trip
        public string Province { get; set; }
        public string ProvinceLabel { get; set; }
```

- [ ] **Step 2: Build**

Run: `cd c:/CTD/ChatBot/insurechat-api && dotnet build InsureChatBot.Api/InsureChatBot.Api.csproj`
Expected: Build succeeded, 0 errors

- [ ] **Step 3: รัน test suite (ต้องไม่พัง)**

Run: `cd c:/CTD/ChatBot/insurechat-api && dotnet test`
Expected: Passed! 43 tests

- [ ] **Step 4: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-api && git add -A && git commit -m "feat(chatbot): add Province/ProvinceLabel to conversation context (ADR 0019)"
```

---

## Task 2: Backend — แยก test fakes ออกมาใช้ร่วม (refactor, DRY)

เหตุผล: Task 3 ต้องการ fake `IMotorFacetService` (12 เมธอด) ซึ่งตอนนี้เป็น private nested class ใน `MotorControllerExtractDocumentTests.cs` — ถ้าไม่แยกจะ copy ทั้งก้อน

**Files:**
- Create: `insurechat-api/InsureChatBot.Api.Tests/Fakes/MotorFakes.cs`
- Modify: `insurechat-api/InsureChatBot.Api.Tests/MotorControllerExtractDocumentTests.cs`

- [ ] **Step 1: สร้างไฟล์ fake ร่วม**

สร้าง `InsureChatBot.Api.Tests/Fakes/MotorFakes.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InsureChatBot.Api.Models.Document;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Services.Document;
using InsureChatBot.Api.Services.Motor;

namespace InsureChatBot.Api.Tests.Fakes
{
    /// <summary>fake vision extraction — คืนผลที่ตั้งไว้</summary>
    public class FakeDocumentExtractionService : IDocumentExtractionService
    {
        public VisionExtractionResult Result = new();
        public Task<VisionExtractionResult> ExtractAsync(byte[] image, string mimeType, CancellationToken ct = default)
            => Task.FromResult(Result);
    }

    /// <summary>fake PDF rasterizer — คืน byte เดิม</summary>
    public class FakePdfRasterizer : IPdfRasterizer
    {
        public byte[] RenderFirstPagePng(byte[] pdf) => pdf;
    }

    /// <summary>fake PDF rasterizer ที่โยน exception (ทดสอบ PDF เสีย)</summary>
    public class ThrowingPdfRasterizer : IPdfRasterizer
    {
        public byte[] RenderFirstPagePng(byte[] pdf) => throw new InvalidOperationException("corrupt pdf");
    }

    /// <summary>fake facet service — ใช้เฉพาะ ResolveVehicleAsync, เมธอดอื่นไม่ถูกเรียกในเทสต์</summary>
    public class FakeMotorFacetService : IMotorFacetService
    {
        public VehicleResolveResult Resolve = new();

        public Task<VehicleResolveResult> ResolveVehicleAsync(string brand, string model, int? year)
            => Task.FromResult(Resolve);

        public Task<List<FacetItem>> GetBrandsAsync() => throw new NotImplementedException();
        public Task<List<FacetItem>> GetModelsAsync(string brand) => throw new NotImplementedException();
        public Task<List<FacetItem>> GetCcAsync(string brand, string model) => throw new NotImplementedException();
        public Task<List<FacetItem>> GetYearsAsync(string brand, string model, string cc) => throw new NotImplementedException();
        public Task<List<PolTypeItem>> GetPolTypesAsync(string brand, string model, string cc, int year) => throw new NotImplementedException();
        public Task<List<FacetItem>> GetSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId) => throw new NotImplementedException();
        public Task<List<FacetItem>> GetFireSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId) => throw new NotImplementedException();
        public Task<List<FacetItem>> GetFloodSumInsuresAsync(string brand, string model, string cc, int year, int? polTypeId) => throw new NotImplementedException();
        public Task<List<FacetItem>> GetProvincesAsync() => throw new NotImplementedException();
        public Task<List<PackageRow>> SearchPackagesAsync(PackageSearchRequest req) => throw new NotImplementedException();
        public Task<MotorPackageDetail> GetPackageDetailAsync(int packageId) => throw new NotImplementedException();
    }
}
```

- [ ] **Step 2: แก้ test เดิมให้ใช้ fake ร่วม**

เปิด `InsureChatBot.Api.Tests/MotorControllerExtractDocumentTests.cs`:

1. เพิ่ม using: `using InsureChatBot.Api.Tests.Fakes;`
2. **ลบ** private nested class ทั้ง 4 ตัวออก: `FakeExtraction`, `FakePdf`, `FakeFacets`, `ThrowingPdf`
3. แทนที่การใช้งานทุกที่:
   - `FakeExtraction` → `FakeDocumentExtractionService`
   - `FakePdf` → `FakePdfRasterizer`
   - `FakeFacets` → `FakeMotorFacetService`
   - `ThrowingPdf` → `ThrowingPdfRasterizer`
4. `NewController(...)` เปลี่ยน signature ให้รับชนิดใหม่:

```csharp
        private static MotorController NewController(FakeDocumentExtractionService ex, FakeMotorFacetService fc)
            => new MotorController(fc, ex, new FakePdfRasterizer(), NullLogger<MotorController>.Instance);
```

**เก็บ** `MakeFile(...)` ไว้เป็น private ในไฟล์นี้ (มีแค่เทสต์นี้ที่อัปโหลดไฟล์)

- [ ] **Step 3: รัน test — ต้องเขียวเท่าเดิม**

Run: `cd c:/CTD/ChatBot/insurechat-api && dotnet test`
Expected: Passed! **43 tests** (จำนวนเท่าเดิม — refactor ล้วน ไม่เพิ่มเทสต์)

- [ ] **Step 4: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-api && git add -A && git commit -m "refactor(tests): extract shared motor fakes for controller tests"
```

---

## Task 3: Backend — `GET /api/Motor/ResolveVehicle` (TDD)

**Files:**
- Modify: `insurechat-api/InsureChatBot.Api/Controllers/MotorController.cs`
- Test: `insurechat-api/InsureChatBot.Api.Tests/MotorControllerResolveVehicleTests.cs`

- [ ] **Step 1: เขียน test ที่ยังไม่ผ่าน**

สร้าง `InsureChatBot.Api.Tests/MotorControllerResolveVehicleTests.cs`:

```csharp
using System.Threading.Tasks;
using InsureChatBot.Api.Controllers;
using InsureChatBot.Api.Models.Motor;
using InsureChatBot.Api.Tests.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace InsureChatBot.Api.Tests
{
    public class MotorControllerResolveVehicleTests
    {
        private static MotorController NewController(FakeMotorFacetService fc)
            => new MotorController(fc, new FakeDocumentExtractionService(), new FakePdfRasterizer(),
                                   NullLogger<MotorController>.Instance);

        [Fact]
        public async Task Returns_canonical_values_when_all_resolve()
        {
            var fc = new FakeMotorFacetService
            {
                Resolve = new VehicleResolveResult { Brand = "HONDA", Model = "CIVIC 1.5", Year = 2020 }
            };
            var res = await NewController(fc).ResolveVehicle("honda", "civic 1.5", 2020);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<VehicleResolveResult>(ok.Value);
            Assert.Equal("HONDA", dto.Brand);
            Assert.Equal("CIVIC 1.5", dto.Model);
            Assert.Equal(2020, dto.Year);
            Assert.True(dto.BrandOk);
            Assert.True(dto.ModelOk);
            Assert.True(dto.YearOk);
        }

        [Fact]
        public async Task Returns_partial_when_only_brand_resolves()
        {
            var fc = new FakeMotorFacetService { Resolve = new VehicleResolveResult { Brand = "KIA" } };
            var res = await NewController(fc).ResolveVehicle("kia", "K2700", 2005);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<VehicleResolveResult>(ok.Value);
            Assert.True(dto.BrandOk);
            Assert.False(dto.ModelOk);
            Assert.False(dto.YearOk);
            Assert.Null(dto.Model);
        }

        [Fact]
        public async Task Returns_all_null_when_nothing_resolves()
        {
            var fc = new FakeMotorFacetService { Resolve = new VehicleResolveResult() };
            var res = await NewController(fc).ResolveVehicle("zzz", null, null);
            var ok = Assert.IsType<OkObjectResult>(res);
            var dto = Assert.IsType<VehicleResolveResult>(ok.Value);
            Assert.False(dto.BrandOk);
            Assert.Null(dto.Brand);
        }
    }
}
```

- [ ] **Step 2: รัน test ให้ FAIL**

Run: `cd c:/CTD/ChatBot/insurechat-api && dotnet test --filter FullyQualifiedName~MotorControllerResolveVehicleTests`
Expected: compile error — `MotorController` ไม่มีเมธอด `ResolveVehicle`

- [ ] **Step 3: เพิ่ม endpoint**

ใน `InsureChatBot.Api/Controllers/MotorController.cs` เพิ่มต่อจาก action `PackageDetail` (ก่อนบรรทัด `private static readonly HashSet<string> AllowedTypes`):

```csharp
        /// <summary>resolve ค่าที่ผู้ใช้พิมพ์เอง → ค่า facet canonical (ใช้โดย intake gate ในแชต). ดู ADR 0019</summary>
        [HttpGet("ResolveVehicle")]
        public Task<IActionResult> ResolveVehicle([FromQuery] string brand, [FromQuery] string model, [FromQuery] int? year)
            => Guarded(async () => await _facets.ResolveVehicleAsync(brand, model, year));
```

`Guarded(...)` เป็น helper เดิมของ controller (คืน `Ok(result)` หรือ 500 พร้อม log)

- [ ] **Step 4: รัน test ให้ PASS**

Run: `cd c:/CTD/ChatBot/insurechat-api && dotnet test --filter FullyQualifiedName~MotorControllerResolveVehicleTests`
Expected: PASS (3 tests)

- [ ] **Step 5: รัน suite เต็ม**

Run: `cd c:/CTD/ChatBot/insurechat-api && dotnet test`
Expected: Passed! **46 tests**

- [ ] **Step 6: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-api && git add -A && git commit -m "feat(motor): GET /Motor/ResolveVehicle for intake gate (ADR 0019)"
```

---

## Task 4: Frontend — `core/motor-popular.ts` (TDD) + quote.ts ใช้ util ร่วม

ลอจิก "ยอดนิยม 8 ตัว" ตอนนี้เป็น private ใน `quote.ts` — ย้ายออกมาให้แชตใช้ด้วย (DRY) โดย **พฤติกรรม tile เดิมต้องไม่เปลี่ยน**

**Files:**
- Create: `insurechat-web/src/app/core/motor-popular.ts`
- Test: `insurechat-web/src/app/core/motor-popular.spec.ts`
- Modify: `insurechat-web/src/app/features/quote/quote.ts`

- [ ] **Step 1: เขียน test ที่ยังไม่ผ่าน**

สร้าง `insurechat-web/src/app/core/motor-popular.spec.ts` (vitest ใช้ globals — ไม่ต้อง import `describe/it/expect`):

```ts
import { brandSlug, topBrands, topProvinces } from './motor-popular';
import { FacetItem } from './motor-facet';

const F = (...v: string[]): FacetItem[] => v.map((x) => ({ value: x, label: x }));

describe('motor-popular', () => {
  it('brandSlug ตัดจุดและเว้นวรรค', () => {
    expect(brandSlug('B.M.W.')).toBe('bmw');
    expect(brandSlug('GWM TANK')).toBe('gwm-tank');
  });

  it('topBrands ดันยี่ห้อยอดนิยมขึ้นก่อน', () => {
    const out = topBrands(F('KIA', 'TOYOTA', 'HONDA'), 8).map((x) => x.value);
    expect(out).toEqual(['TOYOTA', 'HONDA', 'KIA']);
  });

  it('topBrands ตัดตามจำนวนที่ขอ', () => {
    expect(topBrands(F('TOYOTA', 'HONDA', 'ISUZU', 'MITSUBISHI'), 2).length).toBe(2);
  });

  it('topProvinces เอาจังหวัดที่เดาได้ขึ้นก่อน และตัด row ทดสอบ', () => {
    const all: FacetItem[] = [
      { value: '50', label: 'เชียงใหม่' },
      { value: '10', label: 'กรุงเทพมหานคร' },
      { value: '99', label: 'จังหวัดทดสอบ' }
    ];
    const out = topProvinces(all, '50', 8);
    expect(out[0].value).toBe('50');
    expect(out.some((p) => p.label.includes('ทดสอบ'))).toBe(false);
  });

  it('topProvinces เรียงตามประชากรเมื่อไม่มีจังหวัดที่เดาได้', () => {
    const all: FacetItem[] = [
      { value: '50', label: 'เชียงใหม่' },
      { value: '10', label: 'กรุงเทพมหานคร' }
    ];
    expect(topProvinces(all, '', 8)[0].label).toBe('กรุงเทพมหานคร');
  });
});
```

- [ ] **Step 2: รัน test ให้ FAIL**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng test --watch=false`
Expected: FAIL — ไม่พบโมดูล `./motor-popular`

- [ ] **Step 3: สร้าง util**

สร้าง `insurechat-web/src/app/core/motor-popular.ts`:

```ts
import { FacetItem } from './motor-facet';

/** normalize ชื่อยี่ห้อ → slug ของไฟล์โลโก้ (เช่น "B.M.W." → "bmw", "GWM TANK" → "gwm-tank") */
export function brandSlug(v: string): string {
  return v.toLowerCase().replace(/\./g, '').trim().replace(/\s+/g, '-');
}

// ลำดับยี่ห้อยอดนิยมในตลาดไทย (slug → อันดับ) — ดันตัวที่มีโลโก้ขึ้นก่อน
const BRAND_RANK: Record<string, number> = {
  toyota: 0, honda: 1, isuzu: 2, mitsubishi: 3, nissan: 4, mazda: 5, ford: 6,
  mg: 7, suzuki: 8, 'mercedes-benz': 9, bmw: 10, hyundai: 11, byd: 12, kia: 13,
  chevrolet: 14, subaru: 15, lexus: 16, volvo: 17, mini: 18, audi: 19
};

/** ยี่ห้อยอดนิยม n ตัว (ที่เหลือให้เลือกจาก dropdown / พิมพ์เอง) */
export function topBrands(all: FacetItem[], n = 8): FacetItem[] {
  const rank = (x: FacetItem) => BRAND_RANK[brandSlug(x.value)] ?? 999;
  return [...all].sort((a, b) => rank(a) - rank(b)).slice(0, n);
}

// จังหวัดประชากรมากสุด (ไทย) เรียงลำดับ
const POP_PROVINCES = [
  'กรุงเทพ', 'นครราชสีมา', 'อุบลราชธานี', 'ขอนแก่น', 'เชียงใหม่', 'บุรีรัมย์',
  'อุดรธานี', 'นครศรีธรรมราช', 'ศรีสะเกษ', 'ชลบุรี', 'สงขลา', 'สุรินทร์',
  'นนทบุรี', 'เชียงราย', 'สมุทรปราการ', 'ร้อยเอ็ด', 'ชัยภูมิ', 'สกลนคร',
  'ปทุมธานี', 'นครสวรรค์', 'สุราษฎร์ธานี', 'นครปฐม', 'กาฬสินธุ์', 'พิษณุโลก'
];

function provRank(label: string): number {
  for (let i = 0; i < POP_PROVINCES.length; i++) if (label.includes(POP_PROVINCES[i])) return i;
  return 999;
}

/** จังหวัด n ตัว: จังหวัดที่เดาได้ (จาก IP) มาก่อน แล้วประชากรมากสุด; ตัด row "ทดสอบ" ใน DB ทิ้ง */
export function topProvinces(all: FacetItem[], detectedValue: string, n = 8): FacetItem[] {
  const list = all.filter((p) => !p.label.includes('ทดสอบ'));
  const det = list.find((p) => p.value === detectedValue);
  const rest = list
    .filter((p) => p.value !== detectedValue)
    .sort((a, b) => provRank(a.label) - provRank(b.label));
  return (det ? [det, ...rest] : rest).slice(0, n);
}
```

- [ ] **Step 4: รัน test ให้ PASS**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng test --watch=false`
Expected: PASS (2 เดิม + 5 ใหม่ = 7 tests)

- [ ] **Step 5: ให้ quote.ts ใช้ util (ลบโค้ดซ้ำ)**

ใน `insurechat-web/src/app/features/quote/quote.ts`:

(5a) เพิ่ม import:
```ts
import { brandSlug, topBrands, topProvinces } from '../../core/motor-popular';
```

(5b) **ลบ** สมาชิก private เหล่านี้ทิ้ง: `popProvinces`, `provRank(...)`, `brandRank`, `slug(...)`

(5c) แทน getter `provinceTiles` ด้วย:
```ts
  /** tile จังหวัด 8 ตัว: จังหวัดลูกค้า (จาก IP) มาก่อน แล้วประชากรมากสุด */
  get provinceTiles(): FacetItem[] {
    return topProvinces(this.provinces(), this.detectedProvince(), 8);
  }
```

(5d) แทน getter `brandTiles` ด้วย:
```ts
  /** tile ยี่ห้อยอดนิยม 8 ตัว (2 แถว × 4 — ที่เหลือเลือกจาก dropdown ใต้การ์ด) */
  get brandTiles(): FacetItem[] {
    return topBrands(this.brands(), 8);
  }
```

(5e) แทน `brandLogo` ให้ใช้ `brandSlug` ที่ import มา:
```ts
  /** โลโก้แบรนด์รถจาก public CDN (car-logos-dataset). โหลดไม่ได้ → ซ่อนรูป เหลือชื่อ */
  brandLogo(v: string): string {
    return `https://cdn.jsdelivr.net/gh/filippofilip95/car-logos-dataset@master/logos/optimized/${brandSlug(v)}.png`;
  }
```

- [ ] **Step 6: Build + test**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng build && npx ng test --watch=false`
Expected: build 0 errors; tests 7 passed

- [ ] **Step 7: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-web && git add -A && git commit -m "refactor(web): extract topBrands/topProvinces into core/motor-popular (DRY)"
```

---

## Task 5: Frontend — `MotorFacet.resolveVehicle()`

**Files:**
- Modify: `insurechat-web/src/app/core/motor-facet.ts`

- [ ] **Step 1: เพิ่ม interface**

ใน `core/motor-facet.ts` เพิ่มใต้ `DocumentExtractResponse`:

```ts
/** ผล resolve ค่าที่ผู้ใช้พิมพ์ → ค่า facet canonical (ADR 0019) */
export interface VehicleResolveResult {
  brand: string | null; model: string | null; year: number | null;
  brandOk: boolean; modelOk: boolean; yearOk: boolean;
}
```

- [ ] **Step 2: เพิ่มเมธอดในคลาส `MotorFacet`** (ใต้ `extractDocument`)

```ts
  /** resolve ค่าที่ผู้ใช้พิมพ์เอง → ค่า facet canonical (intake gate, ADR 0019) */
  resolveVehicle(brand: string, model?: string, year?: number): Observable<VehicleResolveResult> {
    const params: Record<string, string> = { brand };
    if (model) params['model'] = model;
    if (year != null) params['year'] = String(year);
    return this.http.get<VehicleResolveResult>(`${this.base}/ResolveVehicle`, { params });
  }
```

- [ ] **Step 3: Build**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng build`
Expected: 0 errors

- [ ] **Step 4: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-web && git add -A && git commit -m "feat(web): MotorFacet.resolveVehicle() for intake gate"
```

---

## Task 6: Frontend — `core/doc-intake-handoff.ts`

**Files:**
- Create: `insurechat-web/src/app/core/doc-intake-handoff.ts`

- [ ] **Step 1: สร้างไฟล์**

```ts
import { Injectable } from '@angular/core';
import { ResolvedVehicle } from './motor-facet';

export interface DocIntakeData {
  table: string;                 // markdown ตารางที่ vision สกัดได้
  resolved: ResolvedVehicle;     // ยี่ห้อ/รุ่น/ปี ที่ resolve เข้าค่า DB ได้ (อาจไม่ครบ)
  province?: string;             // จังหวัดที่เดาจาก IP ที่หน้า quote (อาจว่าง)
  provinceLabel?: string;
}

/** ส่งผลสกัดเอกสารจากหน้า quote → หน้า chat (one-shot). ดู ADR 0019 */
@Injectable({ providedIn: 'root' })
export class DocIntakeHandoff {
  private pending: DocIntakeData | null = null;

  set(data: DocIntakeData): void { this.pending = data; }

  /** อ่านแล้วล้าง (ใช้ครั้งเดียว) */
  consume(): DocIntakeData | null {
    const d = this.pending;
    this.pending = null;
    return d;
  }
}
```

- [ ] **Step 2: Build**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng build`
Expected: 0 errors

- [ ] **Step 3: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-web && git add -A && git commit -m "feat(web): DocIntakeHandoff (quote -> chat one-shot)"
```

---

## Task 7: Frontend — `features/chatbot/intake-gate.ts` (TDD, pure)

**Files:**
- Create: `insurechat-web/src/app/features/chatbot/intake-gate.ts`
- Test: `insurechat-web/src/app/features/chatbot/intake-gate.spec.ts`

- [ ] **Step 1: เขียน test ที่ยังไม่ผ่าน**

สร้าง `insurechat-web/src/app/features/chatbot/intake-gate.spec.ts`:

```ts
import { nextField, questionFor } from './intake-gate';

describe('intake-gate', () => {
  it('ไม่รู้อะไรเลย → ถามยี่ห้อก่อน', () => {
    expect(nextField({})).toBe('brand');
  });

  it('รู้ยี่ห้อ → ถามรุ่น', () => {
    expect(nextField({ brand: 'KIA' })).toBe('model');
  });

  it('รู้ยี่ห้อ+รุ่น → ถามปี', () => {
    expect(nextField({ brand: 'KIA', model: 'SORENTO 2.2' })).toBe('year');
  });

  it('รู้รถครบ → ถามจังหวัด', () => {
    expect(nextField({ brand: 'KIA', model: 'SORENTO 2.2', year: 2020 })).toBe('province');
  });

  it('ครบทุกฟิลด์ → null', () => {
    expect(nextField({ brand: 'KIA', model: 'SORENTO 2.2', year: 2020, province: '10' })).toBeNull();
  });

  it('null / undefined นับว่ายังขาด', () => {
    expect(nextField({ brand: null, model: 'X' })).toBe('brand');
    expect(nextField({ brand: 'KIA', model: 'X', year: null })).toBe('year');
  });

  it('คำถามรุ่นอ้างถึงยี่ห้อที่รู้แล้ว', () => {
    expect(questionFor('model', { brand: 'HONDA' })).toContain('HONDA');
  });

  it('คำถามปีอ้างถึงยี่ห้อ+รุ่น', () => {
    const q = questionFor('year', { brand: 'HONDA', model: 'CIVIC 1.5' });
    expect(q).toContain('HONDA');
    expect(q).toContain('CIVIC 1.5');
  });
});
```

- [ ] **Step 2: รัน test ให้ FAIL**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng test --watch=false`
Expected: FAIL — ไม่พบโมดูล `./intake-gate`

- [ ] **Step 3: สร้างโมดูล**

สร้าง `insurechat-web/src/app/features/chatbot/intake-gate.ts`:

```ts
/** ฟิลด์ที่ intake gate ต้องเก็บให้ครบก่อนค้นแผน (ลำดับเดียวกับฟอร์ม quote) — ADR 0019 */
export type IntakeField = 'brand' | 'model' | 'year' | 'province';

export interface IntakeKnown {
  brand?: string | null;
  model?: string | null;
  year?: number | null;
  province?: string | null;        // รหัสจังหวัด (value)
  provinceLabel?: string | null;   // ชื่อไทย (label)
}

/** ฟิลด์ถัดไปที่ต้องถาม; null = ครบแล้ว พร้อมค้นแผน */
export function nextField(known: IntakeKnown): IntakeField | null {
  if (!known.brand) return 'brand';
  if (!known.model) return 'model';
  if (!known.year) return 'year';
  if (!known.province) return 'province';
  return null;
}

/** ข้อความคำถามภาษาไทยของแต่ละฟิลด์ (โทน "พิมพ์" — ที่ปรึกษาประกัน) */
export function questionFor(field: IntakeField, known: IntakeKnown): string {
  switch (field) {
    case 'brand':
      return 'ขอทราบ **ยี่ห้อรถ** หน่อยค่ะ 🚗 พิมพ์ชื่อยี่ห้อ หรือกดเลือกจากปุ่มด้านล่างได้เลยค่ะ';
    case 'model':
      return `รถ **${known.brand}** รุ่นไหนคะ? กดเลือกจากปุ่มด้านล่าง หรือพิมพ์ชื่อรุ่นได้เลยค่ะ`;
    case 'year':
      return `**${known.brand} ${known.model}** ปีอะไรคะ?`;
    case 'province':
      return 'รถจดทะเบียนที่ **จังหวัด** อะไรคะ?';
  }
}
```

- [ ] **Step 4: รัน test ให้ PASS**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng test --watch=false`
Expected: PASS (7 เดิม + 8 ใหม่ = 15 tests)

- [ ] **Step 5: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-web && git add -A && git commit -m "feat(chat): intake-gate pure module (nextField/questionFor)"
```

---

## Task 8: Frontend — quote.ts แนบเอกสารแล้วเด้งเข้าแชต

**Files:**
- Modify: `insurechat-web/src/app/features/quote/quote.ts`
- Modify: `insurechat-web/src/app/features/quote/quote.html`

- [ ] **Step 1: `quote.ts` — import + constructor**

(1a) เพิ่ม import:
```ts
import { DocIntakeHandoff } from '../../core/doc-intake-handoff';
```

(1b) constructor เพิ่ม dependency:
```ts
  constructor(
    private facet: MotorFacet,
    private handoff: QuoteHandoff,
    private docIntake: DocIntakeHandoff,
    private router: Router
  ) {}
```

- [ ] **Step 2: `quote.ts` — ลบ `docNote` แล้วเปลี่ยน `applyExtraction`**

(2a) **ลบ** บรรทัด `docNote = signal('');`

(2b) ใน `onDocSelected` **ลบ** `this.docNote.set('');` ออกจากบรรทัด
`this.docLoading.set(true); this.error.set(''); this.docNote.set('');`
→ เหลือ `this.docLoading.set(true); this.error.set('');`

(2c) แทนที่เมธอด `applyExtraction` ทั้งก้อนด้วย:
```ts
  /** แนบเอกสารสำเร็จ → ส่งผลเข้าแชตทันที; ฟิลด์ที่ขาด แชตจะถามเอง (intake gate — ADR 0019) */
  private applyExtraction(r: DocumentExtractResponse): void {
    const label = this.provinces().find((p) => p.value === this.province)?.label ?? '';
    this.docIntake.set({
      table: r.table,
      resolved: r.resolved,
      province: this.province || undefined,
      provinceLabel: label || undefined
    });
    this.router.navigate(['/chat']);
  }
```

- [ ] **Step 3: `quote.html` — ลบแถบ docNote**

ในบล็อก `<div class="doc-intake">` **ลบ**บรรทัด:
```html
      @if (docNote()) { <div class="doc-note">{{ docNote() }}</div> }
```
(เหลือ `<input>` + `<button>` เท่านั้น)

- [ ] **Step 3b: `quote.css` — ลบ CSS ที่ไม่มีใครใช้แล้ว**

ใน `insurechat-web/src/app/features/quote/quote.css` **ลบ**บรรทัด:
```css
.doc-note { margin-top: 8px; font-size: 13px; color: var(--muted, #64748b); }
```
(เก็บ `.doc-intake` และ `.doc-btn` ไว้ — ยังใช้อยู่)

- [ ] **Step 4: Build + test**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng build && npx ng test --watch=false`
Expected: build 0 errors; 15 tests passed

- [ ] **Step 5: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-web && git add -A && git commit -m "feat(quote): attach document -> navigate to chat (intake gate takes over)"
```

---

## Task 9: Frontend — chatbot.ts wire intake gate

**Files:**
- Modify: `insurechat-web/src/app/features/chatbot/chatbot.ts`
- Modify: `insurechat-web/src/app/features/chatbot/chatbot.html`

- [ ] **Step 1: imports + state + constructor**

(1a) เพิ่ม/แก้ import:
```ts
import { MotorFacet, MotorPackageDetail, DocumentExtractResponse, ResolvedVehicle, FacetItem } from '../../core/motor-facet';
import { DocIntakeHandoff, DocIntakeData } from '../../core/doc-intake-handoff';
import { IntakeField, IntakeKnown, nextField, questionFor } from './intake-gate';
import { topBrands, topProvinces } from '../../core/motor-popular';
```

(1b) เพิ่ม state (ใกล้ `suggestions = signal<string[]>([]);`):
```ts
  // intake gate (ADR 0019) — ไม่ null = กำลังถามฟิลด์ที่ขาด, LLM ยังไม่ถูกเรียก
  intake = signal<IntakeKnown | null>(null);
  private intakeProvinces: FacetItem[] = [];   // cache ไว้ map label → value
```

(1c) constructor เพิ่ม `private docIntake: DocIntakeHandoff` (วางก่อน `private motor: MotorFacet`):
```ts
  constructor(
    private http: HttpClient,
    protected auth: Auth,
    private router: Router,
    private handoff: QuoteHandoff,
    private docIntake: DocIntakeHandoff,
    private motor: MotorFacet
  ) {}
```

- [ ] **Step 2: `ngOnInit` — consume doc-intake ก่อน**

แทนที่ `ngOnInit` ด้วย:
```ts
  ngOnInit(): void {
    const doc = this.docIntake.consume();
    if (doc) { this.startFromDocument(doc); return; }

    const sel = this.handoff.consume();
    if (sel) { this.seedFromQuote(sel); return; }

    this.messages.push({
      timestamp: new Date().toISOString(),
      role: 'assistant',
      text: 'สวัสดีค่ะ 😊 พิมพ์เป็น "ผู้ช่วยหาประกันรถ" ของคุณเองค่ะ — อยากให้ช่วยหาแผนที่ใช่ เริ่มจากกรอกข้อมูลรถที่หน้า **คำนวณเบี้ย 🚗** ได้เลยนะคะ'
    });
    this.scrollChatToBottom(true);
  }
```

- [ ] **Step 3: `seedFromQuote` / `seedContext` รับจังหวัด**

แทนที่ 2 เมธอดนี้ด้วย:
```ts
  /** มาจากหน้า quote (5 ฟิลด์) → seed candidate set + เกริ่นเริ่มสโคป (ADR 0005) */
  private seedFromQuote(d: any): void {
    this.seedContext(d.brand, d.model, d.year, d.candidates ?? [], d.province, d.provinceLabel);
  }

  /** seed context + greeting + ปุ่มถามด่วนชุดแรก (ใช้ทั้ง quote handoff, document intake และ intake gate) */
  private seedContext(
    brand: string, model: string, year: number, cands: any[],
    province?: string | null, provinceLabel?: string | null
  ): void {
    const n = cands.length;
    const insurers = new Set(cands.map((p: any) => p.insurerName)).size;
    const greeting =
      `เยี่ยมเลยค่ะ 😊 ดูให้รถ **${brand} ${model} ปี ${year}** แล้วนะคะ — ` +
      `เจอ **${n} แผน** จาก **${insurers} บริษัท** ที่เลือกได้เลยค่ะ\n\n` +
      `เดี๋ยวพิมพ์ช่วยกรองให้เหลือแผนที่ใช่ บอกงบเบี้ย/ทุนประกันที่ต้องการ หรือกดปุ่มด้านล่างได้เลยค่ะ`;

    this.conversationContext = {
      turns: [{ timestamp: new Date().toISOString(), role: 'assistant', text: greeting }],
      currentStep: 'llm',
      productType: 'motor',
      carBrand: brand, carModel: model, carYear: year,
      province: province ?? null, provinceLabel: provinceLabel ?? null,
      discoveredNeeds: [],
      proposals: [],
      candidates: cands
    };
    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: greeting });
    this.scrollChatToBottom(true);
    this.isReadyToPropose.set(false);
    this.prevReady = false; this.prevProposalSig = '';
    this.fetchOpeningSuggestions();
  }
```

- [ ] **Step 4: เพิ่มลอจิก gate (วางต่อจาก `seedContext`)**

```ts
  /** มาจากหน้า quote พร้อมผลสกัดเอกสาร → โชว์ตาราง แล้วเข้า gate หรือ seed เลย */
  private startFromDocument(d: DocIntakeData): void {
    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: 'อ่านเอกสารให้แล้วค่ะ 😊 นี่คือข้อมูลที่อ่านได้' });
    if (d.table) this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: d.table });
    this.scrollChatToBottom(true);
    this.startIntakeOrSeed(d.resolved, d.province, d.provinceLabel);
  }

  /** ครบ → ค้นแผนแล้ว seed; ไม่ครบ → เปิด gate ถามทีละฟิลด์ */
  private startIntakeOrSeed(rv: ResolvedVehicle, province?: string, provinceLabel?: string): void {
    const known: IntakeKnown = {
      brand: rv.brand, model: rv.model, year: rv.year,
      province: province || null, provinceLabel: provinceLabel || null
    };
    if (nextField(known) === null) { this.searchAndSeed(known); return; }
    this.intake.set(known);
    this.askNext();
  }

  /** ถามฟิลด์ถัดไป + โหลด chip ตัวเลือกจริงจาก facet */
  private askNext(): void {
    const known = this.intake();
    if (!known) return;
    const field = nextField(known);
    if (!field) { this.searchAndSeed(known); return; }

    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: questionFor(field, known) });
    this.scrollChatToBottom(true);
    this.loadChips(field, known);
  }

  /** chip = ค่า canonical จริงจาก DB (ยี่ห้อ/จังหวัด โชว์เฉพาะยอดนิยม 8) */
  private loadChips(field: IntakeField, known: IntakeKnown): void {
    this.suggestions.set([]);
    if (field === 'brand') {
      this.motor.brands().subscribe({
        next: (b) => this.suggestions.set(topBrands(b, 8).map((x) => x.value)),
        error: () => this.suggestions.set([])
      });
    } else if (field === 'model') {
      this.motor.models(known.brand!).subscribe({
        next: (m) => this.suggestions.set(m.map((x) => x.value)),
        error: () => this.suggestions.set([])
      });
    } else if (field === 'year') {
      this.motor.years(known.brand!, known.model!, '').subscribe({
        next: (y) => this.suggestions.set(y.map((x) => x.value)),
        error: () => this.suggestions.set([])
      });
    } else {
      this.motor.provinces().subscribe({
        next: (p) => {
          this.intakeProvinces = p;
          this.suggestions.set(topProvinces(p, known.province ?? '', 8).map((x) => x.label));
        },
        error: () => this.suggestions.set([])
      });
    }
  }

  /** รับคำตอบของ gate (จาก chip หรือพิมพ์เอง) */
  private answerIntake(text: string): void {
    const known = this.intake();
    if (!known) return;
    const field = nextField(known);
    if (!field) return;

    this.suggestions.set([]);

    if (field === 'province') {
      const p = this.intakeProvinces.find((x) => x.label === text || x.value === text);
      if (!p) { this.retryIntake(`ไม่พบจังหวัด "${text}" ค่ะ ลองกดเลือกจากปุ่มด้านล่างนะคะ`, field, known); return; }
      this.intake.set({ ...known, province: p.value, provinceLabel: p.label });
      this.askNext();
      return;
    }

    // brand / model / year → ให้ backend resolve เป็นค่า canonical (พิมพ์เอง = fuzzy, chip = ตรงอยู่แล้ว)
    const probeBrand = field === 'brand' ? text : (known.brand ?? '');
    const probeModel = field === 'model' ? text : (known.model ?? undefined);
    const probeYear = field === 'year' ? (Number(text.replace(/\D/g, '')) || undefined) : (known.year ?? undefined);

    this.loading.set(true);
    this.statusText.set('กำลังตรวจข้อมูลรถ');
    this.motor.resolveVehicle(probeBrand, probeModel, probeYear).subscribe({
      next: (r) => {
        this.loading.set(false); this.statusText.set('');
        const ok = field === 'brand' ? r.brandOk : field === 'model' ? r.modelOk : r.yearOk;
        if (!ok) { this.retryIntake(`ไม่พบ "${text}" ในระบบค่ะ ลองกดเลือกจากปุ่มด้านล่างนะคะ`, field, known); return; }
        this.intake.set({
          ...known,
          brand: r.brand ?? known.brand,
          model: r.model ?? known.model,
          year: r.year ?? known.year
        });
        this.askNext();
      },
      error: () => {
        this.loading.set(false); this.statusText.set('');
        this.retryIntake('ตรวจข้อมูลไม่สำเร็จค่ะ ลองกดเลือกจากปุ่มด้านล่างนะคะ', field, known);
      }
    });
  }

  /** ตอบไม่ผ่าน → บอกเหตุผลแล้วโชว์ chip เดิมซ้ำ */
  private retryIntake(msg: string, field: IntakeField, known: IntakeKnown): void {
    this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: '⚠️ ' + msg });
    this.scrollChatToBottom();
    this.loadChips(field, known);
  }

  /** ครบแล้ว → ค้นแผนจริง; เจอ → ปิด gate ให้ LLM รับช่วง, ไม่เจอ → ถอยกลับไปถามรุ่น */
  private searchAndSeed(known: IntakeKnown): void {
    this.loading.set(true);
    this.statusText.set('กำลังค้นแผนประกัน');
    this.suggestions.set([]);
    this.motor.search({ brand: known.brand!, model: known.model!, cc: '', year: known.year!, sort: 'prem' }).subscribe({
      next: (rows) => {
        this.loading.set(false); this.statusText.set('');
        if (!rows || rows.length === 0) {
          this.messages.push({
            timestamp: new Date().toISOString(), role: 'assistant',
            text: `ไม่พบแผนประกันสำหรับ **${known.brand} ${known.model} ปี ${known.year}** ค่ะ ลองเลือกรุ่นหรือปีใหม่นะคะ`
          });
          this.intake.set({ ...known, model: null, year: null });   // ถอยกลับไปถามรุ่น
          this.askNext();
          return;
        }
        this.intake.set(null);   // ปิด gate → LLM รับช่วง (มี candidates แล้ว)
        this.seedContext(known.brand!, known.model!, known.year!, rows, known.province, known.provinceLabel);
      },
      error: () => {
        this.loading.set(false); this.statusText.set('');
        this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: '⚠️ ค้นแผนไม่สำเร็จ ลองใหม่อีกครั้งนะคะ' });
        this.scrollChatToBottom();
      }
    });
  }
```

- [ ] **Step 5: `sendMessage` แตกทาง (กัน LLM ถูกเรียกตอน gate เปิด)**

แทนที่ต้นเมธอด `sendMessage` — เพิ่มบล็อกใหม่หลัง guard เดิม:
```ts
  sendMessage(): void {
    const text = this.userInput.trim();
    if (!text || this.loading()) return;

    // gate เปิดอยู่ → คำตอบเข้า gate ไม่ยิง /MessageStream (ADR 0019)
    if (this.intake()) {
      this.userInput = '';
      this.messages.push({ timestamp: new Date().toISOString(), role: 'user', text });
      this.scrollChatToBottom(true);
      this.answerIntake(text);
      return;
    }

    this.messages.push({ timestamp: new Date().toISOString(), role: 'user', text });
    this.userInput = '';
    this.stick = true;
    this.scrollChatToBottom(true);
    this.loading.set(true);
    this.suggestions.set([]);
    this.proposalCollapsed.set(true);
    this.statusText.set('กำลังอ่านคำถาม');
    this.streamingText.set('');
    this.streamingActive.set(true);

    void this.streamReply(text);
  }
```
> `sendSuggestion()` ไม่ต้องแก้ — มันเรียก `sendMessage()` อยู่แล้ว จึงแตกทางตามให้เอง

- [ ] **Step 6: ปุ่ม 📎 ในแชต ใช้ gate เดียวกัน**

แทนที่ `applyDocToChat` ด้วย:
```ts
  /** โชว์ตารางที่สกัดได้ แล้วเข้า gate/seed แบบเดียวกับที่มาจากหน้า quote */
  private applyDocToChat(r: DocumentExtractResponse): void {
    if (r.table) this.messages.push({ timestamp: new Date().toISOString(), role: 'assistant', text: r.table });
    this.scrollChatToBottom();
    this.startIntakeOrSeed(r.resolved);
  }
```

- [ ] **Step 7: `chatbot.html` — ปิดปุ่มแนบขณะ gate เปิด**

แก้บรรทัดปุ่มแนบใน `<footer class="chat-input">`:
```html
    <button type="button" class="attach-btn" (click)="chatDoc.click()" [disabled]="loading() || !!intake()" title="แนบเอกสารรถ">📎</button>
```

- [ ] **Step 8: Build + test**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng build && npx ng test --watch=false`
Expected: build 0 errors; 15 tests passed

- [ ] **Step 9: Commit**

```bash
cd c:/CTD/ChatBot/insurechat-web && git add -A && git commit -m "feat(chat): intake gate — ask missing brand/model/year/province before seeding LLM"
```

---

## Task 10: Verify — build/test + manual E2E

**Files:** ไม่มี (verification)

- [ ] **Step 1: Backend suite**

Run: `cd c:/CTD/ChatBot/insurechat-api && dotnet test`
Expected: Passed! **46 tests**

- [ ] **Step 2: Frontend build + unit tests**

Run: `cd c:/CTD/ChatBot/insurechat-web && npx ng build && npx ng test --watch=false`
Expected: build 0 errors; **15 tests** passed

- [ ] **Step 3: รันแอป**

Terminal 1: `cd c:/CTD/ChatBot/insurechat-api && dotnet run --project InsureChatBot.Api --urls http://localhost:5228`
Terminal 2: `cd c:/CTD/ChatBot/insurechat-web && npm start`
Expected: API listening :5228, web :4200

> `.env` ต้องมี `EF_DB_CONNECTION` (ขึ้นต้น `Server=`), `KPN_DB_CONNECTION`, `OPENROUTER_API_KEY`, `JWT_KEY` (ไม่ว่าง)

- [ ] **Step 4: เคส A — เอกสาร resolve ไม่ครบ (KIA K2700)**

เปิด http://localhost:4200/quote → กด "📄 แนบเอกสารรถ" → เลือก `c:\CTD\ChatBot\ตัวอย่างเอกสาร\รายการจดทะเบียน.png`
Expected:
1. เด้งไปหน้า `/chat` ทันที
2. เห็นกล่อง "อ่านเอกสารให้แล้วค่ะ" + กล่องตาราง
3. บอทถาม **รุ่น** พร้อม chip 18 รุ่นของ KIA (ยี่ห้อ resolve ได้แล้ว)
4. กด chip เช่น `SORENTO 2.2` → บอทถาม **ปี** พร้อม chip ปี
5. เลือกปี → บอทถาม **จังหวัด** พร้อม chip 8 จังหวัด
6. เลือกจังหวัด → เห็น "เจอ N แผน จาก M บริษัท" + ปุ่มถามด่วน (LLM รับช่วง)

- [ ] **Step 5: เคส B — พิมพ์เองแทนกด chip**

ทำซ้ำเคส A แต่ตอนถามรุ่น **พิมพ์** `sorento 2.2` (ตัวเล็ก) → ต้อง resolve เป็น `SORENTO 2.2` และเดินต่อได้
พิมพ์ค่ามั่ว เช่น `zzzz` → ต้องได้ "⚠️ ไม่พบ ... ลองกดเลือกจากปุ่มด้านล่างนะคะ" + chip กลับมา

- [ ] **Step 6: เคส C — ไม่ใช่เอกสารรถ**

หน้า `/quote` แนบ `c:\CTD\ChatBot\InsureChat\taste-skill-tmp\assets\readme-banner.png`
Expected: **ไม่เด้งไปแชต**, โชว์ error ที่หน้า quote ("ดูเหมือนไม่ใช่เอกสารรถ …")

- [ ] **Step 7: เคส D — ไม่ทำให้ flow เดิมพัง**

หน้า `/quote` กรอกฟอร์มเอง (ยี่ห้อ→รุ่น→ปี→จังหวัด) → กดค้นหา → เข้าแชตแบบเดิม เห็น "เจอ N แผน" ทันที (ไม่มี gate)

- [ ] **Step 8: เคส E — ปุ่ม 📎 ในแชต**

หน้า `/chat` กด 📎 แนบ `รายการจดทะเบียน.png` → เห็นตาราง แล้วเข้า gate ถามรุ่น (เหมือนเคส A ตั้งแต่ข้อ 3)

---

## Notes for the implementer

- **อย่าเพิ่ม tool ค้นหาให้ LLM** — ADR 0005/0019 บังคับว่า frontend เป็นคนค้น
- **อย่าให้ `sendMessage` ยิง `/MessageStream` ขณะ `intake()` ไม่ null** — บอทจะตอบ "กลับไปหน้าคำนวณเบี้ย" เพราะยังไม่มี `Candidates`
- chip ของยี่ห้อ/จังหวัด = **ยอดนิยม 8 ตัว** (ผู้ใช้พิมพ์เองได้ถ้าไม่อยู่ในนั้น); chip ของรุ่น/ปี = **ทั้งหมด**
- `province` chip โชว์ `label` (ชื่อไทย) แต่เก็บ `value` (รหัส) ลง context
- commit backend ที่ `insurechat-api`, frontend ที่ `insurechat-web` (branch `feature/document-intake`)
