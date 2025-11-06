using S7.Net;
using System.Data;
using System.Net;

namespace MiniAPI.Db
{
    public partial class Sql
    {
        #region Logging

        internal static async void LogDb(string message)
        {
            string query = @" 
                INSERT INTO Log ( Message) VALUES (
                  @Message)
                ; ";
            Dictionary<string, object> args = new()
            {
                
               
                { "@Message", message }
            };
            _ = await NonQueryAsync(MasterDbPath, query, args);
        }

        #endregion



        #region Benutzerverwaltung

        /// <summary>
        /// Verschlüsselt ein Passwort mit SHA256
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        private static string Encrypt(string password)
        {
            if (password == null) return string.Empty;

            byte[] data = System.Text.Encoding.UTF8.GetBytes(password);

            ///TODO: Salting oder andere Sicherheitsverbesserungen nachpflegen

            data = System.Security.Cryptography.SHA256.HashData(data);
#if DEBUG
            Console.WriteLine($"Passwort '{password}' -> '{System.Text.Encoding.UTF8.GetString(data)}'");
#endif
            return System.Text.Encoding.UTF8.GetString(data);
        }

        #endregion


        #region Datenquellenverwaltung

        /// <summary>
        /// Liest die Verbindungsdaten für alle in der Datenbank hinterlegten SPSen aus.    
        /// </summary>
        /// <returns>SPSName, Plc-Objekt</returns>
        internal static Dictionary<string, Plc> GetCpuConfig()
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


//        internal static bool CreateOrUpdatePlc(Dictionary<string, string> form, User admin)
//        {

//#if DEBUG
//            Worker.LogWarning(string.Join(' ', form));
//#endif

//            if (admin.Name.Length < 2)
//                return false;

//            #region Formular auslesen
//            string cpuid = WebUtility.UrlDecode(form["cpuid"]) ?? string.Empty;
//            string cpuname = WebUtility.UrlDecode(form["cpuname"]) ?? string.Empty;
//            int connectiontype = Convert.ToInt32(WebUtility.UrlDecode(form["connectiontype"]));
//            S7.Net.CpuType cputype = (S7.Net.CpuType)Convert.ToInt32(WebUtility.UrlDecode(form["cputype"]));
//            string cpuip = WebUtility.UrlDecode(form["ip"]) ?? string.Empty;
//            int cpuport = Convert.ToInt32(WebUtility.UrlDecode(form["port"]));
//            short cpurack = Convert.ToInt16(WebUtility.UrlDecode(form["rack"]));
//            short cpuslot = Convert.ToInt16(WebUtility.UrlDecode(form["slot"]));
//            string cpucomment = WebUtility.UrlDecode(form["comment"]);

//            bool success = false;
//            #endregion

//            Plc plc = new(cputype, cpuip, cpurack, cpuslot);

//            #region Cpu erstellen oder ändern

//            //TODO: Cpu name nicht änderbar machen, wenn die CPU irgendwo verwendte wird!

//            if (!string.IsNullOrWhiteSpace(cpuname) && plc is not null)
//            {
//                Dictionary<string, object> args = new()
//                {
//                    { "@Name", cpuname },
//                    { "@ConnectionType", connectiontype },
//                    { "@CpuType", cputype },
//                    { "@Ip", cpuip },
//                    { "@Port", cpuport },
//                    { "@Rack", cpurack },
//                    { "@Slot", cpuslot },
//                    { "@Comment", cpucomment },
//                    { "@AdminId", admin.Id }
//                };

//                string query = @" 
//                INSERT INTO Source (Name, ConnectionType, CpuType, Ip, Port, Rack, Slot, Comment) VALUES (
//                @Name, @ConnectionType, @CpuType, @Ip, @Port, @Rack, @Slot, @Comment)
//                ON CONFLICT(Name) DO UPDATE SET
//                    Name = @Name,
//                    ConnectionType = @ConnectionType,
//                    CpuType = @CpuType,
//                    Ip = @Ip,
//                    Port = @Port,
//                    Rack = @Rack,
//                    Slot = @Slot,
//                    Comment = @Comment
//                    WHERE (SELECT IsAdmin FROM User WHERE Id = @AdminId) == 1
//                ; ";

//                success = Sql.MasterNonQueryAsync(query, args);
//            }

//            #endregion

//            #region Änderungen live schalten
//            if (success && MyS7.Sps.ContainsKey(cpuname) && plc is not null)
//            {
//                MyS7.Sps[cpuname] = plc;
//            }
//            #endregion 

//            return success;
//        }


        #endregion

    }
}
