# assets

these files aren't in the repo (they're Mojang's / third-party). drop your own here
with these EXACT names and the app builds + runs:

| file | what / where |
|---|---|
| `Monocraft.ttf` | the minecraft-style font. free (OFL): https://github.com/IdreesInc/Monocraft |
| `now_playing.png` | toast panel background. the 160×32 minecraft "toast" sprite (dark rounded panel) |
| `music_notes.png` | note particle sheet. 16×128 = eight stacked 16×16 note frames, white-on-transparent |
| `In.wav` | toast slide-IN sound (minecraft `ui.toast.in`) |
| `Out.wav` | toast slide-OUT sound (minecraft `ui.toast.out`) |
| `icon.ico` | app + tray icon (any .ico) |

notes:
- `now_playing.png` gets nine-sliced with a 4px inset, so keep the border in the outer
  ~4px and a flat center.
- `music_notes.png` frames are alpha-masked and tinted, so the shape just needs to be
  opaque-on-transparent (grayscale is fine).
- `.ogg` works too for the sounds if you'd rather (the app checks common types), but
  `In.wav`/`Out.wav` are what it looks for first.
