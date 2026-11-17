# NativeViewer

Ein kleiner Windows-Desktop-Viewer für JSON-, XML-, Markdown- und TXT-Dateien.

## Voraussetzungen

- Windows 11
- Für den Ziel-PC ist keine .NET-Installation erforderlich
- Die Anwendung wird als einzelne, selbstenthaltende Windows-x64-EXE veröffentlicht

Die EXE enthält die benötigte .NET-Desktop-Laufzeit. Derzeit ist das Ziel Windows x64; ARM64 wird nicht mit dieser Ausgabe abgedeckt.

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

Im Projektverzeichnis ausführen:

```powershell
dotnet publish NativeViewer\NativeViewer.csproj -c Release -r win-x64 --source https://api.nuget.org/v3/index.json
```

Die weiterzugebende Datei liegt anschließend in `NativeViewer\bin\Release\net10.0-windows\win-x64\publish\NativeViewer.exe`. Diese EXE kann allein auf einen Windows-11-x64-PC kopiert und dort gestartet werden. Es werden keine externen NuGet-Pakete verwendet.

Beim Veröffentlichen lädt das .NET SDK die offiziellen .NET-Laufzeitpakete; diese sind in der EXE enthalten und müssen auf dem Ziel-PC nicht installiert werden.
