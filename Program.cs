using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ensage;

using Ensage.Common;
using Ensage.Common.Menu;
using Ensage.Common.Extensions;
using Ensage.Common.Objects;
using Ensage.Common.AbilityInfo;


using SharpDX;




namespace LastHitMarker
{
    internal class Program
    {

        private static readonly Menu Menu = new Menu("LastHit Marker", "LastHitMarker", true);
        private static readonly Dictionary<Unit, string> CreepsDictionary = new Dictionary<Unit, string>();
        private static readonly Dictionary<Building, string> TowersDictionary = new Dictionary<Building, string>();
        private static int minDamage;
        private static int maxDamage;
        private static double maxDamageCreep;
        private static double minDamageCreep;


        private static void Main(string[] args)
        {
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnDraw += Drawing_OnDraw;
            Menu.AddItem(new MenuItem("Enable", "Enable")).SetValue(true);
            Menu.AddToMainMenu();
        }

        private static void Game_OnUpdate(EventArgs args)
        {
            if (!Game.IsInGame || Game.IsPaused)
                return;

            if (!Menu.Item("Enable").GetValue<bool>())
                return;

            var player = ObjectManager.LocalPlayer;
            var me = player.Hero;
            var battleFury = me.HasItem(ClassID.CDOTA_Item_Battlefury);
            var quellingBlade = me.HasItem(ClassID.CDOTA_Item_QuellingBlade);
            var talon = me.HasItem(ClassID.CDOTA_Item_Iron_Talon);
            minDamage = me.MinimumDamage + me.BonusDamage;
            maxDamage = me.MaximumDamage + me.BonusDamage;

            //Damage Calculation for Melee and Ranged Quelling Blade Modification
            if (me.IsMelee)
            {
                if (battleFury)
                {
                    minDamageCreep = me.MinimumDamage * 1.6 + me.BonusDamage;
                    maxDamageCreep = me.MaximumDamage * 1.6 + me.BonusDamage;
                }

                else if (quellingBlade || talon)
                {
                    minDamageCreep = me.MinimumDamage + 24 + me.BonusDamage;
                    maxDamageCreep = me.MaximumDamage + 24 + me.BonusDamage;
                }

                else
                {
                    minDamageCreep = minDamage;
                    maxDamageCreep = maxDamage;
                }
            }

            else
            {
                if (battleFury)
                {
                    minDamageCreep = me.MinimumDamage * 1.25 + me.BonusDamage;
                    maxDamageCreep = me.MaximumDamage * 1.25 + me.BonusDamage;
                }

                else if (quellingBlade || talon)
                {
                    minDamageCreep = me.MinimumDamage + 7 + me.BonusDamage;
                    maxDamageCreep = me.MaximumDamage + 7 + me.BonusDamage;
                }
                else
                {
                    minDamageCreep = minDamage;
                    maxDamageCreep = maxDamage;
                }
            }



            //List of Allied Tier 1 Towers
            var allyTowers = ObjectManager.GetEntitiesParallel<Building>().Where(tower =>
                tower.ClassID == ClassID.CDOTA_BaseNPC_Tower
                && tower.Team == player.Team
                && tower.MaximumDamage == 120).ToList();

            //List of Enemy Towers
            var enemyTowers = ObjectManager.GetEntitiesParallel<Building>().Where(tower =>
                tower.ClassID == ClassID.CDOTA_BaseNPC_Tower
                && tower.Team != player.Team).ToList();


            //List of Creeps
            var creeps = ObjectManager.GetEntitiesParallel<Creep>().Where(creep =>
                (creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Lane
                || creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege)
                
                && creep.IsAlive
                && creep.IsVisible
                && creep.IsSpawned).ToList();

            if (player == null || player.Team == Team.Observer || me == null)
                return;

            foreach (var tower in enemyTowers)
            {
                if (tower.IsAlive && tower.IsVisible && (tower.Distance2D(me) < 2000))
                {
                    string towerType;
                    if (tower.Health <= minDamage * (1 - tower.DamageResist) * 0.5)
                    {
                        if (!TowersDictionary.TryGetValue(tower, out towerType) || towerType != "passive") continue;
                        TowersDictionary.Remove(tower);
                        towerType = "active";
                        TowersDictionary.Add(tower, towerType);
                    }
                    else if (tower.Health <= 2 * (maxDamage * (1 - tower.DamageResist) * 0.5))
                    {
                        if (TowersDictionary.TryGetValue(tower, out towerType)) continue;
                        towerType = "passive";
                        TowersDictionary.Add(tower, towerType);
                    }
                    else TowersDictionary.Remove(tower);
                }
                else TowersDictionary.Remove(tower);
            }

            foreach (var creep in creeps)
            {
                if (creep.IsAlive && creep.IsVisible && (creep.Distance2D(me) < 2000))
                {
                    string creepType;

                    var mana = new int[5] { 0, 28, 40, 52, 64 };
                    var manaBurn = new int[5] { 0, 16, 24, 31, 38 };


                    //Anti-Mage exception with ManaBurn
                    if (me.ClassID == ClassID.CDOTA_Unit_Hero_AntiMage && creep.Mana > 0 && me.Spellbook.Spell1.Level > 0 && creep.Health > 0)
                    {
                        if (creep.Health < (minDamageCreep * (1 - creep.DamageResist)) + manaBurn[me.Spellbook.Spell1.Level])
                        {
                            CreepsDictionary.Remove(creep); //Remove Primed Key from the creep and set it to active.
                            creepType = "active";
                            CreepsDictionary.Add(creep, creepType);
                        }

                        else if ((allyTowers.Exists(tower => (creep.Distance2D(tower) < 750))) && (creep.Mana > mana[me.Spellbook.Spell1.Level]) && creep.Health % 110 > (minDamageCreep * (1 - creep.DamageResist)) + manaBurn[me.Spellbook.Spell1.Level])
                        {
                            //if (CreepsDictionary.TryGetValue(creep, out creepType)) continue; //If it is a creep
                            CreepsDictionary.Remove(creep);
                            creepType = "prime";
                            CreepsDictionary.Add(creep, creepType);
                        }
                    }

                    else if (creep.Health > 0 && creep.Health < minDamageCreep * (1 - creep.DamageResist) * ((creep.AttackRange == 690) ? 0.5 : 1) && creep.Team != player.Team) //Is last hittable.
                    {
                        //if (!CreepsDictionary.TryGetValue(creep, out creepType) || creepType != "prime") continue; //Not a creep or not primed skip 
                        CreepsDictionary.Remove(creep); //Remove Primed Key from the creep and set it to active.
                        creepType = "active";
                        CreepsDictionary.Add(creep, creepType);
                    }

                    else if (creep.Health > 0 && creep.Health < (me.MinimumDamage  + me.BonusDamage) * (1 - creep.DamageResist) * ((creep.AttackRange == 690) ? 0.5 : 1) && creep.Team == player.Team) //Is last hittable.
                    {
                        //if (!CreepsDictionary.TryGetValue(creep, out creepType) || creepType != "prime") continue; //Not a creep or not primed skip 
                        CreepsDictionary.Remove(creep); //Remove Primed Key from the creep and set it to active.
                        creepType = "active";
                        CreepsDictionary.Add(creep, creepType);
                    }

                    else if (creep.Health > (me.MinimumDamage + me.BonusDamage) * (1 - creep.DamageResist) * ((creep.AttackRange == 690) ? 0.5 : 1) && creep.Health < ((me.MinimumDamage + me.BonusDamage) * (1 - creep.DamageResist) * ((creep.AttackRange == 690) ? 0.5 : 1)*2) && creep.Team == player.Team) //Is last hittable.
                    {
                        //if (!CreepsDictionary.TryGetValue(creep, out creepType) || creepType != "prime") continue; //Not a creep or not primed skip 
                        CreepsDictionary.Remove(creep); //Remove Primed Key from the creep and set it to active.
                        creepType = "dying";
                        CreepsDictionary.Add(creep, creepType);
                    }
                    else if (creep.Health > minDamageCreep * (1 - creep.DamageResist) * ((creep.AttackRange == 690) ? 0.5 : 1) && creep.Health < (minDamageCreep * (1 - creep.DamageResist) * ((creep.AttackRange == 690) ? 0.5 : 1) * 2) && creep.Team != player.Team) //Is last hittable.
                    {
                        //if (!CreepsDictionary.TryGetValue(creep, out creepType) || creepType != "prime") continue; //Not a creep or not primed skip 
                        CreepsDictionary.Remove(creep); //Remove Primed Key from the creep and set it to active.
                        creepType = "dying";
                        CreepsDictionary.Add(creep, creepType);
                    }



                    else if ((allyTowers.Exists(tower => (creep.Distance2D(tower) < 750))) && ((creep.IsMelee && (creep.Health % 98.2142857 > minDamageCreep * (1 - creep.DamageResist) && creep.Team != player.Team))
                            || ((creep.IsRanged && creep.AttackRange == 690) && (creep.Health % 165 > minDamageCreep * (1 - creep.DamageResist) * 0.5))
                            || ((creep.IsRanged && creep.AttackRange != 690) && (creep.Health % 110 > minDamageCreep * (1 - creep.DamageResist)))))
                    {                        
                        //if (CreepsDictionary.TryGetValue(creep, out creepType)) continue; //If it is a creep
                        CreepsDictionary.Remove(creep);
                        creepType = "prime";
                        CreepsDictionary.Add(creep, creepType);
                    }
                    else CreepsDictionary.Remove(creep);
                }
                else
                {
                    CreepsDictionary.Remove(creep);
                }
            }
        }

