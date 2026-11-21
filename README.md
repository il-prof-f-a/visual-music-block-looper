# Visual Music Block Looper

[Leggi in italiano](README-it.md)

Visual Music Block Looper turns printed music cards on a tabletop into a live MIDI performance. A
webcam reads the cards and their colored bodies, the app projects a guide back onto the table, and
MIDI notes and transport events are sent to a digital audio workstation (DAW).

## Why use it?

The project makes musical timing visible and physical. Instead of entering every note in a piano
roll, players arrange cards in a shared grid and hear the result immediately. It is designed for
hands-on composition, group workshops, music education, and interactive music experiments. It can
be used without programming knowledge once it is installed and calibrated; building it from source
requires a Windows development setup.

## Who is it for?

- Music teachers and students exploring rhythm, pitch, duration, and arrangement.
- Musicians and workshop leaders who want a tangible, collaborative sequencer.
- Makers and developers interested in webcam vision, projected interfaces, and MIDI.

This is an experimental instrument and learning tool, not a replacement for a DAW.

## How it works

The playing surface is a **6-row × 16-cell grid**. The QR code identifies a note, chord, setting, or
command. A card's colored body determines its track and, for notes, how long the note lasts. The
webcam reads the table, the app prepares a MIDI score, and a dedicated MIDI worker sends it to the
DAW. The projector displays the grid and the detected notes so players can see what the software
read.

The app starts in **Slave** mode, following MIDI clock and transport from the DAW. Use the
**Master / Slave** button in the control window to let the app provide the clock instead.

## Requirements

- Windows 10 or 11, 64-bit.
- .NET Framework 4.7.2 or a compatible later .NET Framework runtime.
- To build from source: a .NET SDK with support for .NET Framework 4.7.2 Windows Forms projects
  and its targeting pack.
- For a physical setup: a webcam, a projector connected as a second display, printed cards, and a
  suitable table or projection surface.
- For MIDI: [loopMIDI](https://www.tobias-erichsen.de/software/loopmidi.html) and a DAW. Ableton
  Live is the documented setup; other DAWs may work if they support MIDI notes and clock routing.

There is currently no packaged installer in this repository. Build the application from source as
described below.

## Install and build

Open PowerShell in the repository folder:

```powershell
dotnet restore src/VisualMusicBlockLooper.sln
dotnet build src/App/App.csproj -c Release
```

Run the generated application:

```powershell
& .\src\App\bin\Release\net472\VisualMusicBlockLooper.App.exe
```

The build copies `mappatura-default.json` beside the executable. Keep that file there: it contains
the default MIDI ports and note mapping. The app stores its camera and table settings in
`configurazione.json` and the saved calibration in `calibrazione.txt`, also beside the executable.
Keep these files when moving an existing installation.

## Connect MIDI to Ableton Live

The supplied `mappatura-default.json` uses two loopMIDI ports:

| Port | Direction | Purpose |
|---|---|---|
| `VMBL` | App → Ableton | Notes and, in Master mode, clock and transport. |
| `VMBL-DAW` | Ableton → app | Clock and transport when the app is in Slave mode. |

1. Start loopMIDI and create ports named exactly `VMBL` and `VMBL-DAW`.
2. In Ableton's **Preferences → Link, Tempo & MIDI**, enable **Track** on the `VMBL` input.
3. To use the default Slave mode, enable **Sync** on the `VMBL-DAW` output and start playback in
   Ableton. The app follows Live's clock and transport.
4. To use Master mode, enable **Sync** on Ableton's `VMBL` input and start playback from the app.

If you rename either port, update `mappatura-default.json` beside the executable and restart the
app. The detailed hardware guide is in [docs/prova-hardware.md](docs/prova-hardware.md).

## First tabletop session

1. Connect the projector as a second display and extend the desktop. Show a plain white full-screen
   image on the projected surface.
2. Position the webcam so the projected area and the cards are in frame, in focus, and evenly lit.
3. Launch the app, choose the webcam and projector display, then press **Calibra**. Click the
   centers of the 12 projected targets in numerical order. The calibration is tied to the webcam
   and its resolution; a saved CAL3 calibration can be reused with the same setup.
4. Print the cards at **100% scale**. Place them in the grid: columns are time subdivisions, rows
   are musical lines. A note card's colored body sets its duration.
5. Start Ableton playback in Slave mode, or switch to Master and start playback from the app. Use
   the projected guide to check which cells the camera has recognized.

The [hardware guide](docs/prova-hardware.md) gives the full camera, calibration, and Ableton
procedure.

## Card sets

| Cards | Contents |
|---|---|
| [Chromatic notes](assets/cartoncini-note/LEGGIMI.md) | Colored note cards for the six tracks. |
| [Chords and channels](assets/cartoncini-accordi/LEGGIMI.md) | 96 chord cards, including a treble-clef set one octave higher; MIDI channel cards are in `assets/cartoncini-canale/`. |
| [Drums](assets/cartoncini-batteria/LEGGIMI.md) | General MIDI drum cards in two visual styles, plus Jamstix section cards. |
| [Table settings](assets/cartoncini-setting/LEGGIMI.md) | Time signatures, linked rows, and A–B loop settings. |

Print cards at their actual size and avoid scaling them to fit the page. Their QR codes and body
colors are both part of the input format.

## Configuration and limitations

- `assets/config/mappatura-default.json` is copied beside the executable at build time. It sets
  MIDI ports, velocity, channels, and default note numbers.
- `configurazione.json` stores camera, analysis, and per-row settings; `calibrazione.txt` stores
  the camera-to-table calibration.
- MIDI channel cards apply while playback is stopped. Instrument (`ST`) cards are recognized and
  saved, but do not yet load instruments in Ableton.
- Camera focus, lighting, projection geometry, and the saved calibration affect recognition. Check
  the projected grid lines before using the table.

## Development

The solution contains five projects: `Core`, `Vision`, `Midi`, `App`, and `Tests`. Build the full
solution with:

```powershell
dotnet build src/VisualMusicBlockLooper.sln -c Release
dotnet test src/Tests/Tests.csproj -c Release
```

See [docs/architettura.md](docs/architettura.md) for the component overview and
[docs/esecuzione-midi.md](docs/esecuzione-midi.md) for the MIDI execution model.
