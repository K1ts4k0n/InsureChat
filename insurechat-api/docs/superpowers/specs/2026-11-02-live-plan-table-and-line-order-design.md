# Live plan table and LINE order design

## Goal

Show only actual shortlisted motor-insurance plans in chat, using the exact comparison columns requested by the business. When a customer submits the selected plan, notify the configured LINE administrator with the same plan facts and an explicit acknowledgement button.

## Decisions

- A plan is always one row. The rendered chat table has these columns, in this order: `#`, company, class, repair network, sum insured, annual premium, fire, deductible, accessories, flood.
- The candidate query now carries the deductible from `PackageMotor.OwnAmtDeduc`. There is no trustworthy accessories field in the source schema, so the display value is `ไม่มีข้อมูล`; it is never invented.
- The browser-only fallback renders candidate rows rather than explanatory, hard-coded prices. With no candidate set, it directs the customer to calculate a quote and does not make up a plan.
- A submitted web order sends the LINE administrator a fact-based Flex card. Its acknowledgement action is a LINE message button containing the order number; it is deliberately a human acknowledgement, not an unimplemented status mutation.

## Non-goals

- This change does not claim that a plan covers a benefit not present in the source data.
- This change does not add a new order-status database workflow.
