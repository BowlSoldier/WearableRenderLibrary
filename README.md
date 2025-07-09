# WearableRenderLibrary

Vintage Story mod. Adds modding utility classes to allow dynamic rendering of wearable items, such as armor and/or clothing.

More details and instructions can be found on the ModDB page: https://mods.vintagestory.at/show/mod/26189



If attempting to run this from source code, the build does not output files in the right structure. The `OverhaulLibCompat.dll` file must be placed inside a `native/` folder in your mod's folder to work. The folder structure should look like:

```
- modinfo.json
- WearableRenderLibrary.dll
- native/
  - OverhaulLibCompat.dll
```

