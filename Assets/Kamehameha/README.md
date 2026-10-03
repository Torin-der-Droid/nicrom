# Kamehameha – Unity Particle Systems

Nach dem Import erzeugt der Editor das Prefab und die Demo automatisch.
Demo: `Tools > Kamehameha > Open Demo`, dann Play. Leertaste oder Abfeuern startet neu.
In der Demo ist automatische Wiederholung aktiv.

`Kamehameha.prefab` in die eigene Szene ziehen, den Ursprung an die Hand setzen und
die lokale Z-Achse in Schussrichtung drehen. `Fire()` startet den Effekt,
`StopEffect()` beendet ihn sofort. Optional ein Target zuweisen.
Charge Duration, Beam Duration, Beam Length und Beam Width sind im Inspector einstellbar.
Charge Duration ist standardmäßig 0: Der Strahl startet sofort ohne Aufladephase.
Es gibt keine Ladekugel.

Der Effekt besteht aus sechs Particle Systems: Kern, Hülle, einströmenden Ladepartikeln,
Energiefluss, Trefferfunken und einer Schockwelle. Der Strahl wird aus überlappenden
Billboard-Partikeln aufgebaut. Es werden kein VFX Graph und kein LineRenderer verwendet.
Der Controller positioniert die Strahlpartikel im Play Mode; die normale Particle-System-
Vorschau allein zeigt deshalb den vollständigen Effekt nicht.

Treffer benötigen einen 3D-Collider. Hit Layers sollte die eigene Spielfigur ausschließen.
Ohne Treffer läuft der Strahl bis zur maximalen Länge, ohne Einschlagsfunken.
Die Demo nutzt URP-Bloom; in eigenen Szenen Post Processing an der Kamera aktivieren
und ein Volume mit Bloom hinzufügen. Der Shader funktioniert auch ohne Bloom.
Das Prefab wiederholt standardmäßig nicht automatisch und verursacht keinen Schaden.

## Spielersteuerung in Nicrom FPS
Am Nicrom FPS Player hängt NicromKamehameha. Taste K feuert sofort in Richtung Fadenkreuz, Reichweite 35 Meter. Der Effekt stoppt bei Pause, Tod und Dungeon-Übergängen. Der Strahl ist ein visueller Effekt ohne Schaden. Offset und Reichweite sind am Spieler-Prefab einstellbar.

