# Taller 3: consultas de productos con ADO.NET

**Estudiante:** Jheyson Arauz · **Curso:** Programación III · **Institución:** Oteima

Base utilizada: Northwind reducida (77 productos, 69 activos y 8 descontinuados), cargada con `base-datos/northwind-reducida-mysql.sql`. Las pruebas se hicieron sobre la misma base, sin modificar filas.

---

## 1. Exploración del tutorial (`AspectosAdoNet.cs`)

Primero se ejecutó cada ejemplo original y después se cambió el valor indicado en el comentario. Las variantes quedan conservadas en `AspectosAdoNet.cs` mediante el interruptor `UsarVariantes`: con `false` corre el tutorial original (así queda para la entrega) y con `true` corren las variantes (`LIMIT 5`, `@id = 1`, consulta a `Products`, segundo ID `2` e importe `10`). La consola de Windows muestra los decimales con coma (por ejemplo `21,35`).

### 1.1 Recursos (`MostrarRecursos`)

| | Original | Variante |
|---|---|---|
| Cambio | `LIMIT 2` | `LIMIT 5` |
| Salida | Chai, Chang | Chai, Chang, Aniseed Syrup, Chef Anton's Cajun Seasoning, Chef Anton's Gumbo Mix |

Los estados mostrados fueron: antes de `Open` → `Closed`; después de `Open` → `Open`; con el lector ya liberado → `Open`.

**Explicación:** `LIMIT` solo controla cuántas filas devuelve la consulta, por eso con 5 aparecen cinco nombres. Liberar el lector **no** cierra la conexión (sigue `Open`), así que se puede ejecutar otra consulta después. La conexión solo se libera al terminar su propio bloque. El `using` de las llaves es una *declaración* que llama a `Dispose` al salir del bloque; el `using` del inicio del archivo solo importa nombres de un namespace, por lo que no cumplen la misma función.

### 1.2 Parámetros (`MostrarParametros`)

| Entrada | Resultado | ¿Llega a ejecutar SQL? |
|---|---|---|
| `1` | `Producto encontrado: Chai` | Sí |
| `99999` | `No existe un producto con ese ID.` | Sí (0 filas, `ExecuteScalar` devuelve `null`) |
| `0` | `El ID debe ser un número entero mayor que cero.` | No |
| texto (`abc`) | `El ID debe ser un número entero mayor que cero.` | No |

**Explicación:** `0` y el texto se rechazan en C# con `int.TryParse` y la condición `idProducto <= 0`, antes de la consulta. `1` y `99999` son enteros válidos y llegan al servidor, y que el ID no exista no es un error: simplemente no hay fila.

### 1.3 Tipos y NULL (`MostrarTiposYNulos`)

| `@id` | Salida |
|---|---|
| 5 (original) | `Chef Anton's Gumbo Mix` precio `21,35`; `[Simulado] Precio cero` precio `0,00`; `[Simulado] Sin precio` → `Precio: sin registrar.` |
| 1 | `Chai` precio `18,00` y las dos filas simuladas |
| 99999 | Solo las dos filas simuladas |

**Explicación:** con `99999` desaparece la fila real, porque ningún producto tiene ese ID, pero las filas simuladas se mantienen porque vienen de los `UNION ALL`. Sustituir un precio ausente por cero cambiaría el significado: `0.00` es un precio conocido (gratis) y NULL significa que no se sabe el precio. Por eso se usa `IsDBNull` antes de `GetDecimal`.

### 1.4 Errores de consulta y de conexión

**Error de consulta (`MostrarErrorConsulta`).** Original (observado): se muestra `La tabla indicada no existe.`, código `1146` y detalle `Table 'northwind.tablainexistentedemoadonet' doesn't exist`; después `SELECT 1` devuelve `1`, es decir, la conexión sigue abierta. Variante: al cambiar el nombre por `Products` la consulta funciona, no se lanza ninguna excepción y se muestra `La tabla existe; no se reprodujo el fallo esperado.`. Con el nombre restaurado se reproduce de nuevo el error.

