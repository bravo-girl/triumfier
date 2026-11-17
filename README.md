# NativeViewer

Ein kleiner Windows-Desktop-Viewer für JSON-, XML-, Markdown- und TXT-Dateien.

## Voraussetzungen

- Windows 11
- Das in Windows enthaltene .NET Framework 4.x
- Keine NuGet-Pakete und keine zusätzlichen Laufzeitbibliotheken

Das Projekt zielt auf .NET Framework 4.5.2; Windows 11 stellt eine kompatible .NET-Framework-Version bereit.

## Funktionen

- Dateien über **Öffnen…** auswählen oder per Drag-and-drop laden
- Datei speichern oder **Speichern unter…**; beim Überschreiben wird nachgefragt
- JSON validieren und einrücken
- XML validieren und formatiert anzeigen
- Markdown mit Überschriften, Listen, Codeblöcken und einfacher Inline-Formatierung darstellen
- Markdown-Ansicht und Quelltext umschalten
- Regex-Suche mit Weitersuchen und Groß-/Kleinschreibungsoption
- Zeilenumbruch und Schriftgröße einstellen
- TXT-Dateien unverändert lesen
- Ungültiges JSON/XML mit verständlichem Status anzeigen und den Originaltext beibehalten

JSON und XML werden standardmäßig formatiert angezeigt. **Speichern** schreibt die angezeigte Formatierung; bei Markdown wird der Quelltext gespeichert. Die Markdown-Darstellung unterstützt bewusst einen häufig genutzten Teilumfang und ist kein vollständiger CommonMark-Parser.

## Build

Die Solution kann mit Visual Studio unter Windows geöffnet und gebaut werden. Es werden keine externen Pakete wiederhergestellt.
