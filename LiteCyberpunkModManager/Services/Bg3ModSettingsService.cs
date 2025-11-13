using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LiteCyberpunkModManager.Helpers;
using LiteCyberpunkModManager.Models;

namespace LiteCyberpunkModManager.Services
{
    public static class Bg3ModSettingsService
    {
        public static string GetModSettingsPath() => GameHelper.GetBg3ModSettingsPath();

        public static List<Bg3ModuleEntry> LoadModules()
        {
            var path = GetModSettingsPath();
            var result = new List<Bg3ModuleEntry>();

            try
            {
                if (!File.Exists(path)) return result;

                var doc = XDocument.Load(path);
                var modsNode = doc
                    .Descendants("node")
                    .FirstOrDefault(n => (string?)n.Attribute("id") == "Mods");
                var children = modsNode?.Element("children");
                if (children == null) return result;

                foreach (var moduleNode in children.Elements("node").Where(n => (string?)n.Attribute("id") == "ModuleShortDesc"))
                {
                    var attrs = moduleNode.Elements("attribute").ToList();
                    string GetAttr(string id) => attrs.FirstOrDefault(a => (string?)a.Attribute("id") == id)?.Attribute("value")?.Value ?? string.Empty;

                    long.TryParse(GetAttr("Version64"), out var version);

                    result.Add(new Bg3ModuleEntry
                    {
                        Folder = GetAttr("Folder"),
                        Name = GetAttr("Name"),
                        UUID = GetAttr("UUID"),
                        Version64 = version
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BG3] Failed to load modsettings.lsx: {ex.Message}");
            }

            return result;
        }

        public static void SaveOrder(List<Bg3ModuleEntry> ordered)
        {
            var path = GetModSettingsPath();
            try
            {
                if (!File.Exists(path)) return;

                var doc = XDocument.Load(path);
                var modsNode = doc
                    .Descendants("node")
                    .FirstOrDefault(n => (string?)n.Attribute("id") == "Mods");
                var children = modsNode?.Element("children");
                if (children == null) return;

                var moduleNodes = children.Elements("node").Where(n => (string?)n.Attribute("id") == "ModuleShortDesc").ToList();
                var byUuid = moduleNodes
                    .Select(n => new
                    {
                        Node = n,
                        Uuid = n.Elements("attribute")
                            .FirstOrDefault(a => (string?)a.Attribute("id") == "UUID")?
                            .Attribute("value")?.Value ?? string.Empty
                    })
                    .ToDictionary(x => x.Uuid, x => x.Node, StringComparer.OrdinalIgnoreCase);

                var newOrder = new List<XElement>();

                foreach (var module in ordered)
                {
                    if (byUuid.TryGetValue(module.UUID, out var node))
                    {
                        newOrder.Add(node);
                        byUuid.Remove(module.UUID);
                    }
                }

                // Preserve any modules we didn't know about at the end, in original order.
                foreach (var remaining in moduleNodes.Where(n => byUuid.ContainsKey(
                    n.Elements("attribute")
                     .FirstOrDefault(a => (string?)a.Attribute("id") == "UUID")?
                     .Attribute("value")?.Value ?? string.Empty)))
                {
                    newOrder.Add(remaining);
                }

                children.ReplaceNodes(newOrder);

                // Backup then save
                var backup = path + ".bak";
                try
                {
                    File.Copy(path, backup, overwrite: true);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BG3] Failed to backup modsettings.lsx: {ex.Message}");
                }

                doc.Save(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BG3] Failed to save modsettings.lsx: {ex.Message}");
            }
        }
    }
}

