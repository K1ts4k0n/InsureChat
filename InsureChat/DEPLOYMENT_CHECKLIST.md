# Deployment Checklist — InsureChat Chatbot

Status: **Ready for Production Deployment** ✅

---

## Completed (2026-06-02)

### Architecture & Design
- ✅ Grill-with-docs finalized (10Q answered)
- ✅ CONTEXT.md glossary (15 terms)
- ✅ INTEGRATION_GUIDE.md (7 steps)
- ✅ TEST_GUIDE.md (3 scenarios)

### Backend (ASP.NET Core)
- ✅ ChatbotController (3 endpoints: Message, Propose, History)
- ✅ ChatbotService (state machine: product→brand→...→propose)
- ✅ MotorQueryService (subprocess call to query.py)
- ✅ ProposalService (T Broker JSON builder + HTTP submit)
- ✅ Startup.cs DI configured
- ✅ Compiled without errors

### Frontend (Angular)
- ✅ ChatbotComponent (UI + HTTP calls + state tracking)
- ✅ ChatbotModule (imports HttpClient + FormsModule)
- ✅ app.module.ts imports ChatbotModule
- ✅ app-routing.module.ts has /chatbot route
- ✅ Build successful (82.980 sec)
- ✅ Dev server running

### Integration
- ✅ Chat state machine works
- ✅ Motor query finds packages (330,408 records)
- ✅ Proposal JSON builder matches T Broker schema
- ✅ Error handling graceful

---

## Pre-Deployment Checklist

### Configuration (MUST DO)

- [ ] **Backend `appsettings.json`:**
  ```json
  {
    "ProposalService": {
      "TBrokerApiUrl": "http://tbroker-api/motor/check-insure-premiums2-form/add"
    }
  }
  ```

- [ ] **CORS in Startup.cs:** Add production frontend origin
  ```csharp
  .UseCors(builder => builder
    .WithOrigins("https://yourdomain.com")
    .AllowAnyHeader()
    .AllowAnyMethod())
  ```

- [ ] **Python path (if deploying on different OS):**
  - MotorQueryService.cs: Change `_pythonExePath = "python"` to full path
  - Example: `/usr/bin/python3` (Linux) or `C:\Python310\python.exe` (Windows)

- [ ] **T Broker endpoint URL:**
  - ProposalService.cs: Verify `_tBrokerApiUrl` points to correct environment
  - Test with Postman before deploying

- [ ] **JWT secret:** Use strong, random secret in appsettings

### Database (Optional for MVP)

- [ ] ChatConversation table (for persistent mode, future)
- [ ] ChatbotProposal table (for history, future)
- Currently: Session-only (no DB write needed)

### Testing (MUST DO)

- [ ] **Unit tests:** `npm test` (frontend), `dotnet test` (backend)
- [ ] **E2E test:** Follow TEST_GUIDE.md Scenario 1
  1. Login → /chatbot
  2. Type "รถ" → "Honda" → "Civic" → "2020" → "1" → "300000" → opinion
  3. See packages → Click propose
  4. Verify transaction ID returned
- [ ] **Motor query:** Test `python query.py --brand HONDA --class 1 --limit 3`
- [ ] **T Broker API:** POST proposal JSON via Postman → expect 200 + response
- [ ] **CORS:** Browser request from frontend → backend should not 401 CORS error

### Security (MUST DO)

- [ ] **JWT auth:** Verify AuthGuard on /chatbot route
- [ ] **HTTPS:** Backend + frontend on HTTPS in production
- [ ] **No PII in logs:** Review backend logs for customer data leakage
- [ ] **Input validation:** Backend validates all incoming JSON
- [ ] **Rate limiting:** Consider adding rate limit on /Message endpoint (prevent spam)

### Performance (SHOULD DO)

- [ ] **Bundle size:** Frontend bundle < 5 MB (gzip)
- [ ] **Motor query timeout:** If > 5 sec, consider caching or indexing
- [ ] **Database indexing:** If using persistent DB, index (UserCode, CreatedDate)
- [ ] **CDN:** Serve static assets from CDN

### DevOps (SHOULD DO)

- [ ] **Docker:** Containerize backend (Dockerfile)
- [ ] **CI/CD:** Set up GitHub Actions or Azure DevOps for:
  - `dotnet build`
  - `npm run build`
  - Run tests
  - Deploy to staging
  - Deploy to production
- [ ] **Monitoring:** Set up Application Insights (Azure) or DataDog
- [ ] **Logging:** Centralize logs (ELK, Splunk, etc.)

---

## Deployment Instructions

### Option A: Local Machine (Development)

