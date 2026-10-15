# Third-party notices

## Fonts

GameShift bundles two typefaces. Both are licensed under the SIL Open Font License, Version 1.1, which allows use, embedding and redistribution with the app. The license texts are kept next to the font files in `src/GameShift.App/Assets/Fonts/` and are embedded in the executable together with them.

| Font | Used for | Copyright | License text |
|---|---|---|---|
| [Onest](https://github.com/googlefonts/onest) | interface text | Copyright 2021 The Onest Project Authors | `Assets/Fonts/Onest-OFL.txt` |
| [Unbounded](https://github.com/googlefonts/unbounded) | page titles, mode names, app name in the sidebar | Copyright 2022 The Unbounded Project Authors | `Assets/Fonts/Unbounded-OFL.txt` |

The font files come from the [google/fonts](https://github.com/google/fonts) repository (`ofl/onest`, `ofl/unbounded`). WPF does not apply variable-font axes, so the bundled files are static instances cut from the variable fonts at the weights the app uses (Onest Regular and SemiBold, Unbounded SemiBold). No glyphs or names were changed. The fonts are not sold on their own.

License: https://openfontlicense.org (SIL OFL 1.1).
