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
- **Ricevitori, cavi e dongle Logitech.** I dispositivi collegati a un ricevitore Unifying, Bolt o LIGHTSPEED (fino a
  sei per ricevitore), quelli via cavo USB e le cuffie con il proprio dongle non hanno un livello di batteria in
  Windows: l'app li legge in HID++ attraverso il ricevitore, oppure con il protocollo proprio delle cuffie più recenti.
  I ricevitori segnalano subito quando un dispositivo si connette o si disconnette; per i dispositivi che comunicano
  solo una tensione la percentuale si ricava da una curva di scarica Li-ion.
- **Auricolari Nothing e CMF.** Windows non riceve da loro il livello di batteria, quindi l'app lo legge dal canale di
  controllo degli auricolari (il servizio Bluetooth usato dall'app Nothing X): auricolare sinistro, destro e custodia,
  ciascuno con il suo stato di carica, in un'unica scheda. Il canale resta aperto e sono gli auricolari a comunicare i
  cambiamenti. Viene riconosciuto ogni modello che espone quel servizio, qualunque sia il nome.
- Vengono mostrati solo i dispositivi connessi.

## L'icona nell'area di notifica

Un'icona sola, per il dispositivo scelto o, in automatico, per quello connesso con la batteria più bassa. Tre stili,
dal menu del tasto destro:

- **Batteria**: il contorno della batteria di Windows, riempito in base al livello; in carica, la batteria con il fulmine.
- **Percentuale**: il numero; in carica, le cifre si riempiono dal basso verso l'alto, a scatti.
- **Dispositivo e barra**: l'icona del dispositivo con una barra del livello; in carica, la barra si anima verso il pieno.

Con **Un'icona per dispositivo** ogni dispositivo connesso ha la sua icona. Il livello diventa rosso alla soglia di
batteria scarica (20% di default). Un clic sull'icona apre il pannello con tutti i dispositivi connessi: un
indicatore a cinque tacche, la percentuale, lo stato di carica (Logitech), il produttore e da quanto è connesso. Per
gli auricolari ci sono tre colonne, sinistro, custodia e destro, ciascuna con la sua barra; una parte che non
comunica resta visibile in grigio con «--%». L'icona segue l'auricolare più scarico.

## Impostazioni

Si aprono dall'ingranaggio in alto a destra nel pannello, oppure con **Impostazioni…** nel menu del tasto destro (che
contiene anche **Aggiorna ora** ed **Esci**). Le modifiche valgono subito.

- **Icona nell'area di notifica**: stile dell'icona, un'icona per dispositivo, quale dispositivo segue l'icona singola.
- **Notifiche**: batteria scarica (una volta per ciclo di scarica), carica completa (Logitech, auricolari), dispositivo connesso
  o disconnesso, ciascuna attivabile, con soglia di batteria scarica dal 10% al 30% e una notifica di prova.
- **Dispositivi**: rinomina un dispositivo o nascondilo dall'icona, dal pannello e dalle notifiche.
- **Generale**: avvio con Windows (avvio automatico dell'utente) e log diagnostico,
  `%LOCALAPPDATA%\TaskbarBattery\log.txt`, che registra solo cambiamenti ed errori.

## Dispositivi supportati

✅ supportato e provato · 🟡 implementato, non ancora provato · ⚪ sconosciuto · ❌ non supportato

**Logitech**

| Dispositivo o collegamento | Stato | Note |
|---|---|---|
| MX Master 4 (Bluetooth) | ✅ | Livello, stato di carica e aggiornamenti in tempo reale via HID++. |
| Altri dispositivi Bluetooth LE con HID++ (MX Keys, MX Anywhere, Lift, Ergo, Pebble…) | 🟡 | Stesso canale dell'MX Master 4. |
| Dispositivi su ricevitore Bolt | 🟡 | Vengono controllati tutti e sei gli slot. |
| Dispositivi su ricevitore Unifying | 🟡 | Solo i dispositivi HID++ 2.0. |
| Mouse e tastiere su ricevitore LIGHTSPEED (serie G) | 🟡 | Molti comunicano una tensione: la percentuale è una stima. |
| Dispositivi via cavo USB | 🟡 | Uniti allo stesso dispositivo sul ricevitore quando se ne conosce l'id wireless. |
| Cuffie con il proprio dongle (G533, G733, G PRO X…) | 🟡 | Basate sulla tensione, percentuale stimata. |
| Cuffie più recenti (PRO X 2 LIGHTSPEED e simili) | 🟡 | Protocollo proprio, non HID++. |
| Vecchi dispositivi Unifying solo HID++ 1.0 | ❌ | Usano un registro della batteria diverso. |
| Vecchi dispositivi Bluetooth Classic (per esempio K380) | ⚪ | Windows ne riceve di rado la batteria; HID++ non verificato. |

**Nothing e CMF**

| Dispositivo | Stato | Note |
|---|---|---|
| CMF Buds 2 | ✅ | Sinistro, destro e custodia, con stato di carica. |
| Altri auricolari che usano l'app Nothing X (CMF Buds, Buds Pro, Buds Pro 2, Nothing Ear, Ear (a), Ear (2), Ear (stick)) | 🟡 | Riconosciuti dal servizio di controllo; stesso formato della batteria. |
| Nothing Ear (1) | 🟡 | Intestazione dei messaggi un po' diversa, che il parser accetta. |
| Cuffie over-ear (CMF Headphone Pro, Nothing Headphone (1)) | ⚪ | Probabilmente lo stesso canale, ma la batteria potrebbe usare altri codici. |
| CMF Watch | ❌ | Protocollo diverso. |

**Altre marche**

| Dispositivo | Stato | Note |
|---|---|---|
| Dispositivi Bluetooth con il servizio batteria standard (provato con Xbox Wireless Controller) | ✅ | Livello da Windows; nessuno stato di carica. |
| Dispositivi Bluetooth di cui Windows non mostra la batteria | ❌ | Servirebbe il protocollo del produttore. |

`TaskbarBattery.exe --probe` mostra cosa vede l'app per ogni dispositivo: è il modo più rapido per verificarne uno 🟡 o ⚪.

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

elenca i dispositivi che comunicano la batteria, le collection HID Logitech, la risposta HID++, quanto riportano gli
auricolari Nothing / CMF connessi e i dispositivi Logitech trovati su ricevitori, cavi e dongle.
Le impostazioni sono in `%APPDATA%\TaskbarBattery\settings.txt`.

## Licenza

MIT
