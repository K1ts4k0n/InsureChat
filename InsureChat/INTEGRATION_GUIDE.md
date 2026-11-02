# InsureChat Chatbot — Integration Guide

Status: **Skeleton complete**, ready for integration

---

## Architecture Overview

```
┌─────────────────┐
│  Angular Front  │ (KPN_Frontend/chatbot.component.*)
│  - UI/chat      │
│  - HTTP calls   │
└────────┬────────┘
         │ POST /api/FrontEnd/Chatbot/Message
         │
┌────────▼────────┐
│ ASP.NET Backend │ (KPN_Backend)
│ - ChatbotService│ ← State machine (product→brand→...→propose)
│ - HTTP to tools │
└────────┬────────┘
         │ subprocess: python tools/motor/query.py
         │
┌────────▼────────┐
│   Wiki & Tools  │ (InsureChat)
│ - concepts      │ ← Knowledge (SPIN, objection, closing)
│ - tools/motor/  │ ← Package search (330k+ records)
└─────────────────┘
```

---

## Step 1: Frontend Setup (Done)

✅ **ChatbotComponent created** in `KPN_Frontend/InsureTCAP/ClientApp/src/app/views/modules/chatbot/`

Files:
- `chatbot.component.ts` — Logic
- `chatbot.component.html` — UI
- `chatbot.component.css` — Styling
- `chatbot.module.ts` — Module export
- `chatbot.component.spec.ts` — Tests
- `README.md` — Component guide

**Action:** Import ChatbotModule into your app routing, or embed `<app-chatbot></app-chatbot>` in existing layout.

```typescript
// app.module.ts or feature module
import { ChatbotModule } from './views/modules/chatbot/chatbot.module';

@NgModule({
  imports: [ChatbotModule],
})
export class AppModule {}
```

---

## Step 2: Backend Setup (Done)

✅ **ChatbotController + Service created** in `KPN_Backend`

Files:
- `Controllers/FrontEnd/ChatbotController.cs` — 3 endpoints
- `Services/Implementations/ChatbotService.cs` — State machine
- `Models/FrontEnd/ChatbotModels.cs` — DTO schemas
- `Startup.cs` — DI registered (IChatbotService, HttpClientFactory)

**Endpoints:**
1. `POST /api/FrontEnd/Chatbot/Message` — Send message, get reply + updated context
2. `POST /api/FrontEnd/Chatbot/Propose` — Submit proposal JSON
3. `GET /api/FrontEnd/Chatbot/History` — Get conversation history (optional MVP)

**Action:** Compile backend. No build steps needed (already integrated into Startup.cs).

---

## Step 3: Integrate Motor Package Query (TODO)

**Current:** ChatbotService has static step handlers (no real package search yet).

**Next:** When step = `ready_to_propose`, call `tools/motor/query.py` to find packages.

### Option A: HTTP Wrapper (Recommended)
Create `MotorQueryService.cs` that calls Python wrapper HTTP endpoint:

```csharp
public class MotorQueryService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public async Task<List<Package>> SearchPackagesAsync(
        string brand, string model, int year, string insuranceClass, decimal sumInsure)
    {
        // Call HTTP endpoint that wraps tools/motor/query.py
        // GET http://localhost:5001/api/motor/query?brand=HONDA&model=CIVIC&...
        // Return: List<Package>
    }
}
```

**Build Python HTTP wrapper** (Flask/FastAPI):
```python
# tools/motor/api_wrapper.py
from flask import Flask, request
from query import search_packages

app = Flask(__name__)

@app.route('/query', methods=['GET'])
def query():
    brand = request.args.get('brand')
    model = request.args.get('model')
    class_type = request.args.get('class')
    # ... call query.py
    return jsonify(results)

if __name__ == '__main__':
    app.run(port=5001)
```

### Option B: Direct Subprocess (Simpler)
Call `query.py` directly from ChatbotService:

```csharp
var processInfo = new ProcessStartInfo
{
    FileName = "python",
    Arguments = $"tools/motor/query.py --brand {brand} --model {model} --class {insuranceClass} --json",
    RedirectStandardOutput = true,
    UseShellExecute = false
};
var output = // ... capture stdout, parse JSON
```

---

## Step 4: Integrate T Broker Proposal Submission (TODO)

**Current:** ChatbotService.SubmitProposalAsync() is stubbed.

**Next:** POST chatbot-built proposal JSON → `motor/check-insure-premiums2-form/add`

