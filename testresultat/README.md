# Resultat från demogranskningen

- [baseline.json](baseline.json): uppdaterade filer före rättning. 17 kontroller godkända; startprojektets felaktiga status gjorde att körningen misslyckades.
- [basic.json](basic.json): rättade 01 och 02. 10 kontroller godkända, inklusive startstatus och serveromstart.
- [slutkorning-blockerad.json](slutkorning-blockerad.json): full slutkörning avbruten när Windows Application Control blockerade 03. Den filen är inte ett godkänt testresultat.

Se [demogranskningen](../demogranskning.md) för omfattning, rättningar och återstående kontroller. Kör `demo-test.cjs` för nya loggar, skärmbilder och resultat i `demo-testresultat/`; checka bara in granskade resultat här.
