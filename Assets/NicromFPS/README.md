# Nicrom FPS

In Unity `Assets/Scenes/Nicrom FPS.unity` öffnen und Play drücken.
Die Szene ist eine Kopie von NHP_ThreeBiomes mit dem lokalen Akila-FPS-Spieler.

Enthalten: Charaktersteuerung, FPS-Kamera mit Waffen-Kamera, HUD, Gesundheit,
Sturmgewehr, Pistole, Munition, Pause-Menü und die Game-/Spawn-Manager des Frameworks.
Die Flugkamera ist in dieser Szene deaktiviert. Die Originalszene bleibt erhalten.

## Steuerung

- WASD: Bewegung; Maus: Umsehen
- Shift: Sprinten; Leertaste: Springen; C: Ducken
- Linke Maustaste: Schießen; rechte Maustaste: Zielen
- R: Nachladen; 1/2 oder Mausrad: Waffe wechseln
- Q/E: Lehnen; F: Interagieren; Escape: Pause
- F: anvisierten grauen Würfel aus bis zu 3 Metern einsammeln
- I: Ressourceninventar ein-/ausblenden (die Bewegung bleibt aktiv)
- T: Baumodus ein-/ausschalten
- Linksklick im Baumodus: kostenlosen Wand-Bauplan setzen
- R im Baumodus: Wand drehen
- F an einem Bauplan: einen Würfel hinzufügen
- Rechtsklick im Baumodus: abbrechen

## Mauern Schicht für Schicht bauen

T zeigt eine transparente Wandreihe aus 5 Würfeln (2,75 Meter breit).
R dreht den Plan um 90 Grad. Mit Linksklick auf einer ebenen freien Stelle setzen.
Weiß bedeutet gültig, Rot bedeutet blockiert oder zu uneben. Der Plan ist kostenlos.

Den Bauplan aus bis zu 3 Metern Entfernung anvisieren und F drücken: Jeder Tastendruck
verbaut einen Inventarwürfel von links nach rechts. Nach 5 Würfeln erscheint automatisch
die nächste transparente Schicht. Die Anzeige zeigt die Schicht und den Fortschritt.
Zum nächsten Bauplatz darf der Spieler höchstens 3,5 Meter entfernt sein.

Fertige Würfel sind grau und haben Kollision. Unfertige sind transparent und begehbar.
Bei blockiertem Bauplatz wird nichts abgezogen. Halbfertige Wände bleiben erhalten,
während du Material sammeln gehst. T oder Rechtsklick beendet nur die bewegliche
Vorschau. Gesetzte Wände bleiben bei Spieler-Respawns erhalten, werden aber beim
Szenenneustart zurückgesetzt. Speichern und Abriss sind nicht enthalten.

## Würfel und Ressourceninventar

`Cube Spawner and Inventory` in der Szene erzeugt beim Spielstart bis zu 24 graue
Würfel an freien Terrainstellen im Umkreis von 25 Metern um den Spieler.
Anzahl, Radius, Reichweite und Material sind im Inspector einstellbar.
Würfel verschwinden beim Einsammeln und werden in 8 Inventarplätzen mit je maximal
20 Würfeln gestapelt. Die Anzeige oben rechts zeigt die Gesamtzahl.
Das Inventar bleibt während eines Spieler-Respawns erhalten; ein neuer Szenenstart
setzt es zurück und erzeugt neue Würfel. Ein Speicherstand auf Festplatte ist nicht enthalten.
Wände blockieren das Einsammeln. Bei vollem Inventar bleiben Würfel liegen.

Nach externen Szenenänderungen Play stoppen und die Szene erneut öffnen bzw. Unitys
Neuladen der geänderten Szene zulassen.

Inventarlogik prüfen: `dotnet run --project Tests/NicromInventory/NicromInventory.csproj`.

## Anpassung

### Dungeon-Gate

In `Nicrom FPS` erstellt `Dungeon Travel Manager` ein blaues Test-Gate ungefähr
8–13 Meter vom Spielerstart entfernt, sobald eine freie Terrainstelle gefunden wurde.
Durchgehen lädt `Nicrom Dungeon` additiv und erzeugt dort einmal pro Spielsitzung
einen Cube-Dungeon mit acht gewünschten Räumen. Das blaue Gate im ersten Raum führt
zurück. Der Dungeon liegt unterhalb der Außenwelt bei Y = -1000 und bleibt geladen.

