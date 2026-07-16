using D_OS_Save_Editor.Properties;
using D_OS_Save_Editor.savegame;
using LSLib.Granny;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;

namespace D_OS_Save_Editor
{

    [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
    [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
    public class LsxParser
    {
        #region read file
        public static List<string> GenerationBoostCollector;
        public static List<string> StatsBoostsCollector;

        public static Player[] ParsePlayer(XmlDocument doc)
        {
            GenerationBoostCollector = new List<string>();
            StatsBoostsCollector = new List<string>();
            
            // find player data
            var playerData = doc.DocumentElement.SelectNodes("//*[@id='PlayerData']");

            if (playerData == null)
                throw new XmlException("Unable to find any player data in the savegame.");

            var players = new Player[playerData.Count];
            Parallel.For(0, playerData.Count, i =>
            {
                try
                {
                    players[i] = ParsePlayer(playerData[i]);
                }
                catch (Exception e)
                {
                    throw new PlayerParserException(e, playerData[i]);
                }

                // now we have the inventory id, we can get items
                var inventoryData =
                    doc.DocumentElement.SelectNodes($"//attribute [@id='Parent'] [@value='{players[i].InventoryId}']");
                if (inventoryData == null)
                    return;

                players[i].Items = new Item[inventoryData.Count];
                //var notAnItemIdx = new List<int>();
                var notAnItemIdx = new ConcurrentQueue<int>();
                Parallel.For(0, inventoryData.Count, j =>
                {
                    Item item;
                    try
                    {
                        item = ParseItem(inventoryData[j].ParentNode);
                        item.ItemXmlNodeIdx = j;
                    }
                    catch (ObjectNullException)
                    {
                        notAnItemIdx.Enqueue(j);
                        return;
                    }
                    catch (Exception e)
                    {
                        throw new ItemParserException(e, inventoryData[j].ParentNode);
                    }

                    players[i].Items[j] = item;
                    players[i].SlotsOccupation[int.Parse(item.Slot)] = true;
                });

                if (notAnItemIdx.Count == 0) return;

                // remove not an item entry
                var items = new List<Item>(players[i].Items);
                var list = notAnItemIdx.ToList();
                list.Sort((a, b) => b.CompareTo(a));
                foreach (var idx in list)
                    items.RemoveAt(idx);
                players[i].Items = items.ToArray();
            });
            return players;
        }

        public static Player ParsePlayer(XmlNode node)
        {
            var player = new Player
            {
                MaxVitalityPatchCheck = node.ParentNode.ParentNode
                        .SelectSingleNode("attribute [@id='MaxVitalityPatchCheck']")?.Attributes[1].Value,
                Vitality = node.ParentNode.ParentNode
                        .SelectSingleNode("attribute [@id='Vitality']")?.Attributes[1].Value,
                InventoryId = node.ParentNode.ParentNode.SelectSingleNode("attribute [@id='Inventory']")
                        ?.Attributes[1].Value,

                Experience = node.ParentNode
                        .SelectSingleNode("node//attribute [@id='Experience']")?.Attributes[1].Value,
                Reputation = node.ParentNode
                        .SelectSingleNode("node//attribute [@id='Reputation']")?.Attributes[1].Value,

                AttributePoints =
                        node.SelectSingleNode("children//attribute [@id='AttributePoints']")
                            ?.Attributes[1].Value,
                AbilityPoints =
                        node.SelectSingleNode("children//attribute [@id='AbilityPoints']")
                            ?.Attributes[1].Value,
                TalentPoints = node.SelectSingleNode("children//attribute [@id='TalentPoints']")
                        ?.Attributes[1].Value,

                Name = node.SelectSingleNode("children//attribute [@id='Name']")?.Attributes[1].Value,
                Icon = node.SelectSingleNode("children//attribute [@id='Icon']")?.Attributes[1].Value,
                ClassType = node.SelectSingleNode("children//attribute [@id='ClassType']")?.Attributes[1]
                        .Value
            };

            var nodes = node.ParentNode.SelectNodes("node//node [@id='Skills']");
            for (var j = 0; j < nodes?.Count; j++)
            {
                player.Skills.Add(nodes[j].FirstChild.Attributes[1].Value,
                    nodes[j].ChildNodes[1].Attributes[1].Value == "True");
            }

            nodes = node.SelectNodes("children//node [@id='Attributes']");
            for (var j = 0; j < nodes?.Count; j++)
            {
                player.Attributes.Add(j, int.Parse(nodes[j].FirstChild.Attributes[1].Value));
            }

            nodes = node.SelectNodes("children//node [@id='Abilities']");
            for (var j = 0; j < nodes?.Count; j++)
            {
                player.Abilities.Add(j, int.Parse(nodes[j].FirstChild.Attributes[1].Value));
            }

            nodes = node.SelectNodes("children//node [@id='Talents']");
            for (var j = 0; j < nodes?.Count; j++)
            {
                player.Talents.Add(uint.Parse(nodes[j].FirstChild.Attributes[1].Value));
            }

            nodes = node.SelectNodes("children//node [@id='Traits']");
            for (var j = 0; j < nodes?.Count; j++)
            {
                player.Traits.Add(j, int.Parse(nodes[j].FirstChild.Attributes[1].Value));
            }

            if (player.Name == "")
                player.Name = "Henchman";

            return player;
        }

        public static Item ParseItem(XmlNode node)
        {
            var item = new Item();
#if DEBUG
                item.Xml = XmlUtilities.BeautifyXml(node);
#endif
            try
            {

                item.Flags = node.SelectSingleNode("attribute [@id='Flags']").Attributes[1].Value;
                item.IsKey = node.SelectSingleNode("attribute [@id='IsKey']").Attributes[1].Value;
                item.StatsName = node.SelectSingleNode("attribute [@id='Stats']").Attributes[1].Value;
                item.Parent = node.SelectSingleNode("attribute [@id='Parent']").Attributes[1].Value;
                item.Slot = node.SelectSingleNode("attribute [@id='Slot']").Attributes[1].Value;
                item.Amount = node.SelectSingleNode("attribute [@id='Amount']").Attributes[1].Value;
                item.IsGenerated = node.SelectSingleNode("attribute [@id='IsGenerated']").Attributes[1].Value;
                item.LockLevel = node.SelectSingleNode("attribute [@id='LockLevel']").Attributes[1].Value;
                item.Vitality = node.SelectSingleNode("attribute [@id='Vitality']").Attributes[1].Value;
                item.ItemType = node.SelectSingleNode("attribute [@id='ItemType']").Attributes[1].Value;
                item.MaxVitalityPatchCheck = node.SelectSingleNode("attribute [@id='MaxVitalityPatchCheck']").Attributes[1].Value;
                item.MaxDurabilityPatchCheck = node.SelectSingleNode("attribute [@id='MaxDurabilityPatchCheck']")?.Attributes[1].Value;
            }
            catch (NullReferenceException e)
            {
                throw new ObjectNullException(e, "One or more item nodes are not found.");
            }

            // sort item
            if (item.IsKey == "True")
                item.ItemSort = ItemSortType.Key;
                item.ItemSort = DataTable.GetItemSort(item.StatsName);

            // check if has generation
            var genNode = node.SelectSingleNode("children/node [@id='Generation']");
            if (genNode != null)
            {
                item.Generation = new Item.GenerationNode
                {
                    Base = genNode.SelectSingleNode("attribute [@id='Base']").Attributes[1].Value,
                    //ItemType = genNode.SelectSingleNode("attribute [@id='ItemType']").Attributes[1].Value,
                    //Level = genNode.SelectSingleNode("attribute [@id='Level']").Attributes[1].Value,
                    Random = genNode.SelectSingleNode("attribute [@id='Random']").Attributes[1].Value
                };
                var genBoostNodes = genNode.SelectNodes("children//attribute [@id='Object']");
                item.Generation.Boosts = new List<string>();
                foreach (XmlNode n in genBoostNodes)
                {
                    item.Generation.Boosts.Add(n.Attributes[1].Value);
                    if (!GenerationBoostCollector.Contains(n.Attributes[1].Value))
                        GenerationBoostCollector.Add(n.Attributes[1].Value);
                }
            }

            // check if has stats
            var statsNode = node.SelectSingleNode("children/node [@id='Stats']");
            if (statsNode == null)
                return item;

            item.Stats = new Item.StatsNode
            {
                Durability = statsNode.SelectSingleNode("attribute [@id='Durability']").Attributes[1].Value,
                DurabilityCounter =
                    statsNode.SelectSingleNode("attribute [@id='DurabilityCounter']").Attributes[1].Value,
                RepairDurabilityPenalty = statsNode.SelectSingleNode("attribute [@id='RepairDurabilityPenalty']")
                    .Attributes[1].Value,
                Level = statsNode.SelectSingleNode("attribute [@id='Level']").Attributes[1].Value,
                Charges = statsNode.SelectSingleNode("attribute [@id='Charges']").Attributes[1].Value
            };
            var statsBoostNodes = statsNode.SelectNodes("children/node [@id='PermanentBoost']/attribute");
            item.Stats.PermanentBoost = new Dictionary<string, string>();
            foreach (XmlNode n in statsBoostNodes)
            {
                item.Stats.PermanentBoost.Add(n.Attributes[0].Value, n.Attributes[1].Value);
                if (!StatsBoostsCollector.Contains($"{n.Attributes[0].Value}\t{n.Attributes[1].Value}"))
                    StatsBoostsCollector.Add($"{n.Attributes[0].Value}\t{n.Attributes[1].Value}");
            }

            return item;
        }

        public static Meta ParseMeta(XmlDocument doc)
        {
            var metaData = doc.DocumentElement?.SelectSingleNode("./region [@id='MetaData']/node [@id='MetaData']/children/node [@id='MetaData']");
            var saveTimeNode = metaData.SelectSingleNode("children/node [@id='SaveTime']");

            if (metaData == null)
            {
                throw new XmlException("Unable to find MeteData in meta savegame.");
            }

            var meta = new Meta
            {
                Level = metaData.SelectSingleNode("attribute [@id='Level']")?.Attributes[1].Value,
                Seed = metaData.SelectSingleNode("attribute [@id='Seed']")?.Attributes[1].Value,
                Difficulty = Int16.Parse(metaData.SelectSingleNode("attribute [@id='Difficulty']")?.Attributes[1].Value),
                SavegameType = Int16.Parse(metaData.SelectSingleNode("attribute [@id='SaveGameType']")?.Attributes[1].Value),
                Year = saveTimeNode.SelectSingleNode("attribute [@id='Year']")?.Attributes[1].Value,
                Month = saveTimeNode.SelectSingleNode("attribute [@id='Month']")?.Attributes[1].Value,
                Day = saveTimeNode.SelectSingleNode("attribute [@id='Day']")?.Attributes[1].Value,
                Hours = saveTimeNode.SelectSingleNode("attribute [@id='Hours']")?.Attributes[1].Value,
                Minutes = saveTimeNode.SelectSingleNode("attribute [@id='Minutes']")?.Attributes[1].Value,
                Seconds = saveTimeNode.SelectSingleNode("attribute [@id='Seconds']")?.Attributes[1].Value,
                Milliseconds = saveTimeNode.SelectSingleNode("attribute [@id='Milliseconds']")?.Attributes[1].Value
            };

            //Debug.WriteLine("TimeStamp: " + metaData.SelectSingleNode("attribute [@id='TimeStamp']")?.Attributes[1].Value);
            //Debug.WriteLine("Size: " + metaData.SelectSingleNode("attribute [@id='Size']")?.Attributes[1].Value);
            
            var gameVersionChildrenNodes = metaData.SelectNodes("children/node [@id='GameVersions']/children/node [@id='GameVersion']");
            foreach (XmlNode gameVersionNode in gameVersionChildrenNodes)
            {
                meta.GameVersions.Add(gameVersionNode.SelectSingleNode("attribute")?.Attributes[1].Value);
            }

            var modsChildrenNodes = metaData.SelectNodes("children/node [@id='ModuleSettings']/children/node [@id='Mods']/children/node [@id='ModuleShortDesc']");
            foreach (XmlNode moduleModNode in modsChildrenNodes)
            {
                meta.ModNames.Add(moduleModNode.SelectSingleNode("attribute [@id='Name']")?.Attributes[1].Value);
            }

            return meta;
        }
        #endregion

        #region write file
        public static XmlDocument WritePlayer(XmlDocument doc, Player[] players)
        {
            // find player data
            var playerData = doc.DocumentElement.SelectNodes("//*[@id='PlayerData']");

            if (playerData == null)
                throw new XmlException("Unable to find any player data in the savegame.");

            for (int i = 0; i < players.Length; i++)
            //Parallel.For(0, playerData.Count, parallelOptions, i =>
            {
                playerData[i].ParentNode.ParentNode.SelectSingleNode("attribute [@id='MaxVitalityPatchCheck']")
                    .Attributes[1].Value = players[i].MaxVitalityPatchCheck;
                playerData[i].ParentNode.ParentNode.SelectSingleNode("attribute [@id='Vitality']").Attributes[1].Value =
                    players[i].Vitality;
                playerData[i].ParentNode.ParentNode.SelectSingleNode("attribute [@id='Inventory']").Attributes[1].Value
                    = players[i].InventoryId;
                playerData[i].ParentNode.SelectSingleNode("node//attribute [@id='Experience']").Attributes[1].Value =
                    players[i].Experience;
                playerData[i].ParentNode.SelectSingleNode("node//attribute [@id='Reputation']").Attributes[1].Value =
                    players[i].Reputation;

                playerData[i].SelectSingleNode("children//attribute [@id='AttributePoints']").Attributes[1].Value =
                    players[i].AttributePoints;
                playerData[i].SelectSingleNode("children//attribute [@id='AbilityPoints']").Attributes[1].Value =
                    players[i].AbilityPoints;
                playerData[i].SelectSingleNode("children//attribute [@id='TalentPoints']").Attributes[1].Value =
                    players[i].TalentPoints;


                //var nodes = playerData[i].ParentNode.SelectNodes("node//node [@id='Skills']");
                //for (var j = 0; j < nodes?.Count; j++)
                //{
                //    Players[i].Skills.Add(nodes[j].FirstChild.Attributes[1].Value, skills[j].ChildNodes[1].Attributes[1].Value == "True");
                //}

                var nodes = playerData[i].SelectNodes("children//node [@id='Attributes']");
                for (var j = 0; j < nodes?.Count; j++)
                {
                    nodes[j].FirstChild.Attributes[1].Value = players[i].Attributes[j].ToString();
                }

                nodes = playerData[i].SelectNodes("children//node [@id='Abilities']");
                for (var j = 0; j < nodes?.Count; j++)
                {
                    nodes[j].FirstChild.Attributes[1].Value = players[i].Abilities[j].ToString();
                }

                nodes = playerData[i].SelectNodes("children//node [@id='Talents']");
                for (var j = 0; j < nodes?.Count; j++)
                {
                    nodes[j].FirstChild.Attributes[1].Value = players[i].Talents[j].ToString();
                }

                nodes = playerData[i].SelectNodes("children//node [@id='Traits']");
                for (var j = 0; j < nodes?.Count; j++)
                {
                    nodes[j].FirstChild.Attributes[1].Value = players[i].Traits[j].ToString();
                }

                // write item changes
                doc = WriteItemChanges(doc, players[i]);
                //});
            }
            return doc;
        }


        public static XmlDocument WriteItemChanges(XmlDocument doc, Player player)
        {
            // get all items belong to this player
            // find item data
            var inventoryData = doc.DocumentElement.SelectNodes($"//attribute [@id='Parent'] [@value='{player.InventoryId}']");
            
            foreach (var ic in player.ItemChanges)
            {
                try
                {
                    
                    if (ic.Value.ChangeType == ChangeType.Delete)
                    {

                    }
                    else if (ic.Value.ChangeType == ChangeType.Modify)
                    {
                        var itemNode = inventoryData[ic.Value.Item.ItemXmlNodeIdx].ParentNode;

                        //Console.WriteLine(itemNode.OuterXml);

                        var allowedChanges = ic.Value.Item.GetAllowedChangeType();
                        if (allowedChanges.Contains(nameof(ic.Value.Item.Amount)))
                            itemNode.SelectSingleNode("attribute [@id='Amount']").Attributes[1].Value =
                                ic.Value.Item.Amount;

                        if (allowedChanges.Contains(nameof(ic.Value.Item.LockLevel)))
                            itemNode.SelectSingleNode("attribute [@id='LockLevel']").Attributes[1].Value =
                                ic.Value.Item.LockLevel;

                        if (allowedChanges.Contains(nameof(ic.Value.Item.Vitality)))
                        {
                            itemNode.SelectSingleNode("attribute [@id='Vitality']").Attributes[1].Value =
                                ic.Value.Item.Vitality;
                            itemNode.SelectSingleNode("attribute [@id='MaxVitalityPatchCheck']").Attributes[1].Value =
                                ic.Value.Item.MaxVitalityPatchCheck;
                        }

                        if (allowedChanges.Contains(nameof(ic.Value.Item.ItemRarity)))
                            itemNode.SelectSingleNode("attribute [@id='ItemType']").Attributes[1].Value =
                                ic.Value.Item.ItemRarity.ToString();

                        // max durability cannot be changed
                        //var node = itemNode.SelectSingleNode("attribute [@id='MaxDurabilityPatchCheck']");
                        //if (allowedChanges.Contains(nameof(ic.Value.Item.Stats)) && node!=null)
                        //{
                        //    node.Attributes[1].Value = ic.Value.Item.MaxDurabilityPatchCheck;
                        //}

                        // check if has generation
                        if (allowedChanges.Contains(nameof(ic.Value.Item.Generation)) &&
                            ic.Value.Item.Generation != null)
                        {
                            var genNode = itemNode.SelectSingleNode("children/node [@id='Generation']");
                            if (genNode == null)
                            {
                                // create a generation node
                                genNode = doc.CreateDocumentFragment();
                                // TODO Level is taken from stats.level
                                var level = ic.Value.Item.Stats == null ? "1" : ic.Value.Item.Stats.Level;
                                // ItemType is taken from item.ItemType
                                genNode.InnerXml =
                                    $"<node id=\"Generation\"><attribute id=\"Base\" value=\"{ic.Value.Item.Generation.Base}\" type=\"22\" /><attribute id=\"ItemType\" value=\"{ic.Value.Item.ItemType}\" type=\"22\" /><attribute id=\"Level\" value=\"{level}\" type=\"2\" /><attribute id=\"Random\" value=\"{ic.Value.Item.Generation.Random}\" type=\"4\" /><children /></node>";
                                // insert after max durability node
                                itemNode.SelectSingleNode("children").AppendChild(genNode);
                                genNode = itemNode.SelectSingleNode("children/node [@id='Generation']");
                            }

                            var childrenNode = genNode.SelectSingleNode("children");
                            if (childrenNode == null)
                            {
                                childrenNode = doc.CreateElement("children");
                                genNode.AppendChild(childrenNode);
                                childrenNode = genNode.SelectSingleNode("children");
                            }

                            // wipe all existing boost
                            childrenNode.RemoveAll();
                            // then add each defined boosts
                            foreach (var boostName in ic.Value.Item.Generation.Boosts)
                            {
                                var boost = doc.CreateDocumentFragment();
                                boost.InnerXml =
                                    $"<node id=\"Boost\"><attribute id=\"Object\" value=\"{boostName}\" type=\"22\" /></node>";
                                childrenNode.AppendChild(boost);
                            }

                            itemNode.SelectSingleNode("attribute [@id='IsGenerated']").Attributes[1].Value = "True";
                        }

                        // check if has stats
                        var statsNode = itemNode.SelectSingleNode("children/node [@id='Stats']");
                        if (!allowedChanges.Contains(nameof(ic.Value.Item.Stats)) ||
                            ic.Value.Item.Stats == null)
                            continue;

                        // The item may not have a Stats node yet (e.g. when adding the first
                        // modifier/durability to equipment that had none). Create one matching
                        // the verified in-game schema before writing values into it.
                        if (statsNode == null)
                        {
                            var statsFrag = doc.CreateDocumentFragment();
                            statsFrag.InnerXml =
                                "<node id=\"Stats\">" +
                                "<attribute id=\"IsIdentified\" value=\"1\" type=\"4\" />" +
                                $"<attribute id=\"ItemType\" value=\"{ic.Value.Item.ItemType}\" type=\"22\" />" +
                                $"<attribute id=\"Durability\" value=\"{ic.Value.Item.Stats.Durability}\" type=\"4\" />" +
                                $"<attribute id=\"DurabilityCounter\" value=\"{ic.Value.Item.Stats.DurabilityCounter}\" type=\"4\" />" +
                                $"<attribute id=\"RepairDurabilityPenalty\" value=\"{ic.Value.Item.Stats.RepairDurabilityPenalty}\" type=\"4\" />" +
                                "<attribute id=\"CustomRequirements\" value=\"False\" type=\"19\" />" +
                                $"<attribute id=\"Level\" value=\"{ic.Value.Item.Stats.Level}\" type=\"4\" />" +
                                $"<attribute id=\"Charges\" value=\"{ic.Value.Item.Stats.Charges}\" type=\"4\" />" +
                                "<children><node id=\"PermanentBoost\"><attribute id=\"HasReflection\" value=\"False\" type=\"19\" /><children><node id=\"Abilities\" /></children></node></children>" +
                                "</node>";
                            itemNode.SelectSingleNode("children").AppendChild(statsFrag);
                            statsNode = itemNode.SelectSingleNode("children/node [@id='Stats']");

                            // Equipment needs an item-level MaxDurabilityPatchCheck or the new
                            // durability has no ceiling in-game. Add it if it's missing.
                            if (itemNode.SelectSingleNode("attribute [@id='MaxDurabilityPatchCheck']") == null)
                            {
                                var maxDurFrag = doc.CreateDocumentFragment();
                                maxDurFrag.InnerXml =
                                    "<attribute id=\"MaxDurabilityPatchCheck\" value=\"100\" type=\"4\" />";
                                var maxVit = itemNode.SelectSingleNode("attribute [@id='MaxVitalityPatchCheck']");
                                if (maxVit != null)
                                    itemNode.InsertAfter(maxDurFrag, maxVit);
                                else
                                    itemNode.InsertBefore(maxDurFrag, itemNode.SelectSingleNode("children"));
                            }
                        }

                        statsNode.SelectSingleNode("attribute [@id='Durability']").Attributes[1].Value =
                            ic.Value.Item.Stats.Durability;
                        statsNode.SelectSingleNode("attribute [@id='DurabilityCounter']").Attributes[1].Value =
                            ic.Value.Item.Stats.DurabilityCounter;
                        statsNode.SelectSingleNode("attribute [@id='RepairDurabilityPenalty']").Attributes[1].Value =
                            ic.Value.Item.Stats.RepairDurabilityPenalty;
                        statsNode.SelectSingleNode("attribute [@id='Level']").Attributes[1].Value =
                            ic.Value.Item.Stats.Level;
                        statsNode.SelectSingleNode("attribute [@id='ItemType']").Attributes[1].Value =
                            ic.Value.Item.ItemType; // ItemType is taken from item.ItemType
                        statsNode.SelectSingleNode("attribute [@id='IsIdentified']").Attributes[1].Value = "1";
                    }

                }
                catch (Exception ex)
                {
                    throw new ObjectNullException(ex, ic.Value);
                }
            }

            foreach (var ic in player.ItemChanges)
            {

                if (ic.Value.ChangeType == ChangeType.Add)
                {
                    string mapKey = ic.Value.ItemTemplate.TemplateKey;
                    string stats = ic.Value.ItemTemplate.Stats;
                    string empSlot = ic.Key.ToString();
                    string amount = ic.Value.ItemTemplate.Amount.ToString();
                    string itemType = ic.Value.ItemTemplate.ItemRarity.ToString();

                    // Weapons and armor carry a durability/Stats block. Mirror the verified
                    // schema from a real save item so added equipment comes in with full
                    // durability and is durability/rarity-editable after the file is reopened.
                    // Non-equipment items get no Stats node (they don't have one in-game).
                    bool isEquipment = ic.Value.ItemTemplate.ItemSort == ItemSortType.Weapon ||
                                       ic.Value.ItemTemplate.ItemSort == ItemSortType.Armor;
                    string maxDurabilityAttr = isEquipment
                        ? @"<attribute id=""MaxDurabilityPatchCheck"" value=""100"" type=""4"" />"
                        : "";
                    string statsNodeXml = isEquipment
                        ? $@"<node id=""Stats"">
			                                        <attribute id=""IsIdentified"" value=""1"" type=""4"" />
			                                        <attribute id=""ItemType"" value=""{itemType}"" type=""22"" />
			                                        <attribute id=""Durability"" value=""100"" type=""4"" />
			                                        <attribute id=""DurabilityCounter"" value=""8"" type=""4"" />
			                                        <attribute id=""RepairDurabilityPenalty"" value=""0"" type=""4"" />
			                                        <attribute id=""CustomRequirements"" value=""False"" type=""19"" />
			                                        <attribute id=""Level"" value=""1"" type=""4"" />
			                                        <attribute id=""Charges"" value=""0"" type=""4"" />
			                                        <children>
				                                        <node id=""PermanentBoost"">
					                                        <attribute id=""HasReflection"" value=""False"" type=""19"" />
					                                        <children><node id=""Abilities"" /></children>
				                                        </node>
			                                        </children>
		                                        </node>"
                        : "";

                    string rawXml = $@"<node id=""Item"">
	                                        <attribute id=""Translate"" value=""0 0 0"" type=""12"" />
	                                        <attribute id=""Flags"" value=""33240"" type=""5"" />
	                                        <attribute id=""Level"" value="""" type=""22"" />
	                                        <attribute id=""Rotate"" value="" 1.00  0.00  0.00 &#xD;&#xA; 0.00  1.00  0.00 &#xD;&#xA; 0.00  0.00  1.00 &#xD;&#xA;"" type=""15"" />
	                                        <attribute id=""Scale"" value=""1"" type=""6"" />
	                                        <attribute id=""Global"" value=""True"" type=""19"" />
	                                        <attribute id=""Velocity"" value=""0 0 0"" type=""12"" />
	                                        <attribute id=""GoldValueOverwrite"" value=""-1"" type=""4"" />
	                                        <attribute id=""UnsoldGenerated"" value=""False"" type=""19"" />
	                                        <attribute id=""IsKey"" value=""False"" type=""19"" />
	                                        <attribute id=""TreasureGenerated"" value=""False"" type=""19"" />
	                                        <attribute id=""CurrentTemplate"" value=""{mapKey}"" type=""22"" />
	                                        <attribute id=""CurrentTemplateType"" value=""0"" type=""1"" />
	                                        <attribute id=""OriginalTemplate"" value=""{mapKey}"" type=""22"" />
	                                        <attribute id=""OriginalTemplateType"" value=""0"" type=""1"" />
	                                        <attribute id=""Stats"" value=""{stats}"" type=""22"" />
	                                        <attribute id=""IsGenerated"" value=""False"" type=""19"" />
	                                        <attribute id=""Inventory"" value=""0"" type=""5"" />
	                                        <attribute id=""Parent"" value=""{player.InventoryId}"" type=""5"" />
	                                        <attribute id=""Slot"" value=""{empSlot}"" type=""3"" />
	                                        <attribute id=""Amount"" value=""{amount}"" type=""4"" />
	                                        <attribute id=""Key"" value="""" type=""22"" />
	                                        <attribute id=""LockLevel"" value=""1"" type=""4"" />
	                                        <attribute id=""SurfaceCheckTimer"" value=""0"" type=""6"" />
	                                        <attribute id=""Vitality"" value=""-1"" type=""4"" />
	                                        <attribute id=""LifeTime"" value=""0"" type=""6"" />
	                                        <attribute id=""owner"" value=""67174791"" type=""5"" />
	                                        <attribute id=""ItemType"" value=""{itemType}"" type=""22"" />
	                                        <attribute id=""MaxVitalityPatchCheck"" value=""-1"" type=""4"" />
	                                        {maxDurabilityAttr}
	                                        <children>
		                                        <node id=""ItemMachine"" /><node id=""VariableManager"" />
		                                        <node id=""StatusManager"" />
		                                        {statsNodeXml}
	                                        </children>
                                        </node>";
                    
                    XmlNode targetChildren = doc.SelectSingleNode("//region[@id='Items']/node[@id='Items']/children/node[@id='ItemFactory']/children/node[@id='Items']/children");

                    XmlNodeList filteredItems = targetChildren.SelectNodes(
                                                    $"node[@id='Item' and attribute[@id='Parent' and @value='{player.InventoryId}']]"
                                                );

                    XmlNode lastFiltered = filteredItems.Count > 0 ? filteredItems[filteredItems.Count - 1] : null;

                    XmlDocument temp = new XmlDocument();
                    temp.LoadXml(rawXml);

                    XmlNode imported = doc.ImportNode(temp.DocumentElement, true);

                    if (lastFiltered != null && imported != null)
                    {
                        // "owner" is the owning character's handle and is the same for every
                        // item in a given inventory. The template above hardcodes one value,
                        // which is wrong for any character it doesn't happen to match. Copy the
                        // owner from an existing item in this same inventory so the added item
                        // belongs to the right character.
                        var siblingOwner = lastFiltered.SelectSingleNode("attribute [@id='owner']")?.Attributes[1]?.Value;
                        if (siblingOwner != null)
                        {
                            var ownerAttr = imported.SelectSingleNode("attribute [@id='owner']");
                            if (ownerAttr != null)
                                ownerAttr.Attributes[1].Value = siblingOwner;
                        }

                        lastFiltered.ParentNode.InsertAfter(imported, lastFiltered);
                    }

                }

            }

            return doc;
        }
        
        #endregion
    }


}
