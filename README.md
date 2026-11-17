# NativeViewer

Ein kleiner Windows-Desktop-Viewer für JSON-, XML-, Markdown- und TXT-Dateien.

## Voraussetzungen

- Windows 11
- Für den Ziel-PC ist keine .NET-Installation erforderlich
- Die Anwendung wird als einzelne, selbstenthaltende Windows-x64-EXE veröffentlicht

Die EXE enthält die benötigte .NET-Desktop-Laufzeit. Derzeit ist das Ziel Windows x64; ARM64 wird nicht mit dieser Ausgabe abgedeckt.
Die Benutzeroberfläche und Fehlermeldungen sind auf Deutsch.

## Funktionen

- Dateien über **Öffnen…** auswählen oder per Drag-and-drop laden
- Liste der 50 zuletzt geöffneten Dateien, lokal neben der EXE gespeichert
- Datei speichern oder **Speichern unter…**; beim Überschreiben wird nachgefragt
- JSON validieren und einrücken
- XML validieren und formatiert anzeigen
- Markdown mit Überschriften, Listen, Codeblöcken und einfacher Inline-Formatierung darstellen
- Markdown-Ansicht und Quelltext umschalten
- Reguläre Suche mit Weitersuchen und Beachtung der Groß-/Kleinschreibung
- Zeilenumbruch und Schriftgröße einstellen
- Einrückung für JSON und XML auf 1 bis 8 Leerzeichen einstellen
- TXT-Dateien unverändert lesen
- Ungültiges JSON/XML mit verständlichem Status anzeigen und den Originaltext beibehalten

JSON und XML werden standardmäßig formatiert angezeigt. **Speichern** schreibt die formatierte JSON-/XML-Ansicht; bei Markdown und Text bleibt der Quelltext unverändert. UTF-8-, UTF-16- und UTF-32-BOMs werden erkannt und beim Speichern erhalten. Dateien ohne BOM werden als striktes UTF-8 gelesen und bleiben BOM-los. Die Markdown-Darstellung unterstützt bewusst einen häufig genutzten Teilumfang und ist kein vollständiger CommonMark-Parser.

Die letzten Dateien und eine abweichende Einrückung werden in `NativeViewer.settings.xml` im EXE-Ordner gespeichert. Die Einrückung ist standardmäßig 2 Leerzeichen; dieser Wert wird nur dann in der Einstellungsdatei abgelegt, wenn er geändert wurde. Zum Schreiben der lokalen Einstellungen braucht die Anwendung Schreibrechte im EXE-Ordner.

## Build

Im Projektverzeichnis ausführen:

```powershell
dotnet publish NativeViewer\NativeViewer.csproj -c Release -r win-x64 --source https://api.nuget.org/v3/index.json
```

Die weiterzugebende Datei liegt anschließend in `NativeViewer\bin\Release\net10.0-windows\win-x64\publish\NativeViewer.exe`. Diese EXE kann allein auf einen Windows-11-x64-PC kopiert und dort gestartet werden. Es werden keine externen NuGet-Pakete verwendet.

Beim Veröffentlichen lädt das .NET SDK die offiziellen .NET-Laufzeitpakete; diese sind in der EXE enthalten und müssen auf dem Ziel-PC nicht installiert werden.
