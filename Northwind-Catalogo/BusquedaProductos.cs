using System;
using System.Globalization;
using MySql.Data.MySqlClient;

namespace Northwind.Catalogo
{
    // Taller 3: búsqueda por precio y existencias (con su conteo) y búsqueda por nombre.
    internal static class BusquedaProductos
    {
        // Filtros compartidos por la búsqueda y el conteo: así ambos usan exactamente las mismas condiciones.
        private const string FiltrosNumericos =
            "WHERE UnitPrice <= @precioMax AND UnitsInStock >= @stockMin AND Discontinued = 0";

        // ------------------------------------------------------------------
        // Búsqueda por precio máximo y existencias mínimas, seguida de su conteo.
        // ------------------------------------------------------------------
        public static void BuscarPorPrecioYStock(MySqlConnection conexion)
        {
            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine("BÚSQUEDA POR PRECIO MÁXIMO Y EXISTENCIAS MÍNIMAS");
            Console.WriteLine("============================================================");

            // La validación ocurre antes de enviar cualquier consulta.
            decimal precioMax = LeerDecimalNoNegativo("Precio máximo por unidad (use punto decimal): ");
            int stockMin = LeerEnteroNoNegativo("Existencias mínimas (número entero): ");

            int filasMostradas = 0;

            try
            {
                // Para la comparación del taller se probó UnitPrice DESC; para la entrega queda ASC.
                string consulta =
                    "SELECT ProductID, ProductName, UnitPrice, UnitsInStock, QuantityPerUnit " +
                    "FROM Products " + FiltrosNumericos + " " +
                    "ORDER BY UnitPrice ASC, ProductID ASC;";

                {
                    using MySqlCommand comando = new(consulta, conexion);
                    comando.Parameters.Add("@precioMax", MySqlDbType.Decimal).Value = precioMax;
                    comando.Parameters.Add("@stockMin", MySqlDbType.Int32).Value = stockMin;

                    {
                        using MySqlDataReader lector = comando.ExecuteReader();

                        int columnaId = lector.GetOrdinal("ProductID");
                        int columnaNombre = lector.GetOrdinal("ProductName");
                        int columnaPrecio = lector.GetOrdinal("UnitPrice");
                        int columnaStock = lector.GetOrdinal("UnitsInStock");
                        int columnaPresentacion = lector.GetOrdinal("QuantityPerUnit");

                        Console.WriteLine();
                        Console.WriteLine("ID | Producto | Precio | Existencias | Presentación");

                        while (lector.Read())
                        {
                            int id = lector.GetInt32(columnaId);
                            string nombre = lector.GetString(columnaNombre);
                            decimal precio = lector.GetDecimal(columnaPrecio);
                            int stock = lector.GetInt32(columnaStock);

                            // QuantityPerUnit admite NULL: se comprueba antes de leerlo.
                            string presentacion = lector.IsDBNull(columnaPresentacion)
                                ? "Presentación sin registrar"
                                : lector.GetString(columnaPresentacion);

                            Console.WriteLine(id + " | " + nombre + " | " + precio.ToString("F2") +
                                              " | " + stock + " | " + presentacion);
                            filasMostradas++;
                        }
                    } // El lector se libera aquí, antes de ejecutar el conteo.
                }

                // Cero filas no es un error: la consulta se ejecutó bien y no hubo coincidencias.
                if (filasMostradas == 0)
                {
                    Console.WriteLine("No hay productos que cumplan los filtros");
                }

                ContarPorPrecioYStock(conexion, precioMax, stockMin, filasMostradas);
            }
            catch (MySqlException error)
            {
                // Un fallo de conexión o de SQL sí es una excepción y se informa aparte.
                Console.WriteLine("Error de MySQL " + error.Number + ": " + error.Message);
            }
        }

        // Segunda consulta: COUNT(*) con los mismos filtros, parámetros y ExecuteScalar.
        private static void ContarPorPrecioYStock(
            MySqlConnection conexion, decimal precioMax, int stockMin, int filasMostradas)
        {
            string consulta = "SELECT COUNT(*) FROM Products " + FiltrosNumericos + ";";

            {
                using MySqlCommand comando = new(consulta, conexion);
                comando.Parameters.Add("@precioMax", MySqlDbType.Decimal).Value = precioMax;
                comando.Parameters.Add("@stockMin", MySqlDbType.Int32).Value = stockMin;

                long total = Convert.ToInt64(comando.ExecuteScalar());

                Console.WriteLine();
                Console.WriteLine("Total según COUNT(*): " + total);
                if (total == filasMostradas)
                {
                    Console.WriteLine("El conteo coincide con las filas mostradas (" + filasMostradas + ").");
                }
                else
                {
                    Console.WriteLine("ATENCIÓN: el conteo (" + total +
                                      ") no coincide con las filas mostradas (" + filasMostradas + ").");
                }
            }
        }

