# Application icon

Generated with OpenAI image generation for this project. The application icon is
separate from the dynamic battery indicators in the notification area.

Prompt: A polished Windows 11 application icon: a bright white battery inside
three mint green, sky blue and lavender arcs on a dark navy rounded square.
Simple geometric silhouette, no text, transparent outside the tile.

`app-icon.png` is the original generated artwork. `app-icon.ico` packages it at
16, 20, 24, 32, 40, 48, 64, 128 and 256 pixels for Windows.

Run `scripts/Build-Icon.ps1` on Windows to regenerate the ICO. It normalizes the
transparent outer margins so the mark uses the available taskbar icon space.