        private static void Drawing_OnDraw(EventArgs args)
        {
            if (!Game.IsInGame)
                return;

            var player = ObjectManager.LocalPlayer;

            var creeps = ObjectManager.GetEntitiesParallel<Unit>().Where(creep => (creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Lane || creep.ClassID == ClassID.CDOTA_BaseNPC_Creep_Siege) && creep.IsAlive
                && creep.IsVisible
                && creep.IsSpawned).ToList();

            var enemyTowers = ObjectManager.GetEntitiesParallel<Building>().Where(tower =>
                tower.ClassID == ClassID.CDOTA_BaseNPC_Tower
                && tower.Team != player.Team).ToList();

            foreach (var tower in enemyTowers)
            {
                var enemyTowerPos = tower.Position + new Vector3(0, 0, tower.HealthBarOffset);
                var start = HUDInfo.GetHPbarPosition(tower) + new Vector2(HUDInfo.GetHPBarSizeX(tower) / 2 - 5, HUDInfo.GetHpBarSizeY(tower) - 50);
                var size = new Vector2(15, 15);
                var greenText = Drawing.GetTexture("materials/vgui/hud/hud_timer_full.vmat");
                var greyText2 = Drawing.GetTexture("materials/vgui/hud/minimap_creep.vmat");
                string towerType;

                if (!TowersDictionary.TryGetValue(tower, out towerType)) continue;
                switch (towerType)
                {
                    case "active": Drawing.DrawRect(start, new Vector2(size.Y, size.X), greenText); break;
                    case "passive": Drawing.DrawRect(start, new Vector2(size.Y, size.X), greyText2); break;
                }

            }

            foreach (var creep in creeps)
            {
                //Vector2 screenPos;
                var enemyPos = creep.Position + new Vector3(0, 0, creep.HealthBarOffset);
                var start = HUDInfo.GetHPbarPosition(creep) + new Vector2(HUDInfo.GetHPBarSizeX(creep) / 2 - 5, HUDInfo.GetHpBarSizeY(creep) - 10);
                var size = new Vector2(20, 20);
                var greenText = Drawing.GetTexture("materials/vgui/hud/hud_timer_full.vmat");
                var coinText = Drawing.GetTexture("materials/ensage_ui/other/active_coin.vmat");
                var greyText2 = Drawing.GetTexture("materials/vgui/hud/minimap_creep.vmat");
                var coinsText = Drawing.GetTexture("materials/vgui/hud/gold.vmat");
                string creepType;
                var text2 = string.Format("{0}", (int)creep.Health);

                if (!CreepsDictionary.TryGetValue(creep, out creepType)) continue; //If not creep continue.

                switch (creepType)
                {
                    case "active": Drawing.DrawText(text2, start, new Vector2(size.Y, size.X), Color.Red, FontFlags.AntiAlias | FontFlags.DropShadow); break;
                    //Drawing.DrawRect(start, new Vector2(size.Y, size.X), coinText); break;
                    case "dying": Drawing.DrawText(text2, start, new Vector2(size.Y, size.X), Color.Yellow, FontFlags.AntiAlias | FontFlags.DropShadow); break;
                    case "prime": Drawing.DrawText(text2, start, new Vector2(size.Y, size.X), Color.Pink, FontFlags.AntiAlias | FontFlags.DropShadow); break;
                }
            }
        }

    }
}