        // ------------------------------------------------------------------
        // Búsqueda por parte del nombre (productos activos).
        // ------------------------------------------------------------------
        public static void BuscarPorNombre(MySqlConnection conexion)
        {
            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine("BÚSQUEDA POR PARTE DEL NOMBRE");
            Console.WriteLine("============================================================");

            string fragmento = LeerFragmentoNombre();
            int filasMostradas = 0;

            try
            {
                string consulta =
                    "SELECT ProductID, ProductName, UnitPrice, UnitsInStock " +
                    "FROM Products " +
                    "WHERE Discontinued = 0 AND ProductName LIKE @patron " +
                    "ORDER BY ProductName ASC, ProductID ASC;";

                {
                    using MySqlCommand comando = new(consulta, conexion);
                    // Los % forman parte del valor del parámetro; la entrada no se concatena en el SQL.
                    comando.Parameters.Add("@patron", MySqlDbType.VarChar).Value = "%" + fragmento + "%";

                    {
                        using MySqlDataReader lector = comando.ExecuteReader();

                        int columnaId = lector.GetOrdinal("ProductID");
                        int columnaNombre = lector.GetOrdinal("ProductName");
                        int columnaPrecio = lector.GetOrdinal("UnitPrice");
                        int columnaStock = lector.GetOrdinal("UnitsInStock");

                        Console.WriteLine();
                        Console.WriteLine("ID | Producto | Precio | Existencias");

                        while (lector.Read())
                        {
                            int id = lector.GetInt32(columnaId);
                            string nombre = lector.GetString(columnaNombre);
                            decimal precio = lector.GetDecimal(columnaPrecio);
                            int stock = lector.GetInt32(columnaStock);

                            Console.WriteLine(id + " | " + nombre + " | " + precio.ToString("F2") + " | " + stock);
                            filasMostradas++;
                        }
                    }
                }

                if (filasMostradas == 0)
                {
                    Console.WriteLine("No hay productos activos que coincidan con el nombre");
                }
            }
            catch (MySqlException error)
            {
                Console.WriteLine("Error de MySQL " + error.Number + ": " + error.Message);
            }
        }

        // ------------------------------------------------------------------
        // Validación de entradas: se repite la solicitud hasta recibir un valor válido.
        // ------------------------------------------------------------------
        private static decimal LeerDecimalNoNegativo(string mensaje)
        {
            while (true)
            {
                Console.Write(mensaje);
                string? entrada = Console.ReadLine()?.Trim();

                // Solo signo y punto decimal: así "20,5" o "1,000" no se interpretan por error.
                if (!decimal.TryParse(entrada,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out decimal valor))
                {
                    Console.WriteLine("Entrada no válida: escriba un número (por ejemplo 20 o 12.50).");
                    continue;
                }

                if (valor < 0)
                {
                    Console.WriteLine("El valor no puede ser negativo.");
                    continue;
                }

                return valor;
            }
        }

        private static int LeerEnteroNoNegativo(string mensaje)
        {
            while (true)
            {
                Console.Write(mensaje);
                string? entrada = Console.ReadLine()?.Trim();

                // int.TryParse rechaza texto y también valores con decimales como "5.5".
                if (!int.TryParse(entrada, NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out int valor))
                {
                    Console.WriteLine("Entrada no válida: escriba un número entero (sin decimales).");
                    continue;
                }

                if (valor < 0)
                {
                    Console.WriteLine("El valor no puede ser negativo.");
                    continue;
                }

                return valor;
            }
        }

        private static string LeerFragmentoNombre()
        {
            while (true)
            {
                Console.Write("Fragmento del nombre del producto: ");
                string texto = (Console.ReadLine() ?? string.Empty).Trim();

                if (texto.Length == 0)
                {
                    Console.WriteLine("El texto no puede estar vacío. Intente de nuevo.");
                    continue;
                }

                return texto;
            }
        }
    }
}