**Error de conexión (`MostrarErrorConexion`).** Se intenta abrir una conexión al puerto 1 de `127.0.0.1`, donde no hay servidor, y falla en `Open()` con `MySqlException`. Salida observada: `Falló la conexión de prueba.`, código `1042` y detalle `Unable to connect to any of the specified MySQL hosts`. Comparación: el fallo de consulta ocurre con la conexión ya abierta, mientras que el de conexión nunca llega a abrirla ni a enviar SQL.

### 1.5 Clave duplicada (`MostrarErrorRestriccion`)

Original (observado): la primera inserción del ID 1 funciona y la segunda lanza el error `1062`, con detalle `Duplicate entry '1' for key 'demoadonetclaves.PRIMARY'`, que el `catch ... when` captura. Variante: antes de la segunda ejecución se cambió el valor del parámetro `@id` a `2`; las dos inserciones funcionan, **no se lanza ninguna excepción y el `catch` no se ejecuta**, porque no hay clave repetida. Con el valor original restaurado vuelve a aparecer el error.

### 1.6 Transacciones (`MostrarTransacciones` / `TransferirSaldo`)

| Importe | Estado inicial | Caso A (Commit) | Caso B (error + Rollback) |
|---|---|---|---|
| 20 (original, observado) | 100 / 50 | 80 / 70 | 80 / 70 |
| 10 (variante) | 100 / 50 | 90 / 60 | 90 / 60 |

Al cambiar el importe se actualizaron también los mensajes que anuncian la transferencia.

**Explicación:** en el caso B el descuento ya se había ejecutado cuando falla el `INSERT` duplicado; `Rollback` lo deshace y los saldos quedan exactamente como estaban después del último `Commit`. Por eso Commit y Rollback dejan los mismos saldos finales en B: el segundo cambio nunca se confirma.

---

## 2. Comparación de orden ascendente y descendente

Entrada: precio máximo `20`, stock mínimo `10` (32 productos).

- **ASC:** 33, 13, 52, 54, 75, 23, 19, 47, 41, 3, 46, 48, 77, 58, 25, 34, 67, 70, 73, 15, 50, 16, 1, 35, 39, 76, 40, 2, 36, 44, 57, 49
- **DESC:** 49, 57, 44, 2, 36, 40, 1, 35, 39, 76, 16, 50, 15, 70, 73, 25, 34, 67, 58, 77, 48, 46, 3, 41, 47, 19, 23, 75, 54, 52, 13, 33

**Conclusión:** los productos encontrados son exactamente los mismos 32 (el orden no cambia el filtro); solo cambia su posición. Los productos con el mismo precio (por ejemplo 25, 34 y 67 a 14,00) conservan el orden por `ProductID` ascendente en ambos casos. Para la entrega se restauró `UnitPrice ASC`.

---

## 3. Casos de prueba

