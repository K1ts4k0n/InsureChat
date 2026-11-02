# 0003 — Conversation engine แบบ LLM (OpenRouter) + tool-use, fallback state machine

state machine เดิม (product→brand→…→propose) ทำงานเชื่อถือได้แต่ **แข็งทื่อ ไม่เป็นธรรมชาติ และปิดการขายไม่เก่ง**. เราตัดสินใจเพิ่ม **conversation engine ที่ขับด้วย LLM** ให้คุยเป็นธรรมชาติและใช้เทคนิคการขาย (SPIN/รับมือข้อโต้แย้ง/ปิดการขาย จาก wiki playbook) — โดย **ข้อมูล/ธุรกรรมทั้งหมดต้องผ่าน tool** (เบี้ยจริงจาก 330k, ร่าง proposal) เพื่อกัน hallucination และคุมจรรยาบรรณ คปภ. ([[CONTEXT.md]] §Compliance).

แยกเป็น `IConversationEngine` 2 ตัว — `LlmConversationEngine` (หลัก) และ `StateMachineConversationEngine` (เดิม = fallback) — router เลือก LLM ก่อน ถ้าไม่มี key / error / rate-limit → ตกไป state machine.

## Considered Options

- **OpenRouter + `openai/gpt-oss-120b` (free) (เลือก)** — ฟรี, OpenAI-compatible API. แลกกับคุณภาพภาษาไทย/ความเป๊ะของ tool-calling ที่ด้อยกว่าโมเดลใหญ่ และ rate limit. ทำ client เป็น **config-driven** (BaseUrl/ApiKey/Model) → สลับไป Claude/รุ่นเสียเงินภายหลังได้โดยไม่แก้โค้ด.
- **Anthropic Claude (Sonnet/Opus)** — คุณภาพ/ความเป๊ะสูงกว่า แต่มีค่าใช้จ่ายต่อข้อความ. *ไม่เลือกตอนนี้เพราะต้องการฟรี* — แต่ config รองรับให้สลับได้.
- **คง state machine ล้วน** — เสถียร/ฟรี แต่ไม่เป็นธรรมชาติ ไม่ตอบโจทย์ "ปิดการขายได้จริง". เก็บไว้เป็น fallback แทน.

## Consequences

- **Grounding (บังคับ):** LLM ห้ามแต่งเบี้ย/ชื่อแผนเอง — พูดได้เฉพาะค่าที่ tool คืนมา; ใส่กฎ คปภ. + คำเตือน "ต้องยืนยันใน T Broker" ใน system prompt; เปิดเผยข้อยกเว้น. โมเดลฟรีเสี่ยงหลุดกฎนี้มากกว่า → ต้องเฝ้า/ทดสอบหนัก (อนาคตอาจเพิ่ม hard-validation เทียบเลขกับ tool result).
- **Tools:** `search_motor_packages` (เรียก MotorQueryService → 330k), `prepare_proposal` (เลือกแพคเกจ → set draft + ready flag). **การส่งจริงไม่ใช่ tool** — คนกดปุ่มยืนยัน → `POST /Propose` (deterministic ProposalService, mock/live) = human-in-the-loop กับ action ที่มีผลจริง.
- **State:** ประวัติสนทนา (messages) round-trip client↔server (stateless, session-based เหมือนเดิม) — ยังไม่เก็บ DB.
- **MVP ไม่ stream** — รอคำตอบเต็ม + typing indicator เดิม.
- ดูแล 2 engine แต่ state machine มีอยู่แล้ว (ต้นทุนส่วนเพิ่มต่ำ แลกความไม่มีวันใช้ไม่ได้).
- ต้องมี `OPENROUTER_API_KEY` (ไม่ commit — env var / appsettings.Development).
