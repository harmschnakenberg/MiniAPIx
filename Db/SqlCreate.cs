using S7.Net;
using System.Data;

namespace MiniAPI.Db
{
    public partial class Sql
    {
        #region Datenbank-Pfade

        private static readonly string AppFolder = AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Pfad zur Stammdaten-Datenbank
        /// </summary>
        static string MasterDbPath { get; } = Path.Combine(AppFolder, "db", "Master.db");

        /// <summary>
        /// Pfad zur Tages-Datenbank von heute
        /// </summary>
        static string DailyDbPath { get; } = GetDailyDbPath(DateTime.UtcNow);

        /// <summary>
        /// Pfad zur Tages-Datenbank von date
        /// </summary>
        /// <param name="date">Datum des Tages der gesuchten Datenbank</param>
        /// <returns>Pfad zur Datenbank</returns>
        static string GetDailyDbPath(DateTime date)
        {
            return Path.Combine(AppFolder, "db", date.ToUniversalTime().ToString("yyyyMMdd") + ".db");
        }

        #endregion

        /// <summary>
        /// Prüfe, ob die Tages-Datenbank vorhanden ist und erstelle sie ggf.
        /// </summary>
        internal static void CeckDailyDatabase()
        {
            _ = CheckDbFileAsync(DailyDbPath, false);
        }

        internal static async Task<bool> CheckDbFileAsync(string dbPath, bool testOpen = true)
        {

#if DEBUG
            //File.Delete(dbPath); //TEST

            //Console.WriteLine(dbPath);
#endif
            #region Datenbank-Datei anlegen, falls nicht vorhanden

            if (!File.Exists(dbPath))
            {
                if (dbPath == MasterDbPath)
                    await CreateDataBaseMasterAsync();
                if (dbPath == GetDailyDbPath(DateTime.UtcNow))
                    CreateDataBaseDaily();
            }

            #endregion

            if (!testOpen)
                return true;

            #region Mehrfach versuchen die Datenbank zu öffnen, falls sie grade in Benutzung ist

            int numTries = 10;

            while (numTries > 0)
            {
                --numTries;

                try
                {
                    using FileStream stream = new FileInfo(MasterDbPath).Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    stream.Close();
                    break;
                }
                catch (IOException)
                {
                    //the file is unavailable because it is:
                    //still being written to
                    //or being processed by another thread
                    //or does not exist (has already been processed)
                    Thread.Sleep(200);
                }
            }

            if (numTries == 0)
            {
                string txt = $"Die Datenbankdatei >{MasterDbPath}< ist durch ein anderes Programm gesperrt.";
                Console.WriteLine(txt);
                //Sql.InsertLog(1, txt);
                //Worker.LogWarning(txt);
            }
            return numTries > 0;

            #endregion
        }


        /// <summary>
        /// Erzeugt eine Datenbank für Stammdaten
        /// </summary>
        private static async Task CreateDataBaseMasterAsync()
        {
           // Worker.LogInfo($"Erstelle eine neue Datenbank-Datei unter '{MasterDbPath}'");

            try
            {
                //Erstelle Datenbank-Datei und öffne einmal zum Testen
                string dir = Path.GetDirectoryName(MasterDbPath) ?? string.Empty;
                _ = Directory.CreateDirectory(dir);
                FileStream stream = File.Create(MasterDbPath);
                stream.Close();

                System.Threading.Thread.Sleep(500);

                #region Tabellen erstellen
                string query = @"CREATE TABLE IF NOT EXISTS Log ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, 
                          Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                          Category TEXT NOT NULL,
                          Content TEXT 
                          ); ";
                _ = NonQueryAsync(MasterDbPath, query, null);

                query = @"CREATE TABLE IF NOT EXISTS User ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,
                          IsAdmin INTEGER DEFAULT 0,
                          Password TEXT,
                          Identification TEXT
                          ); ";
                _ = NonQueryAsync(MasterDbPath, query, null);


                query = @"CREATE TABLE IF NOT EXISTS ConnectionType ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE                          
                          ); ";
                _ = NonQueryAsync(MasterDbPath, query, null);

                query = @"CREATE TABLE IF NOT EXISTS Source ( 
                          Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                          Name TEXT NOT NULL UNIQUE,
                          ConnectionType INTEGR NOT NULL DEFAULT 1,       
                          CpuType INTEGR NOT NULL DEFAULT 40,  
                          Ip TEXT, 
                          Port INTEGER DEFAULT 102,                           
                          Rack INTEGER DEFAULT 0,
                          Slot INTEGER DEFAULT 0,
                          Comment TEXT,

