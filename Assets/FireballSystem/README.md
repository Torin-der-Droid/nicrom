# Anime-Feuerball

In Unity `Tools > Anime Fireball > Open Demo` wählen und Play drücken.

- Leertaste oder Bildschirmbutton: Feuerball abfeuern
- `AnimeFireballLauncher.Fire()`: Feuerball aus eigenem Spielcode starten
- Das Projektil fliegt vorwärts und erzeugt bei einer Kollision automatisch den Einschlagseffekt.

Das System liegt vollständig unter `Assets/FireballSystem` und ist vom Magiekreis unabhängig.
Für den vorgesehenen Leuchteindruck benötigen Zielkamera und Szene HDR, Post Processing und Bloom.

Die Flammenkrone, die Energiewirbel im Schweif und der zusätzliche Einschlagsburst laufen über
Unity Visual Effect Graph. Das klassische Particle System ergänzt Rauch und einzelne Glutpartikel.

## Steuerung in Nicrom FPS

Mit **D-Pad links** wird der Feuerball aus der aktiven FPS-Kamera abgefeuert. Als Tastatur-Ersatz
funktioniert **Pfeil links**. Das Projektil verursacht bei einem getroffenen Goblin 300 Schaden und
erzeugt bei jeder Kollision die vorhandene Einschlags-Explosion. Abklingzeit: 0,7 Sekunden.
