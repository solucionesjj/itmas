# Material Symbols Outlined — procedencia

Fuente de iconos de terceros autoalojada (SPEC-0010 RF-15, ADR-0021). La usa `mat-icon` con la clase
`material-symbols-outlined` y la declara `frontend/src/styles/_fonts.scss`. Se sirve desde el mismo origen (`font-src 'self'`).
`fonts-provenance.spec.ts` comprueba esta tabla contra los archivos servidos.

## Origen
- **Proyecto:** Material Symbols, de Google.
- **Repositorio:** <https://github.com/google/material-design-icons>, carpeta `variablefont`.
- **Versión:** *commit* `10ecc14790612ca8fd164e3f4ffe9c16fc50d5b7` (2026-10-23), el último que cambia esa carpeta al descargar.
- **Descargado:** 2026-11-03.
- **Licencia:** Apache License 2.0 (`LICENSE`, copia exacta de la de origen).

## Archivos de origen
| Archivo | URL | SHA-256 |
|---|---|---|
| `MaterialSymbolsOutlined[FILL,GRAD,opsz,wght].ttf` | `https://raw.githubusercontent.com/google/material-design-icons/10ecc14790612ca8fd164e3f4ffe9c16fc50d5b7/variablefont/MaterialSymbolsOutlined%5BFILL,GRAD,opsz,wght%5D.ttf` | `f7837ba8573d9a7a74931618ced8307bc81f96d49af296bf6c8052c772ed5282` |
| `LICENSE` | `https://raw.githubusercontent.com/google/material-design-icons/10ecc14790612ca8fd164e3f4ffe9c16fc50d5b7/LICENSE` | `58d1e17ffe5109a7ae296caafcadfdbe6a7d176f0bc4ab01e12a689b0499d8bd` |

## Transformación
Una instancia estática del archivo variable (relleno 0, grado 0, tamaño óptico 24 y peso 400), en WOFF2: el archivo variable completo
pesa más de 10 MB (RNF-3). El peso 400 se eligió en la verificación visual, junto a Poppins 300 en el cuerpo. `SOURCE_DATE_EPOCH`
fija la fecha interna de la fuente (la del *commit* de origen) para que la salida sea reproducible.

- Python 3.11.6 en un entorno virtual aislado, con `fonttools==4.59.2` y `brotli==1.1.0` (PyPI). Ninguna de las dos herramientas
  forma parte de ITMas: solo se usan para producir estos archivos, que se versionan ya transformados.

```bash
export SOURCE_DATE_EPOCH=1792721923
python -I -m fontTools.varLib.instancer "MaterialSymbolsOutlined[FILL,GRAD,opsz,wght].ttf" FILL=0 GRAD=0 opsz=24 wght=400 --output=material-symbols-400.ttf
python -I -m fontTools.ttLib.woff2 compress -o material-symbols-outlined-400.woff2 material-symbols-400.ttf
```

## Archivos servidos
| Archivo | SHA-256 |
|---|---|
| `material-symbols-outlined-400.woff2` | `16ec8339b8a81e61b9be9fb903cb5dc35456a4eec8126aebcc3f6401562559fb` |

## Actualizar
Repetir la descarga con el nuevo *commit*, la transformación y esta tabla en el mismo cambio, con revisión de `security`
(`agents/README.md`). Un icono nuevo de ITMas que no exista en esta instancia exige actualizarla.
