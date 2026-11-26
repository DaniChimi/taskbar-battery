# TaskbarBattery

Mostra il livello di batteria dei dispositivi wireless nell'area di notifica di Windows, di qualsiasi marca.

[English](README.md)

## Come funziona

- **Qualsiasi dispositivo Bluetooth.** Windows legge già la batteria di molti dispositivi Bluetooth (il servizio GATT
  standard Battery Service) e ne conserva l'ultimo valore come proprietà del dispositivo. TaskbarBattery legge quella
  proprietà: mouse, tastiere, controller e cuffie che comunicano la batteria via Bluetooth funzionano senza software
  del produttore.
- **Dispositivi Logitech, più dettagli.** Tramite il protocollo HID++ di Logitech l'app legge anche lo stato di carica
  e un livello più aggiornato. Tiene aperto il canale HID++ del dispositivo e ne attende gli avvisi sulla batteria:
  collegare o staccare il cavo si vede subito, senza interrogazioni periodiche.
- Vengono mostrati solo i dispositivi connessi.

## L'icona nell'area di notifica

Un'icona sola, per il dispositivo scelto o, in automatico, per quello connesso con la batteria più bassa. Tre stili,
dal menu del tasto destro:

- **Batteria**: il contorno della batteria di Windows, riempito in base al livello; in carica, la batteria con il fulmine.
- **Percentuale**: il numero; in carica, le cifre si riempiono dal basso verso l'alto, a scatti.
- **Dispositivo e barra**: l'icona del dispositivo con una barra del livello; in carica, la barra si anima verso il pieno.

Con **Un'icona per dispositivo** ogni dispositivo connesso ha la sua icona. Il livello diventa rosso alla soglia di
batteria scarica (20% di default). Un clic sull'icona apre il pannello con tutti i dispositivi connessi: un
indicatore a cinque tacche, la percentuale, lo stato di carica (Logitech), il produttore e da quanto è connesso.

## Impostazioni

Si aprono dall'ingranaggio in alto a destra nel pannello, oppure con **Impostazioni…** nel menu del tasto destro (che
contiene anche **Aggiorna ora** ed **Esci**). Le modifiche valgono subito.

- **Icona nell'area di notifica**: stile dell'icona, un'icona per dispositivo, quale dispositivo segue l'icona singola.
- **Notifiche**: batteria scarica (una volta per ciclo di scarica), carica completa (Logitech), dispositivo connesso
  o disconnesso, ciascuna attivabile, con soglia di batteria scarica dal 10% al 30% e una notifica di prova.
- **Dispositivi**: rinomina un dispositivo o nascondilo dall'icona, dal pannello e dalle notifiche.
- **Generale**: avvio con Windows (avvio automatico dell'utente) e log diagnostico,
  `%LOCALAPPDATA%\TaskbarBattery\log.txt`, che registra solo cambiamenti ed errori.

Provato con: Logitech MX Master 4 (Bluetooth), Xbox Wireless Controller (Bluetooth).

## Requisiti

Windows 10 o 11. .NET Framework 4.x è già installato.

## Compilazione

```
build.bat
```

Usa il compilatore C# incluso in Windows; non servono SDK né IDE.

## Risoluzione dei problemi

```
TaskbarBattery.exe --probe | Out-String
```

elenca i dispositivi che comunicano la batteria, le collection HID Logitech e la risposta HID++.
Le impostazioni sono in `%APPDATA%\TaskbarBattery\settings.txt`.

## Licenza

MIT
