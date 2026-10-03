# Cube Dungeon

## Start

1. `Tools > Cube Dungeon > Beispielszene öffnen` wählen oder `Scenes/CubeDungeonDemo.unity` öffnen.
2. In der Hierarchy **Cube Dungeon Generator** auswählen.
3. Im Inspector **Generieren** oder **Neuer Seed + Generieren** drücken.
4. Play starten: Du startest im ersten Raum in der **First-Person-Perspektive**.
   **WASD** bewegt dich, die **Maus** steuert den Blick, **Leertaste** springt und **Esc**
   gibt den Mauszeiger frei. Ein Linksklick im Spiel fängt ihn wieder ein.
   Die Augenhöhe ist am Spieler einstellbar (Standard: 1,6 m). Die Capsule bleibt als
   Collider und Schatten erhalten und ist beim Spielen für die Kamera ausgeblendet.

Einen weiteren Generator legst du mit **GameObject > Cube Dungeon > Generator** an.
Er verwendet die mitgelieferten Bauteile automatisch. Die bisherigen Daedalus-Szenen bleiben verfügbar.

## Einstellungen

- **Raster:** Anzahl der 5-Meter-Zellen in X, Höhe und Z.
- **Raumanzahl / Raumgröße:** Gewünschte Räume und ihre Ausdehnung in Zellen. Die Räume sind jeweils eine Ebene hoch.
- **Seed:** Gleicher Seed und gleiche Einstellungen ergeben dasselbe Layout.
- **Zusätzliche Verbindungen:** Zusätzliche Gänge neben dem verbindenden Grundgerüst.
- **Decken anzeigen:** Blendet ausschließlich die Decken-Renderer um. Die Collider bleiben aktiv. Würfel, die zugleich einen Wandanschluss zwischen Etagen schließen, bleiben sichtbar. In der Beispielszene sind Decken zur Übersicht ausgeblendet.
- **Beim Spielstart erzeugen:** Erzeugt den Dungeon auch im Play Mode und im gebauten Spiel. Ausschalten, um einen manuell bearbeiteten Dungeon unverändert zu spielen.

Jeder Block ist ein eigenes GameObject mit MeshRenderer und BoxCollider. Ein Cube ist 1 × 1 × 1 Meter groß. Räume und Gänge haben Böden, Wände und Decken aus Cubes; Höhenwechsel bestehen aus 45-Grad-Planes mit MeshCollider. Die Beispielszene enthält einen Capsule-Spieler mit CharacterController, WASD-Bewegung, Mausblick und Sprung.

Unter dem Generator liegen **Cube Dungeon** mit den sichtbaren Bauteilen und **Daedalus Layout** mit der vorhandenen Layout-Logik. Der Generator darf verschoben und gedreht werden; seine Skalierung und die seiner Eltern müssen `(1, 1, 1)` bleiben.

**Generieren ersetzt alle bisher erzeugten Bauteile dieses Generators einschließlich manueller Änderungen.** `Dungeon löschen` betrifft nur dessen eigene Ausgabe. Andere Generatoren und die ursprünglichen Dungeon-Assets werden dabei nicht gelöscht.

Die grauen Materialien verwenden URP/Lit. Farben lassen sich in `Materials` ändern, einzelne Bauteile in `Prefabs`. Es werden gemeinsame Materialien verwendet, keine Materialinstanzen pro Cube. Große Raster können viele tausend einzelne GameObjects erzeugen.

Daedalus verteilt Räume zufällig. Bei wenig Platz können weniger Räume entstehen oder Gangverbindungen scheitern; der Inspector meldet das. Dann Raumanzahl reduzieren, das Raster vergrößern oder einen anderen Seed wählen.

## Skriptzugriff

`CubeDungeon.CubeDungeonGenerator` bietet `Generate()`, `GenerateNewSeed()`, `Clear()` und `SetCeilingsVisible(bool)`. Einstellungen stehen auf der Komponente. `GeneratedRoot`, `BlockCount`, `RampCount`, `GeneratedRoomCount`, `FailedConnections` und `LastMessage` liefern das Ergebnis.

## Prüfungen

`Tools > Cube Dungeon > Generator prüfen` führt die Editor-Prüfungen in einer separaten Testszene aus. Ergebnisse stehen in `Logs/CubeDungeonValidation.txt`.

`Tools > Cube Dungeon > Rampenwände prüfen` prüft die Wandanschlüsse zwischen Etagen,
ihre Collider und die freien Rampendurchgänge mit mehreren Seeds. Der Bericht steht in
`Logs/CubeDungeonWalls.txt`. Für einen Batch-Lauf: `CubeDungeon.Editor.CubeDungeonWallValidation.Run`.

Für automatisierte Unity-Läufe gibt es `CubeDungeon.Editor.CubeDungeonValidation.Run`, `BuildPlayer`, `RenderPreview` und `PlayModeCheck`. `PlayModeCheck` benötigt den zusätzlichen Startparameter `--cube-dungeon-smoke-test` und wird ohne `-quit` gestartet. Der Test beendet seinen Batch-Editor selbst. Der gleiche Parameter aktiviert die Prüfung im Development-Player; normale Spielstarts führen keine Prüfungen aus.

Die Daedalus-Algorithmen stammen aus dem lokal eingebundenen MIT-lizenzierten Paket. Die Lizenzvermerke sind dort erhalten.