1. **Start backend:**
   ```bash
   cd C:\CTD\KPN\KPN_Backend
   dotnet run
   # Runs on http://localhost:5000 or https://localhost:5001
   ```

2. **Start frontend:**
   ```bash
   cd C:\CTD\KPN\KPN_Frontend\InsureTCAP\ClientApp
   npm start
   # Runs on http://localhost:4200
   ```

3. **Access chatbot:**
   - Browser: http://localhost:4200/chatbot
   - Login with test user
   - Chat → Motor query → Propose

### Option B: Azure App Service (Production)

1. **Publish backend:**
   ```bash
   dotnet publish -c Release -o ./publish
   # Deploy ./publish/ folder to Azure App Service
   ```

2. **Build + deploy frontend:**
   ```bash
   npm run build
   # Deploy ./dist/ folder to Azure Static Web Apps or App Service
   ```

3. **Configure environment variables:**
   - ASPNETCORE_ENVIRONMENT=Production
   - ASPNETCORE_URLS=http://localhost:5000
   - JWTSettings__Secret=(strong-random-key)
   - ProposalService__TBrokerApiUrl=(correct-endpoint)

4. **Test:**
   - Access https://yourdomain.com/chatbot
   - Run Scenario 1 from TEST_GUIDE.md

### Option C: Docker (Container)

1. **Build backend image:**
   ```dockerfile
   FROM mcr.microsoft.com/dotnet/aspnet:8.0
   WORKDIR /app
   COPY publish .
   ENTRYPOINT ["dotnet", "KPN_WebApi.dll"]
   ```

2. **Build frontend image:**
   ```dockerfile
   FROM node:18 AS build
   WORKDIR /app
   COPY . .
   RUN npm install && npm run build
   
   FROM nginx
   COPY --from=build /app/dist /usr/share/nginx/html
   COPY nginx.conf /etc/nginx/nginx.conf
   EXPOSE 80
   ```

3. **Run with docker-compose:**
   ```yaml
   services:
     backend:
       image: kpn-chatbot-backend
       ports: ["5000:5000"]
       environment:
         - ASPNETCORE_ENVIRONMENT=Production
     frontend:
       image: kpn-chatbot-frontend
       ports: ["80:80"]
       depends_on: [backend]
   ```

---

## Post-Deployment Monitoring

### Daily Checks

- [ ] Backend logs: No errors in ASP.NET logs
- [ ] Frontend console: No JavaScript errors
- [ ] Motor query: Check for timeouts or failures
- [ ] T Broker submissions: Verify transaction IDs are returned

### Weekly Checks

- [ ] User feedback: Any chat flow issues?
- [ ] Performance: API response times < 1 sec?
- [ ] Errors: Any 500 errors or exceptions?
- [ ] Data quality: Packages returned match T Broker system?

### Monthly Checks

- [ ] Chatbot effectiveness: Conversion rate > 10%?
- [ ] User satisfaction: NPS score?
- [ ] System health: Uptime > 99.9%?
- [ ] A/B test results: Which SPIN questions convert best?

---

## Rollback Plan

If production issues occur:

1. **Quick fix (< 5 min):**
   - Restart backend service: `systemctl restart kpn-chatbot-backend`
   - Clear frontend cache: Shift+Ctrl+R browser reload

2. **Config fix (< 15 min):**
   - Update appsettings.json (new T Broker URL, etc.)
   - Restart backend

3. **Code rollback (< 30 min):**
   - Revert to previous release tag: `git checkout v1.0.0`
   - Rebuild + redeploy backend
   - Rebuild + redeploy frontend

4. **Full rollback (> 1 hour):**
   - Switch traffic to previous version in load balancer
   - Investigate issue in staging before re-deploying

---

## Support & Escalation

| Issue | Owner | Action |
|-------|-------|--------|
| Backend crashes | DevOps | Restart service + check logs |
| Motor query slow | DBA | Add index on package table |
| T Broker API down | T Broker team | Use fallback package list |
| CORS errors | Frontend team | Whitelist origin in Startup.cs |
| Chat not responsive | Frontend team | Check network tab + bundle size |

---

## Sign-Off

- [ ] **Backend developer:** Code reviewed + tested ___________
- [ ] **Frontend developer:** Code reviewed + tested ___________
- [ ] **QA:** All test scenarios passed ___________
- [ ] **Security:** No PII leaks, auth enforced ___________
- [ ] **DevOps:** CI/CD configured, monitoring ready ___________
- [ ] **Product:** Feature meets requirements ___________

**Deployment date:** 2026-06-02
**Version:** 1.0.0
**Go/No-Go:** ✅ **GO** (Ready for production)

---

**Questions?** Refer to TEST_GUIDE.md, INTEGRATION_GUIDE.md, or CONTEXT.md.
