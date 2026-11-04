# Poppins — procedencia

Fuente de terceros autoalojada (SPEC-0010 RF-15, ADR-0021). Se sirve desde el mismo origen (`font-src 'self'`) y la declara
`frontend/src/styles/_fonts.scss`. `fonts-provenance.spec.ts` comprueba esta tabla contra los archivos servidos.

## Origen
- **Proyecto:** Poppins, de Indian Type Foundry (<https://github.com/itfoundry/Poppins>), en la publicación oficial de Google Fonts.
- **Repositorio:** <https://github.com/google/fonts>, carpeta `ofl/poppins`.
- **Versión:** *commit* `8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5` (2026-03-12), el último que cambia esa carpeta al descargar.
- **Descargado:** 2026-11-03.
- **Licencia:** SIL Open Font License 1.1 (`OFL.txt`, copia exacta de la de origen). Su aviso de copyright no declara ningún
  *Reserved Font Name*, así que el subconjunto (una modificación) puede conservar el nombre «Poppins».

## Archivos de origen
| Archivo | URL | SHA-256 |
|---|---|---|
| `Poppins-Light.ttf` | `https://raw.githubusercontent.com/google/fonts/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/poppins/Poppins-Light.ttf` | `650ba57fa99d12ec40c31ccfb680be656be4497fbe14164617d67e32ffe9cd46` |
| `Poppins-Regular.ttf` | `https://raw.githubusercontent.com/google/fonts/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/poppins/Poppins-Regular.ttf` | `7e65201e9b79159e2300267cc885e16c8dcef2424cdfa09a29bfb0980a94a7ba` |
| `Poppins-Medium.ttf` | `https://raw.githubusercontent.com/google/fonts/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/poppins/Poppins-Medium.ttf` | `90373e7d838d32468438fc3e152dca0bdb12edcab99ea639f158790b1ba1fd05` |
| `Poppins-SemiBold.ttf` | `https://raw.githubusercontent.com/google/fonts/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/poppins/Poppins-SemiBold.ttf` | `d3bf1bdaf0550e83da9ac0b1d1d9fe6db086835a83aa28578e609a394b9a0286` |
| `Poppins-Bold.ttf` | `https://raw.githubusercontent.com/google/fonts/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/poppins/Poppins-Bold.ttf` | `983676516167748b74de6f4771fb384c664fd913acb8b471122ecacf5da5ea6c` |
| `OFL.txt` | `https://raw.githubusercontent.com/google/fonts/8b0a1d0f5983c89bc2b93f1b5fb55f9e252744b5/ofl/poppins/OFL.txt` | `6be04893d770899a015649c7aa3b582f871b272f8747a92b78b17c3e5c8b2573` |

## Transformación
Un subconjunto por peso con los rangos latino y latino extendido de Google Fonts, en WOFF2. La salida es reproducible: con las mismas
entradas y versiones, las sumas de abajo se repiten.

- Python 3.11.6 en un entorno virtual aislado, con `fonttools==4.59.2` y `brotli==1.1.0` (PyPI). Ninguna de las dos herramientas
  forma parte de ITMas: solo se usan para producir estos archivos, que se versionan ya transformados.

```bash
LATIN='U+0000-00FF,U+0131,U+0152-0153,U+02BB-02BC,U+02C6,U+02DA,U+02DC,U+0304,U+0308,U+0329,U+2000-206F,U+20AC,U+2122,U+2191,U+2193,U+2212,U+2215,U+FEFF,U+FFFD'
LATINEXT='U+0100-02BA,U+02BD-02C5,U+02C7-02CC,U+02CE-02D7,U+02DD-02FF,U+0304,U+0308,U+0329,U+1D00-1DBF,U+1E00-1E9F,U+1EF2-1EFF,U+2020,U+20A0-20AB,U+20AD-20C0,U+2113,U+2C60-2C7F,U+A720-A7FF'
python -I -m fontTools.subset Poppins-Light.ttf --unicodes="$LATIN,$LATINEXT" --layout-features='*' --flavor=woff2 --output-file=poppins-300.woff2
python -I -m fontTools.subset Poppins-Regular.ttf --unicodes="$LATIN,$LATINEXT" --layout-features='*' --flavor=woff2 --output-file=poppins-400.woff2
python -I -m fontTools.subset Poppins-Medium.ttf --unicodes="$LATIN,$LATINEXT" --layout-features='*' --flavor=woff2 --output-file=poppins-500.woff2
python -I -m fontTools.subset Poppins-SemiBold.ttf --unicodes="$LATIN,$LATINEXT" --layout-features='*' --flavor=woff2 --output-file=poppins-600.woff2
python -I -m fontTools.subset Poppins-Bold.ttf --unicodes="$LATIN,$LATINEXT" --layout-features='*' --flavor=woff2 --output-file=poppins-700.woff2
```

## Archivos servidos
| Archivo | SHA-256 |
|---|---|
| `poppins-300.woff2` | `6b2cdd4ef0053ea80b91095c401bd9281877329eda098dc85cec242b6f468d75` |
| `poppins-400.woff2` | `26c6d44e53219e3e73734989b55daf3b82b074a499e383438f3ae0e3f2fb29ae` |
| `poppins-500.woff2` | `2933599f8174cbf4f4b714df7d877904cc2167f8e525fa0fd756d3fd303c365f` |
| `poppins-600.woff2` | `c3ae157cca450722863cdd30a942ead33dbf88d5fa91a36df4d93fe76d3aa159` |
| `poppins-700.woff2` | `8083f26e8ff4f761d62d7fad55fc72ada08104eebbb777151aac957e630a4736` |

## Actualizar
Repetir la descarga con el nuevo *commit*, la transformación y esta tabla en el mismo cambio, con revisión de `security`
(`agents/README.md`).