Die vorhandene FPS-Figur wird versetzt; Leben, Waffen, Munition und Würfelinventar
bleiben erhalten. Gebaute Wände bleiben bis zum Ende der Sitzung bestehen. Die
Außenwelt-Wellen, Goblins und Goblin-Geräusche pausieren im Dungeon; ihre
Minikarten-Markierungen werden ausgeblendet. Der Dungeon hat eigene Goblin-Wellen (siehe unten).
Nach Tod erfolgt der vorhandene Außenwelt-Respawn. Eine neue Spielsitzung erzeugt
beim ersten Eintritt ein neues Layout; es gibt noch keine Speicherung über Neustarts.

Ankunftsstellen werden auf Boden und Platz für die Spieler-Kapsel geprüft. Bei einem
blockierten Ziel bleibt die Figur auf ihrer bisherigen Seite. Für einen erneuten
Versuch den Gate-Bereich verlassen und wieder betreten. Ladehinweise erscheinen im HUD.

`DungeonGate` kann auf spätere Gate-Modelle gesetzt werden; `returnsToWorld` kennzeichnet
den Rückweg. `DungeonTravelManager.Travel(player, returning)` steuert den Übergang.
`FirstPersonController.ResetMotionAfterTeleport()` verwirft Fall- und Plattformbewegung.

Generator und Editor-Werkzeuge liegen unter `Assets/CubeDungeon`. Das Daedalus-Paket
liegt inklusive Lizenz unter `Packages/com.dillondrum70.daedalus-dungeon-generator`.
Project 15 wird zur Laufzeit und beim Öffnen des Projekts nicht benötigt.

Prüfungen im Editor: `Tools > Cube Dungeon > Generator prüfen`,
`Tools > Cube Dungeon > Rampenwände prüfen` und
`Tools > Nicrom FPS > Dungeon-Ankunft prüfen`.

Geprüft am 29.09.2026: C#-Build ohne Fehler, alle übernommenen Asset-Verweise,
Generator-Prüfungen sowie 7.864 Wandanschlüsse und Rampendurchgänge. Sichere
FPS-Ankunft und erreichbares Rückweg-Gate bestehen mit Seeds 42, 7 und 1337.
Die Unity-Geometrieprüfungen liefen in einem separaten Testprojekt ohne Fenster.
Berichte: `Logs/CubeDungeonValidation.txt`, `Logs/CubeDungeonWalls.txt` und
`Logs/NicromDungeonPlacement.txt`.

### Bewegung und Darstellung im Dungeon

Im Dungeon erhält die FPS-Kapsel mindestens 50 Grad Steigungsgrenze für die
45-Grad-Rampen. Auf begehbaren Rampen bleibt die Stufenhilfe von 0,3 Metern aktiv,
damit die Figur die Kante zur nächsten Bodenfläche überquert. Beim Verlassen oder
Tod werden die vorherigen Einstellungen der Außenwelt wiederhergestellt.

Die statischen Dungeon-Würfel werden für die Darstellung nach Material und kleinen
räumlichen Bereichen zusammengefasst. Würfelobjekte, Materialien, Rampen und alle
Collider bleiben erhalten; das Umschalten der Decken bleibt möglich. In der
Prüfszene sank die Anzahl aktiver Würfel-Renderer von 14.605 auf 1.019.

Prüfungen: `Tools > Nicrom FPS > Dungeon-Rampen begehen prüfen` und
`Tools > Nicrom FPS > Dungeon-Darstellung prüfen`.
Alle 24 Auf-/Abstiege mit echter CharacterController-Kollision bestehen bei
30/60/144 Bewegungsschritten pro Sekunde in vier Richtungen.
Geometrie, Materialzuordnung, Kollisionen und Deckenumschaltung der zusammengefassten
Darstellung wurden geprüft. Der Test fand zusätzlich fehlende Mesh-Freigabe im
Editor; dafür wurde ExecuteAlways ergänzt. Der erneute Unity-Teststart nach dieser
Korrektur wurde nicht freigegeben, daher ist die abschließende Freigabeprüfung offen.
Eine Messung der Bildrate und des Mausgefühls im echten Spiel steht noch aus.

Noch im echten Playmodus zu prüfen: sichtbare Beleuchtung und Beschriftung,
Hin- und Rückweg mit Waffen und Inventar, wiederholter Eintritt mit unverändertem
Layout, Wellenpause samt Wiederaufnahme, Bauen im Dungeon und Tod/Respawn.

