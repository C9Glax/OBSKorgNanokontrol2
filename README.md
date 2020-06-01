# nanoKontrol2OBS

Translates MIDI-Events from your KORG nanoKontrol2 to the [obs-websocket](https://github.com/Palakis/obs-websocket)

## Getting Started

Download latest [Release](https://github.com/C9Glax/OBSKorgNanokontrol2/releases), extract the Archive, and run Executable.
Connect to [obs-websocket](https://github.com/Palakis/obs-websocket)

### Prerequisites

A working [obs-websocket](https://github.com/Palakis/obs-websocket) installation.

### Config

The default config is my own preference. Different people might want different setups. So here you go.

The Binds-config has to be in the same folder as the executable and has to be named `config.xml`.

The Schema is `config.xsd`.

`inputs` can be	`slider`,`dial` or `button`.

`slider`- and `dial`-Actions can be:
```
setobsvolume(<source>)
setwindowsvolume(<source>)
```
`button`-Actions can be:
```
obsmute(<source>)
switchscene(<index>)
windowsmute(<source>)
previoustrack()
nexttrack()
playpause()
startstopstream()
savereplay()

```
`<source>`can be:
```
desktop1
desktop2
mic1
mic2
mic3
```
`<index>` is a zero-based integer.

Example:
`<button midicontrolid="48" action="windowsmute(desktop1)" />`

## Built With

* [NAudio](https://github.com/naudio/NAudio) - MIDI Controls
* [AudioSwitcher](https://github.com/xenolightning/AudioSwitcher) - Windows Audio Controls
* [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) - Because that is what everyone uses for JSON
* [websocket-sharp](https://github.com/sta/websocket-sharp) - To connect to [obs-websocket](https://github.com/Palakis/obs-websocket)
* [OBS-Websocket-Sharp](https://github.com/C9Glax/OBS-Websocket-Sharp) - To connect to [obs-websocket](https://github.com/Palakis/obs-websocket)

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details
