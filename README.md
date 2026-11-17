# RhaView

Ein kleiner Windows-Desktop-Viewer und Editor für JSON, XML, Markdown, HTML, CSS, CSV und Text.

## Voraussetzungen

- Windows 11
- Verwendet das in Windows 11 integrierte .NET Framework 4.8
- Es werden keine .NET-Laufzeit und keine Drittanbieterbibliotheken in die Anwendung gebündelt

RhaView nutzt ausschließlich WPF- und .NET-Framework-Komponenten, die Windows bereitstellt. Dadurch ist die EXE deutlich kleiner; sie benötigt die Windows-eigene .NET-Framework-Laufzeit und ist nicht selbstenthaltend.
Die Benutzeroberfläche und Fehlermeldungen sind auf Deutsch.

## Funktionen

- Dateien über **Öffnen…** auswählen oder per Drag-and-drop laden
- Mehrere Dateien gleichzeitig als Tabs in einem Fenster öffnen; Tabs fragen vor dem Schließen nach ungespeicherten Änderungen
- Über **Datei → Neues Fenster** weitere Fenster öffnen
- Dateien als Kommandozeilenargumente übergeben; `*` und `?` im Dateinamen werden unterstützt, zum Beispiel `RhaView .\*.xml`
- Liste der 50 zuletzt geöffneten Dateien, lokal neben der EXE gespeichert
- JSON, XML, Markdown, HTML, CSS, CSV und Text bearbeiten und speichern
- Markdown-Quelltext mit Live-Vorschau bearbeiten
- HTML/CSS-Quelltext formatieren
- CSV-Trennzeichen Komma/Semikolon automatisch erkennen und als Tabelle mit automatisch angepassten Spalten anzeigen
- In der Symbolleiste zwischen formatierter Ansicht und Rohtext wechseln sowie die Einrückung per -1/+1 anpassen
- Rückgängig/Wiederholen, Weitersuchen und Treffer markieren
- Statusleiste mit Dateiname, Größe, Zeile/Spalte und Änderungsstatus
- Ein-Tasten-Kürzel: F1 Hilfe, F2 Speichern, F3 Weitersuchen, F4 Öffnen, F6 Suche, F8 vorheriger Treffer
- Strg-Tastenkürzel bleiben als Alternative verfügbar
- Datei speichern oder **Speichern unter…**
- JSON validieren und einrücken
- XML validieren und formatiert anzeigen
- Markdown mit Überschriften, Listen, Codeblöcken und einfacher Inline-Formatierung darstellen
- Markdown-Ansicht und Quelltext umschalten
- Reguläre Suche mit Weitersuchen und Beachtung der Groß-/Kleinschreibung
- Zeilenumbruch und Schriftgröße einstellen
- Einrückung für JSON, XML, HTML und CSS auf 1 bis 8 Leerzeichen einstellen; Standard: 3
- Deutsches Hilfemenü mit Kommandozeilenhilfe, Tastenkürzeln, Programminformation und Lizenz
- TXT-Dateien unverändert lesen
- Ungültiges JSON/XML mit verständlichem Status anzeigen und den Originaltext beibehalten

JSON, XML, HTML und CSS werden standardmäßig formatiert angezeigt. **Speichern** schreibt den aktuellen Editorinhalt; CSV wird in einer Tabelle mit separat editierbarem Quelltext angezeigt. UTF-8-, UTF-16- und UTF-32-BOMs werden erkannt und beim Speichern erhalten. Dateien ohne BOM werden als striktes UTF-8 gelesen und bleiben BOM-los. Die Markdown-Darstellung unterstützt bewusst einen häufig genutzten Teilumfang und ist kein vollständiger CommonMark-Parser.

Die letzten Dateien und eine abweichende Einrückung werden in `RhaView.settings.xml` im EXE-Ordner gespeichert. Die Einrückung ist standardmäßig 3 Leerzeichen; dieser Wert wird nur dann in der Einstellungsdatei abgelegt, wenn er geändert wurde. Zum Schreiben der lokalen Einstellungen braucht die Anwendung Schreibrechte im EXE-Ordner.

## Lizenz

RhaView 1.0 steht unter der MIT-Lizenz. Private und kommerzielle Nutzung ist erlaubt; die Lizenz verlangt keine Quellcode-Veröffentlichung. Das bestehende GitHub-Repository bleibt wie gewünscht öffentlich und enthält den Quellcode. Der Lizenztext ist in der Anwendung unter **Hilfe → Lizenz** verfügbar.

## Build

Mit dem .NET SDK bauen:

```powershell
dotnet build NativeViewer\RhaView.csproj -c Release
```

Die EXE liegt anschließend in `NativeViewer\bin\Release\net48\RhaView.exe`. Es werden keine externen NuGet-Laufzeitpakete verwendet oder in die EXE kopiert. Für den Build kann das .NET SDK Referenzassemblys als reine Build-Abhängigkeit wiederherstellen.
