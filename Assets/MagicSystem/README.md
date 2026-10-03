# Goldener Magiekreis

## Demo öffnen

In Unity `Tools > Magic Circle > Open Demo` wählen und Play drücken.

- Leertaste: Kreis weich aktivieren oder deaktivieren
- Energie-Regler: `SetEnergy(float)` testen
- Rune 1 bis 12: einzelne Rune aktivieren und aufblitzen lassen
- Runen löschen: Aktivzustände zurücksetzen
- Zentralstrahl auslösen: dünner Lichtstrahl, explosionsartige Ausbreitung und stückweises Abreißen

## Im eigenen Spiel verwenden

`Assets/MagicSystem/Prefabs/MagicCircle.prefab` in eine Szene ziehen. Das Prefab startet
deaktiviert. Über `MagicCircleController` stehen `ActivateCircle()`, `DeactivateCircle()`,
`SetEnergy(float)`, `ActivateRune(int)` und `TriggerCenterBurst()` zur Verfügung. Runen verwenden
Indizes von 0 bis 11. `ActivateCircle()` löst den Zentralstrahl automatisch mit aus.

Für denselben Leuchteindruck muss die Zielkamera HDR und Post Processing verwenden und die Szene
ein globales Volume mit Bloom enthalten. Die Demo enthält eine passende Konfiguration.

## Aufbau

Die fünf Bildschichten besitzen denselben Mittelpunkt und dieselbe Größe. Ihre Pivot-Objekte drehen
sich um die lokale Y-Achse. Kleine Höhenunterschiede und feste Sorting Orders verhindern Z-Fighting.
Zwölf zusätzliche Vollbild-Overlays verwenden eine Winkelmaske im Shader, damit jede Rune aus der
gemeinsamen Runentextur einzeln hervorgehoben werden kann.

`MagicCircleVFX` verwendet nur noch GoldenDust am Ring. Funken, Partikel aus dem Zentrum
und Kernpuls sind deaktiviert; der Kern behält eine konstante Größe. Der Zentralstrahl besteht aus
einem additiven Mesh ohne herausfliegende Partikel.

## Goblin-Instant-Kill

Im Spiel löst **D-Pad nach oben** die Fähigkeit aus. Als Tastatur-Ersatz funktioniert ebenfalls
**Pfeil nach oben**. Der Magiekreis erscheint am anvisierten Punkt bis maximal 80 Meter Entfernung.
Ohne Oberflächentreffer erscheint er 25 Meter vor der Kamera. Beim explosionsartigen Aufweiten
werden alle schadbaren Objekte im sichtbaren Radius von 3 Metern sofort getötet. Die eigene
Spielfigur ist davon ausgenommen. Die flachen Kreisebenen werden beim Erzeugen in ein feines
24×24-Gitter unterteilt. Jeder Gitterpunkt wird auf den Boden projiziert, damit sich der Effekt
auch an unebenes Gelände anschmiegt und nicht darin verschwindet.