### Goblin-Gegner

Die Szene enthält `Goblin Enemies`. Welle 1 bringt 10 Goblins, Welle 2 bringt 20,
Welle 3 bringt 30 usw. Sind alle besiegt, folgt nach 5 Sekunden die nächste Welle.
Goblins erscheinen an freien, erreichbaren Terrainstellen in einem Ring von
15–30 Metern um die aktuelle Spielerposition. Fehlende freie Stellen werden erneut
gesucht; ausstehende Gegner werden nicht als besiegt gezählt.
Die Wegfindung wird im Hintergrund erstellt und bei Bewegung um mehr als 25 Meter
aktualisiert. Der Bereich umfasst auch die noch lebenden Gegner.

Oben steht die aktuelle Welle mit Gesamtzahl, verbleibenden Gegnern und Countdown.
Die vorhandene Minikarte unten zeigt lebende Goblins als rote Markierungen;
entfernte Gegner bleiben am Kartenrand sichtbar. Die Kartenreichweite beträgt 50 Meter.

- 100 Lebenspunkte mit sichtbarem Lebensbalken; mit den vorhandenen Waffen verwundbar.
- Verfolgen den Spieler sofort ab dem Spawn, auch ohne Sichtkontakt und über 45 Meter hinaus.
- 12 Schaden pro Treffer; Trefferprüfung während des Schwungs, keine Angriffe durch Wände.
- Stehen, Laufen, Angriff und Tod verwenden die Animationen des Goblin01-Packs.
- Tote Goblins greifen nicht mehr an und werden nach 15 Sekunden entfernt.
- Fertige Mauerwürfel blockieren die Wegfindung; transparente Baupläne bleiben durchlässig.
- Spawnradius, Wellenpause, Gesundheit, Schaden und Tempo sind am Szenenobjekt einstellbar.
- Während Pause oder Spielertod werden keine weiteren Gegner erzeugt; nach Respawn geht es weiter.

Das Goblin-Pack selbst bleibt unverändert. Die Integration verwendet einen eigenen
Animationscontroller und ein URP-Material mit den Texturen des Packs. Die Navigation
folgt dem Spieler; die Wellen laufen lokal ohne Netzwerksynchronisierung.
Build, Lebenslogik und Asset-Verknüpfungen sind geprüft; ein visueller Playmode-Test
der Goblins steht noch aus.

`Prefabs/Nicrom FPS Player.prefab` ist eine eigene Kopie des Framework-Spielers.
Startwaffen werden dort im Inventory unter `startItems` festgelegt.
Der SpawnManager verwendet dieselbe Kopie für Respawns.

Der Startpunkt liegt bei der bisherigen Flugkamera. `NicromTerrainSpawn` sucht beim
Start und bei Respawns in bis zu 24 Metern Umkreis eine freie Terrainstelle mit
höchstens 35 Grad Neigung. Die Höhe wird über den TerrainCollider ermittelt.
Wasser mit Collider auf Layer Water wird berücksichtigt. Wasserflächen ohne Collider
müssen beim manuellen Platzieren der Spawnpunkte berücksichtigt werden.

Die Szene ist als erste Szene in den Build Settings eingetragen.
Dies ist die lokale FPS-Integration; ein Multiplayer-Lobby-Ablauf ist nicht eingerichtet.

Geprüft: C#-Build einschließlich NicromTerrainSpawn, lokale Szenen-/Prefab-Verweise
und externe Referenzen des Spielers. Ein visueller Playmode-Test steht noch aus.


### Goblin-Wellen im Dungeon

Beim ersten Eintritt startet nach 10 Sekunden Welle 1. Die ersten fünf Wellen
enthalten 6, 8, 13, 18 und 24 Goblins. Höchstens 24 leben gleichzeitig; Nachschub
kommt nach Abschüssen. Spawnabstand: 2 Sekunden mal 0,95 je weiterer Welle,
Untergrenze 0,08 Sekunden. Zwischen vollständig besiegten Wellen liegen 10 Sekunden.
Goblins haben 150 Leben in Welle 1, bis Welle 9 je 100 mehr, danach je 10 Prozent
mehr (ganzzahlig abgerundet). Schaden und Tempo kommen aus dem Außenwelt-Encounter.

