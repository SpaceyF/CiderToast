[![forthebadge](https://forthebadge.com/badges/powered-by-black-magic.svg)](https://forthebadge.com) [![forthebadge](data:image/svg+xml;base64,PHN2ZyBkYXRhLXYtODY1ODNhZmQ9IiIgeG1sbnM9Imh0dHA6Ly93d3cudzMub3JnLzIwMDAvc3ZnIiB3aWR0aD0iMTQ4LjU4MzMzNTg3NjQ2NDg0IiBoZWlnaHQ9IjM1IiB2aWV3Qm94PSIwIDAgMTQ4LjU4MzMzNTg3NjQ2NDg0IDM1IiBjbGFzcz0iYmFkZ2Utc3ZnIj48ZGVmcyBkYXRhLXYtODY1ODNhZmQ9IiI+PCEtLS0tPjwhLS0tLT48IS0tLS0+PC9kZWZzPjxyZWN0IGRhdGEtdi04NjU4M2FmZD0iIiB3aWR0aD0iMTA3LjMzMzMzNTg3NjQ2NDg0IiBoZWlnaHQ9IjM1IiBmaWxsPSIjNmY0YWQ5Ii8+PHJlY3QgZGF0YS12LTg2NTgzYWZkPSIiIHg9IjEwNy4zMzMzMzU4NzY0NjQ4NCIgd2lkdGg9IjQxLjI1IiBoZWlnaHQ9IjM1IiBmaWxsPSIjNzUxMGZlIi8+PCEtLS0tPjx0ZXh0IGRhdGEtdi04NjU4M2FmZD0iIiB4PSI1My42NjY2Njc5MzgyMzI0MiIgeT0iMTcuNSIgZHk9IjAuMzVlbSIgZm9udC1zaXplPSIxMiIgZm9udC1mYW1pbHk9IlJvYm90bywgc2Fucy1zZXJpZiIgZmlsbD0iI0ZGRkZGRiIgdGV4dC1hbmNob3I9Im1pZGRsZSIgbGV0dGVyLXNwYWNpbmc9IjIiIGZvbnQtd2VpZ2h0PSI2MDAiIGZvbnQtc3R5bGU9Im5vcm1hbCIgdGV4dC1kZWNvcmF0aW9uPSJub25lIiBmaWxsLW9wYWNpdHk9IjEiIGZvbnQtdmFyaWFudD0ibm9ybWFsIiBzdHlsZT0idGV4dC10cmFuc2Zvcm06IHVwcGVyY2FzZTsiPk1BREUgV0lUSDwvdGV4dD48IS0tLS0+PHRleHQgZGF0YS12LTg2NTgzYWZkPSIiIHg9IjEyNy45NTgzMzU4NzY0NjQ4NCIgeT0iMTcuNSIgZHk9IjAuMzVlbSIgZm9udC1zaXplPSIxMiIgZm9udC1mYW1pbHk9Ik1vbnRzZXJyYXQsIHNhbnMtc2VyaWYiIGZpbGw9IiNGRkZGRkYiIHRleHQtYW5jaG9yPSJtaWRkbGUiIGZvbnQtd2VpZ2h0PSI5MDAiIGxldHRlci1zcGFjaW5nPSIyIiBmb250LXN0eWxlPSJub3JtYWwiIHRleHQtZGVjb3JhdGlvbj0ibm9uZSIgZmlsbC1vcGFjaXR5PSIxIiBmb250LXZhcmlhbnQ9Im5vcm1hbCIgc3R5bGU9InRleHQtdHJhbnNmb3JtOiB1cHBlcmNhc2U7Ij5DIzwvdGV4dD48IS0tLS0+PC9zdmc+)](https://forthebadge.com)

# CiderToast 🎵

a minecraft-style "now playing" toast for windows. start a song and a lil minecraft
toast slides into the corner with the title, artist, album, length and album art,
and it colors itself to match the album. works with basically ANY player (cider,
spotify, firefox, zen, whatever) because it reads windows' own media info. no api
keys, no logins, no setup.

![toast](docs/toast.png)

## what it does

- slides in a minecraft toast every time the song changes
- shows **title / artist / album / length** + pixelated album art
- **themes itself to the album art**, the gold "Now Playing" header and the floating
  note particles pull the album's main color
- plays the real minecraft toast slide in/out sounds
- **universal**, anything that shows up in the windows media popup works (cider,
  spotify, browsers, etc). no per-app setup
- tray icon + a little minecraft-styled settings window (corner, duration, scale,
  toggles, album-art color on/off)
- "start with windows" toggle

## grab it

**just wanna run it?** download the self-contained `.exe` from
[Releases](../../releases). no .NET install, no extra files, just double-click.
(first launch takes a sec while it unpacks, then it's instant. windows smartscreen
might pop up since it's unsigned, so hit More info then Run anyway.)

**wanna build it?** you need the [.NET 10 SDK](https://dotnet.microsoft.com/download).
also grab the assets (see below), then:

```
dotnet build -c Release
```

or make the standalone single-file exe:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

## assets (read this if you're building)

the minecraft textures + sounds aren't in this repo, they're Mojang's, not mine to
hand out. drop your own into `assets/` with these exact names (see
[`assets/README.md`](assets/README.md) for details):

| file | what it is |
|---|---|
| `Monocraft.ttf` | the font ([download, it's free/OFL](https://github.com/IdreesInc/Monocraft)) |
| `now_playing.png` | the toast panel background (160×32 minecraft toast) |
| `music_notes.png` | note sprite sheet (16×128, eight 16×16 frames) |
| `In.wav` / `Out.wav` | the toast slide in/out sounds |
| `icon.ico` | app/tray icon |

## how it works

it listens to the Windows **System Media Transport Controls** (SMTC), the same thing
that powers the little media popup on your keyboard's play/pause key. that's why it
works with everything and needs no token. built in **WPF / .NET 10**. the toast is a
borderless topmost window with a nine-sliced panel, the album color gets pulled by
grabbing the dominant vibrant hue from the thumbnail.

tweak stuff in `config.json` (or the settings window): `toastSeconds`, `marginLeft/Top`,
`borderScale`, `minWidth`, `showArtwork`, `colorAccent`, `corner`.

## license

[MIT](LICENSE) for my code. the minecraft assets you supply are Mojang's and covered
by their terms, not this license.