```csharp
public async Task<ChatbotProposalResponse> SubmitProposalAsync(...)
{
    // Build JSON matching schema (user provided):
    // carFeetList, quotationPremCoverList, customer info, etc.

    var content = JsonSerializer.Serialize(proposalJson);
    var response = await _httpClientFactory
        .CreateClient()
        .PostAsync("http://tbroker/motor/check-insure-premiums2-form/add", ...);

    return new ChatbotProposalResponse { Success = true, TransactionId = ... };
}
```

---

## Step 5: Wiki Knowledge Integration (Optional for MVP)

**Current:** Chatbot asks fixed questions (no wiki context).

**Nice-to-have:** Inject wiki knowledge into SPIN questions.

Example:
```
Static: "ทำไมต้องชั้น 1?"
Enhanced: "ชั้น 1 ให้ความคุ้มครอง [wiki: full coverage definition]. 
          คุณต้องการความมั่นใจแบบนี้ไหม?"
```

**Implementation:**
1. Add keyword search method to ChatbotService (read wiki/concepts/*.md files)
2. Extract relevant passages
3. Inject into SPIN templates

---

## Step 6: Database for Persistent Conversations (Optional)

**Current:** Session-only (memory lost on refresh).

**Future:** Persist conversation + proposals to DB.

Model:
```csharp
public class ChatbotConversation
{
    public int Id { get; set; }
    public string UserCode { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public string ContextJson { get; set; } // JSON-serialized ConversationContext
    public List<ChatbotProposal> Proposals { get; set; }
}

public class ChatbotProposal
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public string ProposalJson { get; set; }
    public string TransactionId { get; set; }
    public string Status { get; set; } // pending, confirmed, rejected
    public DateTime CreatedDate { get; set; }
}
```

Add migration + update service to read/write DB.

---

## Testing Checklist

### Frontend
- [ ] Component renders without error
- [ ] Can type and send message
- [ ] Message bubbles appear (user + bot)
- [ ] Typing indicator shows during loading
- [ ] Send button disables when loading
- [ ] Conversation state persists across turns

### Backend
- [ ] Compile without error
- [ ] Endpoints accessible (POST /Message, /Propose, GET /History)
- [ ] State machine transitions correctly (ask_product → ask_brand → ...)
- [ ] JWT auth works (401 on missing token)
- [ ] HTTP calls to motor/query.py succeed
- [ ] Proposal JSON submitted to T Broker API

### E2E
- [ ] Login → ChatBot page → Type "รถ" → Get "ยี่ห้าอะไร?"
- [ ] Continue flow → Class selection → SPIN question
- [ ] Search packages → See results
- [ ] Click propose → Transaction ID returned

---

## Deployment Checklist

- [ ] Backend compiled (Release config)
- [ ] Frontend built (ng build --configuration production)
- [ ] ChatbotComponent imported in routing/layout
- [ ] CORS configured (frontend origin allowed in backend)
- [ ] JWT token issuer configured
- [ ] Motor query service running (Python API wrapper or direct subprocess)
- [ ] T Broker API endpoint accessible
- [ ] Database migrations applied (if persisting conversations)
- [ ] Environment variables set (API URLs, secrets)
- [ ] Smoke test: Login → ChatBot → Full flow → Proposal submit

---

## Support & Debugging

### Common Errors

**Error: "Module not found: chatbot.module"**
→ Ensure ChatbotModule path is correct, component files exist

**Error: 401 Unauthorized**
→ JWT token missing or expired; check AuthInterceptor

**Error: Package query returns empty**
→ Motor query API not running; check Python wrapper or query.py

**Error: Proposal submit fails**
→ T Broker API endpoint wrong or down; verify URL + test with Postman

### Logs

- **Frontend:** Browser DevTools → Console/Network
- **Backend:** Application log file (check Startup.cs logging config)
- **Motor query:** stdout from python subprocess or API wrapper logs

---

## Next Phase: Scale & Polish

1. **Conversation analytics** — Track which products sell best, where users drop off
2. **A/B testing** — Test different SPIN questions, closing techniques
3. **Multichannel** — WhatsApp, Line, Messenger bot wrappers
4. **Mobile app** — React Native wrapper around same backend
5. **Performance** — Cache wiki, optimize motor query (add indexes)
6. **Compliance audit** — Record all conversations for regulatory review

---

## Questions?

Refer to:
- `CONTEXT.md` — Domain glossary
- `wiki/overview.md` — Wiki home
- `tools/motor/README.md` — Package query guide
- KPN_Backend/KPN_Frontend code comments