Das ist eine eigene BO1-inspirierte Umsetzung anhand des öffentlich einsehbaren
Mod-Skripts https://github.com/CabConMods/EnCoReV8_Black-Ops-1-Zombie/blob/master/maps/_zombiemode.gsc;
keine Zusage einer unveränderten Originalspiel-Balance. Ohne Punkte, Barrikaden,
kaufbare Türen oder zusätzliche Munitionsversorgung.

DungeonGoblinNavigation erzeugt eine eigene Navigationsfläche für 1,4 m hohe Goblins
mit 0,32 m Radius und 50 Grad Steigungsgrenze. Spawnpunkte liegen in Räumen, mindestens
8 m vom Spieler und 4 m vom Rückweg-Gate entfernt. Kapselplatz, Kamerasicht und
vollständiger Laufweg werden vor jedem Spawn geprüft. Wege bis 30 m werden bevorzugt.
Bei blockierten Punkten erfolgt nach 0,5 Sekunden ein neuer Versuch. PlacedCube
aktiviert bereits NavMeshObstacle-Carving für fertige Wände. Festhängende Gegner
werden nach 30 Sekunden ohne Fortschritt unsichtbar an einen sicheren Punkt versetzt;
Leben und Wellenzugehörigkeit bleiben erhalten.

DungeonGoblinEncounter steuert DungeonWaveProgress und verwendet GoblinFactory
mit denselben Assets wie draußen. GoblinEnemy, GoblinVoice und GoblinMinimapMarker
unterscheiden die beiden Welten. Das HUD zeigt die Dungeon-Welle und alle noch
verbleibenden Gegner einschließlich Nachschub. Draußen pausiert die Dungeon-Welle;
im Dungeon pausieren die Außenwelt-Wellen. Bei Tod im Dungeon werden dessen Gegner
entfernt und die nächste Runde dort beginnt wieder bei Welle 1. Layout und Bauten
bleiben erhalten. Vorbereitung erfolgt vor dem Teleport; fehlende Navigation oder
sichere Punkte verhindern den Eintritt mit Ladehinweis und erlauben einen neuen Versuch.

Geprüft am 29.09.2026:
- C#-Build: 0 Fehler, 2 bestehende Warnungen.
- Reine Tests: Wellenzahlen, Lebensskalierung, Spawnabstand, Limit 24, doppelte Tode,
  pausierte Timer, Tod-Reset; bestehende Außenwelt-/Inventar-/Bautests bestanden.
- Unity ohne Fenster im separaten Testprojekt: Seeds 42/7/1337, jeweils alle acht
  Räume beidseitig erreichbar, 641/607/661 freie Spawnpunkte, Gate-Abstand und
  Ablehnung eines nachträglich belegten Spawnpunkts geprüft.
- Bestehende 24 FPS-Rampenbegehungen und sichere Ankunft/Gate-Platzierung bestanden.
  Bericht: Logs/NicromDungeonGoblins.txt. Editor-Menü:
  Tools > Nicrom FPS > Dungeon-Goblin-Navigation prüfen.

Noch offen im tatsächlichen Playmode: Kameraabhängigkeit der verdeckten Spawns,
Verfolgung mit animierten Goblins, Waffen-/Kontaktschaden, Sounds, Minimap,
mehrfacher Gate-Wechsel samt Erhalt von Gegnerleben, Tod/Respawn und dynamisch
vollständig zugemauerte Wege. Die Navigationstests prüfen Pfade und Kollisionen;
sie ersetzen diese visuellen Spieltests nicht.


### L1-Handgeste und Vorbereitung für spätere Magie

NicromHandGesture hängt am Nicrom FPS Player-Prefab und findet dessen untergeordnetes
Inventory. L1/Left Shoulder streckt bei ausgerüstetem Gewehr oder Pistole die Hand in
0,18 Sekunden nach vorne. Solange L1 gehalten wird, bleiben Hand und Kamehameha-Strahl
ausgestreckt. Beim Loslassen endet der Strahl und der Arm wird weich zurückgeführt.
Die rechte Hand und die Waffenwurzel werden nicht verändert.
Die frühere L1-Belegung für den Visiermodus wurde aus Controls.inputactions und dem
zugehörigen generierten Controls.cs entfernt; die übrigen Visier-Bindings bleiben.

