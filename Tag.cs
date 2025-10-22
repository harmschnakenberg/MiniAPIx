using Microsoft.AspNetCore.Mvc.Infrastructure;
using S7.Net;
using S7.Net.Types;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MiniAPI
{
    public static class TagCollection
    {
        private static bool _IsReadingTags;


        #region SPS
        private static readonly Dictionary<string, Plc> Plcs = [];

        public static void AddCpu(string name, CpuType cpuType, string ip, short rack, short slot)
        {
            Plcs.Add(name, new Plc(cpuType, ip, rack, slot));
        }
        #endregion

        #region Tag-CRUD
        public static LinkedList<Tag> Tags { get; private set; } = new LinkedList<Tag>();

        public static void AddTag(string tagName)
        {
            if (Tags.Where(x => x.Name == tagName).FirstOrDefault() != null)
            {
                Tags.Where(x => x.Name == tagName).FirstOrDefault()?.RefreshExpiration();
                Console.WriteLine($"Tag bereits vorhanden, Ablaufzeit aktualisiert: {tagName}");
            }
            else
            {
                Tag tag = new(tagName);
                Tags.AddLast(tag);
            }


        }

        public static void AddTags(string[]? tagNames)
        {
            if (tagNames == null)
                return;

            foreach (var tagName in tagNames)
            {
                AddTag(tagName);
            }
        }

        private static void RemoveExpiredTags()
        {
            // Iterator initialisieren
            var knoten = Tags.First;

            // While-Schleife für die Iteration
            while (knoten != null)
            {
                // Nächsten Knoten speichern, bevor wir ihn möglicherweise ändern
                var nextNode = knoten.Next;

                // Aktionen auf dem aktuellen Knoten ausführen
                if (knoten.Value.IsExpired())
                {
                    Console.WriteLine("entferne " + knoten.Value.Name);
                    // Element entfernen
                    Tags.Remove(knoten);
                }

                // Zum nächsten Knoten wechseln
                knoten = nextNode;
            }
        }
        #endregion

        #region Tags lesen
        /// <summary>
        /// Startet Tag-Lesevorgang
        /// </summary>
        public static void StartReading()
        {
            if (!_IsReadingTags)
            {
                _IsReadingTags = true;
                ReadTagsAsync();
            }
        }

        /// <summary>
        /// Stoppt Tag-Lesevorgang
        /// </summary>
        public static void StopReading()
        {
            _IsReadingTags = false;
        }

        public static string PokeTagValues(string[] tagNames)
        {           
            var answer = new List<JsonTag>();

            foreach (var tag in Tags.Where(x => tagNames.Contains(x.Name)))
            {
                Console.WriteLine($"Tag {tag.Name}={tag.Value} wird gesendet.");
                answer.Add(new JsonTag(tag.Name, tag.Value));
            }

            return JsonSerializer.Serialize(answer);
        }

        /// <summary>
        /// Liest alle Tags in einer Schleife, entfernt anschließend abgelaufene Tags und startet erneut
        /// </summary>
        /// <returns></returns>
        private static async void ReadTagsAsync()
        {
            for (int i = 0; i < 90; i++)
            {
                Console.Write("; " + i);
                await ReadAllTagsOnceAsync();
                await Task.Delay(1000);
            }

            RemoveExpiredTags();
            Console.WriteLine($"Es werden {Tags.Count} Tags überwacht.");

            if (_IsReadingTags)
                ReadTagsAsync();
            else
                foreach (var plc in Plcs.Values)
                    plc.Close();
        }

        private static async Task ReadAllTagsOnceAsync()
        {
            try
            {
                foreach (var plcName in Plcs.Keys)
                {
                    if (!Plcs[plcName].IsConnected)
                    {
                        await Plcs[plcName].OpenAsync();
                    }

                    #region Prepare Tags for PLC
                    List<DataItem> tags = Tags
                                            .Where(x => x.PlcName == plcName)
                                            .Select(y => y.Item)
                                            .ToList();
                    #endregion
                    #region Read batches of 20
                    int index = 0;
                    int end = tags.Count;

                    if (end < 1)
                        continue;

                    while (index < end)
                    {
                        var batch = tags.Skip(index).Take(20).ToList();
                        await Plcs[plcName].ReadMultipleVarsAsync(batch);

                        foreach (var dataItem in batch)
                        {
                            foreach (var tag in Tags)
                            {
                                if (tag.PlcName == plcName
                                    && tag.Item.StartByteAdr == dataItem.StartByteAdr
                                    && tag.Item.DataType == dataItem.DataType
                                    && tag.Item.VarType == dataItem.VarType
                                    )
                                {
                                    var diff = Convert.ToDouble(tag.Value) - Convert.ToDouble(dataItem.Value);
                                    //Console.WriteLine($"Tag {tag.Name} gelesen: {dataItem.Value} (alt: {tag.Value}, diff: {diff})");
                                    if (Math.Abs(diff)>0.09)
                                        tag.Value = dataItem.Value;
                                }
                            }
                        }

                        index += 20;
                    }

                    #endregion
                }
            }
            catch (PlcException plcEx)
            {
                Console.WriteLine("SPS-Fehler beim Lesen der Tags: " + plcEx.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fehler beim Lesen der Tags: " + ex.Message);
            }
            #endregion

        }

    }

    public class Tag
    {

        public Tag(string name)
        {
            Name = name;
            PlcName = name.Substring(0, 3);
            string address = name.Substring(4).Replace('_', '.');
            Console.WriteLine($"Tag zu {PlcName} hinzugefügt: {address}");
            Item = DataItem.FromAddress(address);
            RefreshExpiration();
        }

        public string Name { get; set; }
        public string? Comment { get; set; }
        internal string PlcName { get; private set; } = "A00";        
        public DataItem Item { get; set; }      
        public object? Value { get; set; }

        private System.DateTime Expiration;

        public void RefreshExpiration()
        {
            Expiration = System.DateTime.Now.AddSeconds(90);
        }

        public bool IsExpired()
        {
            return System.DateTime.Now > Expiration;
        }
    }

    public record JsonTag(string N, object? V);

    [JsonSerializable(typeof(JsonTag))]
    internal partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}
