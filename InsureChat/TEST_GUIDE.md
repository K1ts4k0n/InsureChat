# Chatbot Testing Guide

Status: **Ready for E2E testing**

---

## Setup Before Testing

1. **Backend running:**
   ```bash
   cd C:\CTD\KPN\KPN_Backend
   dotnet run
   # Should start on https://localhost:5001 or http://localhost:5000
   ```

2. **Frontend running:**
   ```bash
   cd C:\CTD\KPN\KPN_Frontend\InsureTCAP\ClientApp
   npm start
   # Already running on http://localhost:4200
   ```

3. **Python motor query tool available:**
   - `C:\CTD\ChatBot\InsureChat\tools\motor\query.py` exists
   - Python 3 installed and accessible

---

## Test Scenarios

### Scenario 1: Complete Chat Flow

**Steps:**
1. Navigate to http://localhost:4200 → Login with JWT
2. Go to `/chatbot` route
3. **Type:** `รถ` → Should get "ยี่ห้าอะไร?"
4. **Type:** `Honda` → Should get "รุ่นอะไร?"
5. **Type:** `Civic` → Should get "ปีกี่?"
6. **Type:** `2020` → Should get "ชั้นประกัน?"
7. **Type:** `1` → Should get "ทุนประกัน?"
8. **Type:** `300000` → SPIN question appears
9. **Type:** `เพราะต้องการความมั่นใจเต็ม` → Top 3 insurers shown
10. **Click:** "เสนอแพคเกจ" → Transaction ID returned

**Expected:**
- ✅ Messages flow smoothly
- ✅ Motor query finds packages
- ✅ T Broker receives proposal

---

## API Testing (Postman)

### POST /Message
```bash
curl -X POST http://localhost:5000/api/FrontEnd/Chatbot/Message \
  -H "Authorization: Bearer {JWT}" \
  -H "Content-Type: application/json" \
  -d '{"message":"รถ", "conversationContext":{}}'
```

### POST /Propose
```bash
curl -X POST http://localhost:5000/api/FrontEnd/Chatbot/Propose \
  -H "Authorization: Bearer {JWT}" \
  -H "Content-Type: application/json" \
  -d '{"proposalJson":{...}, "conversationContext":{...}}'
```

---

## Debugging Checklist

- [ ] Backend logs: `dotnet run` output has no errors
- [ ] Frontend console: F12 → Console tab has no red errors
- [ ] Network: F12 → Network tab → POST requests return 200
- [ ] Motor query: runs `python query.py --brand HONDA` manually ✅
- [ ] T Broker endpoint: reachable and accepts JSON POST

---

## Known Issues

| Issue | Fix |
|-------|-----|
| Python not found | Add to PATH or hardcode full path in MotorQueryService |
| CORS blocked | Add frontend origin to Startup.cs UseCors() |
| T Broker endpoint 404 | Verify URL in ProposalService._tBrokerApiUrl |
| Motor query returns empty | Check query.py file and filter args |

---

**Ready to test?** Start with Scenario 1. Report errors in console or backend logs.
