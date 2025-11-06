using System.Data;
using System.Data.SQLite;
using System.Diagnostics.CodeAnalysis;

namespace MiniAPI.Db
{
    public partial class Sql
    {
        #region Basic DB Manipulation
        /// <summary>
        /// A queue that is protected by Monitor.
        /// </summary>
        private static bool lockQuery = false;

        /// <summary>
        /// Führt einen SQL-Befehl gegen die Datenbank aus.
        /// </summary>
        /// <param name="query">SQL-Abfrage</param>
        /// <param name="args">Parameter für SQL-Abfrage</param>
        /// <returns>true = mindestens eine Zeile in der Datenbank wurde eingefügt, geändert oder gelöscht.</returns>
        internal static async Task<bool> NonQueryAsync(string dbPath, string query, Dictionary<string, object>? args)
        {

            while (lockQuery)
            {
                Thread.Sleep(100);
            }

            lockQuery = true;

            try
            {
                // if (!CheckDbFileAsync(dbPath, false).Result) return false; //böse! erzeugt Zirkel beim neu erstellen der Datenbank

                using var connection = new SQLiteConnection("Data Source=" + dbPath);
                //SQLitePCL.Batteries.Init();

                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = query;
                if (args != null && args.Count > 0)
                {
                    foreach (string key in args.Keys)
                    {
                        command.Parameters.AddWithValue(key, args[key]);
                    }
                }

                return await command.ExecuteNonQueryAsync() > 0;
            }

            catch (Exception ex)
            {
                string argsShow = "";
                if (args != null)
                    foreach (string key in args.Keys)
                    {
                        argsShow += "\r\n" + key + "\t'" + args[key] + "'";
                    }

                Program.Message(Message.MessageType.Alert, $"Datenbankfehler. Bitte Administrator benachrichtigen. {ex}");
                //Worker.LogError("SqlNonQuery(): " + query + argsShow + "\r\n" + ex.GetType() + "\r\n" + ex.Message + "\r\n" + ex.InnerException + "\r\n");
                return false;
            }
            finally
            {
                // Ensure that the lock is released.
                lockQuery = false;
            }
        }
         
        /// <summary>
        /// Fragt Tabellen-Daten mit einem SQL-Befehl gegen die Datenbank ab.
        /// </summary>
        /// <param name="query">SQL-Abfrage</param>
        /// <param name="args">Parameter für SQL-Abfrage</param>
        /// <returns>Tabelle mit dem Ergebnis der Abfrage.</returns>                   
        internal static DataTable SelectDataTable(string dbPath, string query, Dictionary<string, object>? args)
        {
            DataTable myTable = new();

            if (!CheckDbFileAsync(dbPath).Result) return myTable;

            try
            {
                using var connection = new SQLiteConnection("Data Source=" + dbPath);
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = query;


                if (args != null && args.Count > 0)
                {
                    foreach (string key in args.Keys)
                    {
                        command.Parameters.AddWithValue(key, args[key]);
                    }
                }

                try
                {
                    using (var reader = command.ExecuteReader())
                    {
                        //Mit Schema einlesen
                        // myTable = reader.GetSchemaTable(); //soll langsam sein
                        myTable.Load(reader);

                    }

                    return myTable;
                }
                catch
                {

                    //Worker.LogWarning("SelectDataTable(): Hinweis: Abfrage hat Schema nicht eingehalten:\r\n" + query);

                    myTable = new DataTable();

                    //Wenn Schema aus DB nicht eingehalten wird (z.B. UNIQUE Constrain in SELECT Abfragen); dann neue DataTable, alle Spalten <string>
                    using (var reader = command.ExecuteReader())
                    {
                        //if (reader.FieldCount == 0)
                        //    return myTable;

                        //zu Fuß einlesen
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            //Spalten einrichten
                            myTable.Columns.Add(reader.GetName(i), typeof(string));
                        }

                        while (reader.Read())
                        {
                            List<object> row = [];

                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                // string colType = myTable.Columns[i].DataType.Name;

                                if (reader.IsDBNull(i))
                                {
                                    row.Add(string.Empty);
                                }
                                else
                                {
                                    string r = reader.GetFieldValue<object>(i)?.ToString() ?? string.Empty;
                                    row.Add(r);
                                }
                            }

                            myTable.Rows.Add([.. row]);
                        }
                    }

                    return myTable;
                }
            }
            catch (Exception ex)
            {
                Program.Message(Message.MessageType.Alert, "SqlSelectDataTable(): " + query + "\r\n" + ex.GetType() + "\r\n" + ex.Message + "\r\n" + ex.StackTrace);
                //Worker.LogError("SqlSelectDataTable(): " + query + "\r\n" + ex.GetType() + "\r\n" + ex.Message + "\r\n" + ex.StackTrace);
            }

            return myTable;
        }

        /// <summary>
        /// Fragt einen Einzelwert mit einem SQL-Befehl gegen die Datenbank ab.
        /// </summary>
        /// <param name="query">SQL-Abfrage</param>
        /// <param name="args">Parameter für SQL-Abfrage</param>
        /// <returns>Ergebniswert der Abfrage.</returns>
        internal static object? SelectValue(string dbPath, string query, Dictionary<string, object>? args)
        {
            try
            {
                using var connection = new SQLiteConnection("Data Source=" + dbPath);
                connection.Open();

                var command = connection.CreateCommand();
                command.CommandText = query;

                if (args != null && args.Count > 0)
                {
                    foreach (string key in args.Keys)
                    {
                        command.Parameters.AddWithValue(key, args[key]);
                    }
                }

                Console.WriteLine(command.ToString());
                return command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                Program.Message(Message.MessageType.Alert, "SqlSelectValue(): " + query + "\r\n" + ex.GetType() + "\r\n" + ex.Message);                
            }

            return null;
        }



        #endregion
    }
}