| # | Caso | Entrada | Resultado | ¿Cumple? |
|---|---|---|---|---|
| 1 | Precio máx. 20 y stock mín. 10 | `20` / `10` | 32 filas, todas activas, con precio ≤ 20,00 y stock ≥ 10, ordenadas por precio y luego por ID (de 33 Geitost 2,50 a 49 Maxilaku 20,00). `COUNT(*)` = 32 y el programa informa que coincide con las filas mostradas. | Sí |
| 2 | Sin coincidencias en el catálogo | `0` / `0` | El precio más bajo de un producto activo es 2,50, por lo que no hay filas. Muestra `No hay productos que cumplan los filtros`; `COUNT(*)` = 0. | Sí |
| 3 | Valores muy altos | `1000` / `10000` | 0 filas (el mayor stock de un producto activo es 125). Muestra `No hay productos que cumplan los filtros`, sin error: la consulta se ejecutó correctamente. Una excepción se mostraría como `Error de MySQL <código>: ...`. | Sí |
| 4 | Negativos | precio `-5`; stock `-1` | Se muestra `El valor no puede ser negativo.` y se vuelve a pedir el dato, sin ejecutar SQL. | Sí |
| 5 | Texto en cada entrada numérica | `abc` en precio; `abc` en stock | Se muestra `Entrada no válida...` y se vuelve a pedir; el programa no se cierra. | Sí |
| 6 | Stock con decimales | `5.5` | `int.TryParse` falla y se muestra `Entrada no válida: escriba un número entero (sin decimales).` | Sí |
| 7 | Límites inclusivos | `18.00` / `39` (producto 1, **Chai**, 18,00 y 39 unidades) | Chai aparece en los resultados (posición 13 de 15); también aparece Genen Shouyu (15,50 / 39). `COUNT(*)` = 15, igual que las filas mostradas. Confirma que `<=` y `>=` incluyen los límites. | Sí |
| 8 | Presentación NULL | Se cambió temporalmente `QuantityPerUnit` por `CAST(NULL AS CHAR) AS QuantityPerUnit` en el SELECT | El catálogo no tiene ninguna presentación NULL (0 filas). Con el CAST temporal, todas las filas muestran `Presentación sin registrar`. Se restauró la consulta normal y no se modificó ninguna fila. | Sí |
| 9 | Nombre con coincidencias | `Choc` | 2 filas activas: `48 · Chocolade · 12,75 · 15` y `19 · Teatime Chocolate Biscuits · 9,20 · 25`. Ambas contienen el fragmento y el orden por nombre es correcto (`Chocolade` antes que `Teatime...`). | Sí |
| 10 | Nombre sin coincidencias | `zzzzsincoincidencias999` | Muestra `No hay productos activos que coincidan con el nombre`, sin tratarlo como error. | Sí |
| 11 | Nombre vacío | Enter sin escribir; luego solo espacios | Ambos casos muestran `El texto no puede estar vacío. Intente de nuevo.` (`Trim()` deja la cadena vacía) y se vuelve a pedir, sin ejecutar SQL. | Sí |

---

## 4. Explicación

### Asignación de parámetros

En mis consultas no pego lo que escribe el usuario dentro del texto SQL. Pongo marcadores como `@precioMax`, `@stockMin` y `@patron`, y después les asigno el valor con `comando.Parameters.Add(...)`, indicando el tipo (`Decimal`, `Int32`, `VarChar`). Así MySQL recibe el valor como un dato y no como parte de la instrucción, y nadie puede meter SQL propio en la consulta. En la búsqueda por nombre, los `%` del `LIKE` los agrego al valor del parámetro (`"%" + fragmento + "%"`), no al SQL.

### Recursos que libera cada `using`

- El `using` del **comando** libera el `MySqlCommand` cuando termina de usarse. No cierra la conexión.
- El `using` del **lector** cierra el `MySqlDataReader`. Mientras el lector está abierto la conexión está ocupada, por eso el `COUNT(*)` lo ejecuto después de que el lector se cierra.
- El `using` de la **conexión** (en `Program.cs`) cierra la conexión al terminar.

Lo bueno de `using` es que libera todo aunque ocurra un error.

### Cero coincidencias, columna NULL y excepción

- **Cero coincidencias:** la consulta funciona bien, pero no devuelve filas. `Read()` da `false` desde el principio y el programa solo muestra un mensaje. No es un error.
- **Columna NULL:** sí hay una fila, pero una columna no tiene valor (por ejemplo `QuantityPerUnit`). Hay que revisar `IsDBNull` antes de leerla; si no, falla. Por eso muestro `Presentación sin registrar`.
- **Excepción (`MySqlException`):** algo salió mal de verdad, como un error de conexión o de SQL. No hay resultado y se maneja con un `catch`, mostrando el código y el mensaje del error.

### Seguridad

En `Program.cs` dejé los marcadores `TU_USUARIO` y `TU_CONTRASENA`, y ningún archivo ni commit tiene una contraseña real.