Die linke Armkette wird nach der Waffenanimation ausgerichtet. Finger nutzen die offene
Referenzpose aus dem gemeinsamen FPS Hands-Avatar (LeftHandOpenPose), da die
Mesh-Bindepose eine gekrümmte Griffhaltung enthält. Vor dem nächsten Animator-Schritt
wird die vorherige Korrektur entfernt. Tod, Waffenwechsel, Wegwerfen, Nachladen und
Dungeon-Übergänge brechen die Geste ab; eine Spielpause hält ihren Zeitablauf an.

NicromHandGesture.MagicOrigin liefert den Anker auf der Handfläche. PalmExtended meldet
das Ausstrecken einmal pro regulär durchlaufener Geste, nachdem die Pose gesetzt wurde.
+Z des Ankers zeigt aus der Handfläche, +Y zu den Fingern. Am höchsten Punkt der Geste
erzeugt L1 den Kamehameha-Prefab in Weltkoordinaten am Handanker. Der Strahl folgt der
Hand und verursacht alle 0,1 Sekunden Schaden am ersten getroffenen Gegner. Die
Standardrate beträgt 2500 Lebenspunkte pro Sekunde. Eigene Spieler-, Waffen- und
Wasser-Layer werden ignoriert. Die ältere separate K-Tasten-Komponente ist im
Spieler-Prefab deaktiviert. Der importierte Heavy-Beam-Sound läuft während L1 gehalten
wird als Schleife und stoppt zusammen mit dem Strahl. Der aktive Strahl verbraucht
gleichmäßig 200 Mana pro Sekunde und endet automatisch, sobald kein Mana mehr übrig ist.

Geprüft am 30.09.2026: Zeitablauf bei 30/60/144 FPS, Pause, erneutes Auslösen und Abbruch;
L1-Eingabe im Unity Input System; echte Gewehr-/Pistolenmodelle im separaten Unity-Playmode
mit offenen Fingern, nach vorne gerichtetem Handanker, unveränderter rechter Hand und
vollständiger Rückstellung der Gelenke. Gerenderte Posen visuell kontrolliert.
Der Modelltest normalisiert die Armgröße für seine Vorschaukamera. Ein kompletter
Durchlauf in der Nicrom-Spielszene mit realem Controller, Nachladen und Gate-Wechsel
ist noch offen. Testharness: Tests/UnityDungeonValidationProject/Assets/Editor/HandGestureValidation.cs.
Berichte und Modellvorschauen: Logs/HandGesture/.

Unity-Referenzen für die Implementierung: [Mesh.bindposes](https://docs.unity3d.com/ScriptReference/Mesh-bindposes.html)
und [SkinnedMeshRenderer.BakeMesh](https://docs.unity3d.com/ScriptReference/SkinnedMeshRenderer.BakeMesh.html).

### Mana-Kugeln

Beim Tod erzeugt jeder Goblin blaue, emissive Mana-Kugeln. Je 20 volle maximale
Lebenspunkte entsteht eine eigene Kugel; jede Kugel gibt beim Einsammeln 10 Mana.
Die Kugeln fallen auf den Boden und schweben
mit ihrer Unterseite ungefähr fünf Zentimeter darüber. Erst ab 2,5 Metern fliegen sie
magnetisch zum unteren Körper der Spielfigur und werden automatisch eingesammelt. Sie
fliegen dabei nicht zur First-Person-Kamera oder ins Gesicht.

ManaWallet speichert den Mana-Gesamtwert ohne Obergrenze und zeigt ihn links oben an.
Es gibt keinen Maximalbalken und das Einsammeln verbraucht momentan kein Mana. Zum
Schutz der Bildrate verwenden höchstens 24 herumliegende Kugeln gleichzeitig ein
Punktlicht; alle weiteren bleiben durch das blaue Emissionsmaterial sichtbar.

# Build und Startmenü

Der Build startet in `Assets/Scenes/Main Menu.unity`. Dort führen **SPIELEN** in
`Nicrom FPS.unity` und **BEENDEN** zum Spielende. Maus, Tastatur und Controller
können die Menüschaltflächen bedienen. Der Dungeon bleibt als dritte Build-Szene
enthalten, weil das Gate ihn während des Spiels additiv lädt.

Die Build-Reihenfolge ist bereits eingerichtet unter **File → Build Profiles →
Scene List**:

1. Main Menu
2. Nicrom FPS
3. Nicrom Dungeon

Wähle im Build Profiles-Fenster dein Zielsystem und drücke **Build**. Die
importierten Framework-Demos und `SampleScene` gehören nicht zum Spiel-Build.

