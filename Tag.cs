using Microsoft.AspNetCore.Mvc.Infrastructure;
using S7.Net;
using S7.Net.Types;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace MiniAPI
{
    public static class TagCollection
    {
        private static bool _IsReadingTags;
       
//#if DEBUG
//        public const int TagExpirationSeconds = 30;
//#else
        public const int TagExpirationSeconds = 90;
//#endif

        #region SPS
        private static readonly Dictionary<string, Plc> Plcs = [];


        public static void AddCpu(string name, CpuType cpuType, string ip, short rack, short slot)
        {
            Plcs[name] = new Plc(cpuType, ip, rack, slot);
        }
        #endregion

        #region Tag-CRUD
        // Threadsichere, schnelle Lookups nach Name
        public static ConcurrentDictionary<string, Tag> Tags { get; } = new();

        private static readonly Lock _startStopLock = new();
        private static CancellationTokenSource? _readCts;
        private static Task? _readingTask;

        public static void AddTag(string tagName)
        {
            var tag = Tags.GetOrAdd(tagName, n => new Tag(n));
            tag.RefreshExpiration();
#if DEBUG
            Console.WriteLine($"Tag hinzugefügt/aktualisiert: {tagName}");
#endif
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
            var now = System.DateTime.Now;
            var expired = Tags.Where(kv => kv.Value.IsExpired(now)).Select(kv => kv.Key).ToList();
            foreach (var key in expired)
            {
                if (Tags.TryRemove(key, out _))
                    Console.WriteLine("entferne " + key);
            }
        }
        #endregion

        #region Tags lesen
        /// <summary>
        /// Startet Tag-Lesevorgang
        /// </summary>
        public static void StartReading()
        {
            lock (_startStopLock)
            {                
                if (_IsReadingTags) return;
                _IsReadingTags = true;
                _readCts = new CancellationTokenSource();
                _readingTask = Task.Run(() => ReadLoopAsync(_readCts.Token));
            }
        }

        /// <summary>
        /// Stoppt Tag-Lesevorgang
        /// </summary>
        public static void StopReading()
        {
            lock (_startStopLock)
            {
                if (!_IsReadingTags) return;
                _IsReadingTags = false;
                _readCts?.Cancel();
            }
        }

        public static List<JsonTag> PokeTagValues(ref Dictionary<string, object?> request)
        {
            var jsonTags = new List<JsonTag>();
            var keys = request.Keys.ToList();

            foreach (var name in keys)
            {
                if (!Tags.TryGetValue(name, out var tag))
                    continue;

                if (!TagCollectionHelpers.TryConvertToDouble(tag.Value, out var newVal))
                    continue;

                if (!TagCollectionHelpers.TryConvertToDouble(request[name], out var oldVal))
                {
                    jsonTags.Add(new JsonTag(name, newVal));
                    request[name] = newVal;
                    continue;
                }

                var diff = Math.Abs(newVal - oldVal);
                var newJsonItem = new JsonTag(name, newVal);

                if (diff > 0.09 && !jsonTags.Contains(newJsonItem)) 
                {
                    jsonTags.Add(newJsonItem);
                }
                request[name] = newVal;
            }
#if DEBUG
            if(jsonTags.Count > 0)
                Console.WriteLine(string.Join(' ', jsonTags.Select(j => $"{j.N}:{j.V}")));
#endif
            return jsonTags;
        }

        /// <summary>
        /// Hintergrundloop: regelmäßiges Lesen und periodisches Entfernen abgelaufener Tags
        /// </summary>
        private static async Task ReadLoopAsync(CancellationToken ct)
        {
            try
            {
                Console.WriteLine($"Tag-Lifetime: {TagExpirationSeconds} sec");
                int seconds = 0;
                // Verwende PeriodicTimer für stabilere Ticks und geringere Overhead
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
                while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
                {
                    await ReadAllTagsOnceAsync(ct);

                    seconds++;
                    if (seconds >= TagExpirationSeconds)
                    {
                        RemoveExpiredTags();
                        seconds = 0;
                        Console.WriteLine($"Es werden {Tags.Count} Tags überwacht.");
                    }
                }
            }
            catch (OperationCanceledException) {
                /* erwartet beim Stop */
                Console.WriteLine($"Lesen aus SPS beenedet.");
            }
            finally
            {
                foreach (var plc in Plcs.Values)
                {
                    try { plc.Close(); } catch { }
                }
                _IsReadingTags = false;
            }
        }

        private static async Task ReadAllTagsOnceAsync(CancellationToken ct = default)
        {
            try
            {
                // Kopiere die PLC-Namen um Änderungen an Plcs während der Iteration zu vermeiden
                var plcNames = Plcs.Keys.ToList();

                // Paralleles Lesen pro PLC (je PLC sequentiell in Batches)
                await Parallel.ForEachAsync(plcNames, ct, async (plcName, token) =>
                {
                    token.ThrowIfCancellationRequested();

                    if (!Plcs.TryGetValue(plcName, out var plc))
                        return;

                    try
                    {
                        if (!plc.IsConnected)
                        {
                            await plc.OpenAsync(token);
                        }

                        // Nur Tags für diese PLC ermitteln
                        var plcTags = new List<Tag>();
                        foreach (var kv in Tags)
                        {
                            if (kv.Value.PlcName == plcName)
                                plcTags.Add(kv.Value);
                        }

                        if (plcTags.Count == 0) return;

                        // Mapping für schnelle Zuordnung
                        var itemToTag = new Dictionary<string, Tag>(plcTags.Count);
                        var items = new List<DataItem>(plcTags.Count);
                        foreach (var t in plcTags)
                        {
                            var key = TagCollectionHelpers.DataItemKey(t.Item);
                            itemToTag[key] = t;
                            items.Add(t.Item);
                        }

                        int index = 0;
                        int end = items.Count;
                        const int batchSize = 20;

                        while (index < end)
                        {
                            token.ThrowIfCancellationRequested();
                            var take = Math.Min(batchSize, end - index);
                            // slice ohne LINQ-Allokationen
                            var batch = new List<DataItem>(take);
                            for (int i = 0; i < take; i++)
                                batch.Add(items[index + i]);

                            await plc.ReadMultipleVarsAsync(batch, token);

                            foreach (var dataItem in batch)
                            {
                                var key = TagCollectionHelpers.DataItemKey(dataItem);
                                if (!itemToTag.TryGetValue(key, out var tag)) continue;

                                if (!TagCollectionHelpers.TryConvertToDouble(tag.Value, out var oldVal))
                                    oldVal = double.NaN;

                                if (!TagCollectionHelpers.TryConvertToDouble(dataItem.Value, out var newVal))
                                    continue;

                                var diff = Math.Abs(oldVal - newVal);
                                if (double.IsNaN(oldVal) || diff > 0.09)
                                {
                                    tag.Value = dataItem.Value;
                                }
                            }

                            index += take;
                        }
                    }
                    catch (PlcException plcEx)
                    {
                        Console.WriteLine("SPS-Fehler beim Lesen der Tags (PLC: " + plcName + "): " + plcEx.Message);
                    }
                    catch (OperationCanceledException)
                    {
                        // Durch Cancellation token erwartet
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Fehler beim Lesen der Tags (PLC: " + plcName + "): " + ex.Message);
                    }
                });
            }
            catch (PlcException plcEx)
            {
                Console.WriteLine("SPS-Fehler beim Lesen der Tags: " + plcEx.Message);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Lesen aus SPS abgebrochen.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Fehler beim Lesen der Tags: " + ex.Message);
            }
        }

        #endregion

    }

    /// <summary>
    /// Datenpunkt
    /// </summary>
    public class Tag
    {
        public Tag(string name)
        {
            Name = name;
            PlcName = name.Length >= 3 ? name[..3] : "A00";
            string address = name.Length > 4 ? name[4..].Replace('_', '.') : string.Empty;
            try
            {
                Item = DataItem.FromAddress(address);
            }
            catch
            {
                Item = DataItem.FromAddress(string.Empty);
            }
            RefreshExpiration();
        }

        public string Name { get; set; }
        public string? Comment { get; set; }
        internal string PlcName { get; private set; } = "A00";
        internal DataItem Item { get; set; }
        public object? Value { get; set; }

        private System.DateTime Expiration; //MHD

        public void RefreshExpiration()
        {
            Expiration = System.DateTime.Now.AddSeconds(TagCollection.TagExpirationSeconds);
        }

        public bool IsExpired(System.DateTime? now = null)
        {
            var check = now ?? System.DateTime.Now;
            return check > Expiration;
        }
    }

    public record JsonTag(string N, object? V);

    // Hilfsfunktionen für TagCollection
    internal static partial class TagCollectionHelpers
    {
        internal static string DataItemKey(DataItem item) =>
            $"{item.StartByteAdr}|{item.DataType}|{item.VarType}";

        internal static bool TryConvertToDouble(object? value, out double result)
        {
            result = double.NaN;
            if (value == null) return false;
            if (value is double d) { result = d; return true; }
            if (value is float f) { result = f; return true; }
            if (value is int i) { result = i; return true; }
            if (value is long l) { result = l; return true; }
            if (value is decimal dec) { result = (double)dec; return true; }
            if (double.TryParse(value.ToString(), out d)) { result = d; return true; }
            return false;
        }
    }

}