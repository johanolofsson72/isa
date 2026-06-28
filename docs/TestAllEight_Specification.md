# TestAllEight - Specifikation

## Översikt
Detta är det nuvarande testsystemet som validerar att alla 8 rapporttyper kan genereras utan fel, men som INTE validerar att rapporternas innehåll är korrekt.

## Testfiler
- **Huvudtest**: `simple_final_test.sh`
- **E2E-test**: `tests/E2E/TestReportGeneration_ShouldCreateReportAndSendEmail`
- **Validering**: `tests/AnalysisPlugin.Tests/ValidateCompleteDataChain_SIE4ToExcel_ShouldMatch100Percent`

## De 8 frågorna som testas
1. "Skapa en utgiftsrapport för 2023 med prydlig startsida och sedan sidor för diagram och combo charts"
2. "Skapa en rapport för 2024 med prydlig startsida och sedan sidor med funnel charts där du kan se samtliga projekt och som summerar intäkter och kostnader och som går att filtrera på datum"
3. "Skapa en rapport med prydlig startsida och sedan sidor med waterfall charts som visar omsättning, varukostnad, övriga externa kostnader, personalkostnader, avskrivningar, finansiella kostnader uppdelat på kvartal för de senaste 5 åren"
4. "Skapa en rapport för 2022 med prydlig startsida och sedan sidor med box & whisker som visar omsättning, varukostnad, bruttovinst i SEK, bruttovinst i % för fg vecka"
5. "Skapa en standard balansrapport för 2024"
6. "Skapa en standard årsrapport för 2023"
7. "Skapa en standard momsrapport för 2025 Q3"
8. "Skapa en inbjudande framtidsanalys inför 2026 Q1"

## Rapporttyper
1. Ekonomirapport2023
2. Ekonomirapport2024
3. Femårsrapport
4. Ekonomirapport2022
5. Balansrapport2024
6. Årsrapport2023
7. Momsrapport2025Q3
8. Analysrapport2026Q1

## Status
- ✅ Alla 8 tester passerar (teknisk validering)
- ❌ Innehållsvalidering saknas (t.ex. momsrapport innehåller inte momsdata)
- ❌ Rapportspecifik validering saknas

## Nästa steg
Genomgå varje rapport individuellt och validera att innehållet matchar frågan.
