# TESTPLAN — Status & Täckning

Senast uppdaterad: 2025-09-11

## Översikt
- Framework: xUnit + FakeItEasy (ADR-0011)
- CI-regler: inga varningar, grön testsvit, nightly regression (ADR-0041)

## Täckning per domän (mål ≥ 80% publika metoder)
- Customers:  ✅  (enhet + integration)
- Invoices:   ✅  (enhet + integration Fortnox sandbox)
- Documents:  ✅  (OCR + AI, mock/fallback)
- Reports:    ⚠️  (påbörjad; exportformat testade)
- Notifications: ✅  (idempotens, failover)
- Workflows (Quartz): ✅  (schemaläggning, DLQ)

## Nightly (ADR-0041)
- Sandbox-integrationer: Fortnox, OCR, notifiering
- Healthchecks + smoke-tests
- Kostnadsrapport (ADR-0051)

## Öppna gap
- Bankgiro/SEB end-to-end (fas 2)
- Skatteverket/SCB simuleringar (fas 3)
- Peppol validering mot externa gateways

> Se även `NIGHTLY_TESTS.md` för exakta testfall och triggertider.