                          CONSTRAINT fk_ConnectionType FOREIGN KEY (ConnectionType) REFERENCES ConnectionType (Id) ON DELETE NO ACTION
                          ); "; //CONSTRAINT für CpuType?
                _ = NonQueryAsync(MasterDbPath, query, null);



                #endregion

                #region Tabellen füllen

                query = @"INSERT INTO Log (Category, Content) VALUES ('System', 'Datenbank neu erstellt.'); ";
                await NonQueryAsync(MasterDbPath, query, null);

                query = $"INSERT INTO User (Name, IsAdmin, Password) VALUES ('admin', 1, '{Encrypt("admin")}'); ";
                await NonQueryAsync(MasterDbPath, query, null);

                query = @"INSERT INTO ConnectionType (Name) VALUES ('S7'); ";
                await NonQueryAsync(MasterDbPath, query, null);

                query = @"INSERT INTO Source (Name, Ip) VALUES ('A01', '192.168.0.10'); ";
                await NonQueryAsync(MasterDbPath, query, null);

                #endregion

            }
            catch (Exception ex)
            {
                Program.Message(Message.MessageType.Alert, "CreateNewDataBase() " + ex.Message + ex.StackTrace);                
                throw;
            }

        }


        /// <summary>
        /// Erzeugt für jeden Tag eine Datenbank für die auzuzeichnenden Daten
        /// </summary>
        internal static void CreateDataBaseDaily()
        {
            //Worker.LogInfo($"Erstelle eine neue Datenbank-Datei unter '{DailyDbPath}'");

            try
            {
                //Erstelle Datenbank-Datei und öffne einmal zum Testen
                string dir = Path.GetDirectoryName(DailyDbPath) ?? string.Empty;
                _ = Directory.CreateDirectory(dir);
                FileStream stream = File.Create(DailyDbPath);
                stream.Close();

                System.Threading.Thread.Sleep(500);

                
                #region Tabellen erstellen
                string query = @"CREATE TABLE IF NOT EXISTS TagNames (                                                   
                          TagName TEXT NOT NULL PRIMARY KEY,
                          TagType TEXT,
                          TagComment TEXT
                          ); ";
                _ = NonQueryAsync(DailyDbPath, query, null);

                //TagName besser als TagId mit ForeignKey von TagNames.Id verwenden? Nachteil: mehr Joins nötig
                //FOREIGN KEY (TagId) REFERENCES TagNames(Id) 
                query = @"CREATE TABLE IF NOT EXISTS Data (                         
                          Time TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP, 
                          TagName TEXT NOT NULL,
                          TagValue NUMERIC
                          ); ";
                _ = NonQueryAsync(DailyDbPath, query, null);

                #endregion

                #region Tabelle TagNames mit TagNames aus der letzten Tabelle füllen

                DataTable dt = new();
                DateTime date = DateTime.UtcNow;
                int counter = 100; // limitieren, wie weit in die Vergangenheit geschaut werden soll

                //Wenn die Datenbank grade neu erstellt wurde ist sie leer.
                //Deshalb die letzte Tages-DB suchen und TagNames übernehmen
                while (dt.Rows.Count == 0 && --counter > 0)
                {
                    date = date.AddDays(-1);
                    string dbPath = Path.Combine(AppFolder, "db", date.ToString("yyyyMMdd") + ".db");

                    if (!File.Exists(dbPath))
                        continue;
#if DEBUG
                    Sql.LogDb($"[{counter}] Lese TagNames aus " + dbPath);                    
#endif                    
                    query = $"ATTACH DATABASE '{dbPath}' AS old_db; " +
                    $"INSERT INTO TagNames SELECT * FROM old_db.TagNames; " +
                    //Ergänze TagNames aus der alten DB, die unter Data vorhanden aber nicht in TagNames vorhanden sind
                    "INSERT INTO TagNames (TagName) " +
                    "SELECT DISTINCT TagName FROM old_db.Data " +
                    "WHERE TagName NOT IN (SELECT TagName FROM TagNames); " +
                    "DETACH DATABASE old_db; " +
                    //Lese alle TagNames aus der neuen DB zur Kontrolle
                    "SELECT TagName FROM TagNames; ";

                    dt = SelectDataTable(DailyDbPath, query, null);

                }

                #endregion
            }

            catch
            {
                throw new NotImplementedException();
            }
        }

        internal static Dictionary<string, Plc> GetCpuConfigs()
        {
            string query = @" 
                SELECT Name, CpuType, Ip, Rack, Slot FROM Source
                WHERE ConnectionType == 1
                ; ";

            DataTable dt = Sql.SelectDataTable(MasterDbPath, query, []);

            return dt.AsEnumerable()
                .ToDictionary<DataRow, string, Plc>(
                    row => row.Field<string>(0) ?? string.Empty,
                    row => new Plc(
                        (CpuType)row.Field<Int64>(1),
                        row.Field<string>(2) ?? string.Empty,
                        (short)row.Field<Int64>(3),
                        (short)row.Field<Int64>(4)
                        )
                    );
        }


    }
}
