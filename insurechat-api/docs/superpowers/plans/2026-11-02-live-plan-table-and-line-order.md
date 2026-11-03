# Live plan table and LINE order implementation plan

> **For implementation:** Execute inline in the current checkout. The repository is shared and the user requested the completed, published change.

**Goal:** Replace fabricated client fallback answers with real plan tables and send selected orders to LINE with a fact-based confirmation card.

**Architecture:** Extend the compact package row with deductible, pass it through the LLM filtering tool and deterministic state-machine response, and use the same compact fact set in the browser fallback. Add a dedicated LINE order Flex builder that contains the selected plan facts and a message-action acknowledgement.

## Steps

1. Add a failing unit test for the browser table renderer, then expose a pure renderer that outputs exactly the required headers, one row per package, locale-formatted money, and `ไม่มีข้อมูล` for accessories.
2. Add a failing .NET unit test for the LINE order card, then build the card from the selected proposal and actual motor package detail without generic coverage claims.
3. Carry `OwnAmtDeduc` into `PackageRow`, update LLM tool/state-machine table contracts, and wire submitted order notification to retrieve the selected package detail and send the new LINE card.
4. Update the GitHub Pages fallback path to provide its actual candidate set to the renderer; when none exists, provide an honest calculation prompt without fabricated plans.
5. Run frontend and backend tests/builds, commit source, deploy the built frontend to `gh-pages`, and verify the published route.
